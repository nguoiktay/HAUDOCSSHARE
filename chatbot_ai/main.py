"""
main.py — FastAPI AI Chatbot Service cho HAUDOCSSHARE
Sử dụng Google Gen AI SDK (google.genai) + Gemini với retry & model fallback
Chạy: uvicorn main:app --port 8001 --reload
"""
import os
import time
import logging
import asyncio
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from dotenv import load_dotenv
from openai import AsyncOpenAI

from db_search import search_documents, get_all_categories, get_recent_documents
from prompts import (
    SYSTEM_PROMPT,
    build_user_prompt,
    build_html_docs_block,
    parse_suggestions_from_response,
)

# ─── Logging ────────────────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("chatbot_ai")

# ─── Load .env ───────────────────────────────────────────────────────────────
load_dotenv(dotenv_path=os.path.join(os.path.dirname(__file__), ".env"))

OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY", "")
if not OPENROUTER_API_KEY:
    raise RuntimeError("OPENROUTER_API_KEY chưa được cấu hình trong file .env!")

MODEL_PRIORITY = [
    "google/gemma-4-31b-it:free",
    "openrouter/free",
    "meta-llama/llama-3.3-70b-instruct:free",
    "qwen/qwen3-coder:free",
    "meta-llama/llama-3.2-3b-instruct:free",
]

# ─── OpenRouter client ────────────────────────────────────────────────────────────
ai_client: AsyncOpenAI | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global ai_client
    logger.info("🚀 Khởi tạo OpenRouter client...")
    ai_client = AsyncOpenAI(
        base_url="https://openrouter.ai/api/v1",
        api_key=OPENROUTER_API_KEY,
        max_retries=0,
    )
    logger.info(f"✅ OpenRouter client sẵn sàng! Model ưu tiên: {MODEL_PRIORITY[0]}")
    yield
    logger.info("🛑 Chatbot AI Service đã tắt.")


# ─── FastAPI app ─────────────────────────────────────────────────────────────
app = FastAPI(
    title="HAUDOCSSHARE AI Chatbot Service",
    description="Trợ lý ảo thông minh dùng OpenRouter AI — có retry & model fallback",
    version="2.2.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["POST", "GET"],
    allow_headers=["*"],
)


# ─── Schemas ─────────────────────────────────────────────────────────────────
class ChatHistoryItem(BaseModel):
    role: str       # "user" hoặc "model"
    content: str


class ChatRequest(BaseModel):
    message: str
    history: list[ChatHistoryItem] | None = None


class ChatResponse(BaseModel):
    response: str
    suggestions: list[str]
    docs_found: int = 0
    model_used: str = ""


# ─── Helper: Gọi OpenRouter với retry + model fallback (Async) ────────────────
async def call_openrouter_with_fallback(
    prompt: str,
    system: str,
    history: list[dict],
    max_retries: int = 1,
    retry_delay: float = 1.0,
) -> tuple[str, str]:
    """
    Gọi OpenRouter bất đồng bộ. Nếu lỗi mạng hoặc quota hoặc timeout → thử model tiếp theo.
    Trả về (response_text, model_name_used).
    """
    if ai_client is None:
        raise RuntimeError("OpenRouter client chưa khởi tạo")

    last_error: Exception | None = None

    for model_name in MODEL_PRIORITY:
        for attempt in range(max_retries + 1):
            try:
                logger.info(f"🤖 Gọi {model_name} (lần {attempt + 1})...")

                messages = [{"role": "system", "content": system}]
                messages.extend(history)
                messages.append({"role": "user", "content": prompt})

                # Gọi OpenRouter Chat Completion API
                resp = await asyncio.wait_for(
                    ai_client.chat.completions.create(
                        model=model_name,
                        messages=messages,
                        temperature=0.65,
                        top_p=0.9,
                        extra_headers={
                            "HTTP-Referer": "https://github.com/googlecolab/colabtools",
                            "X-Title": "HAUDOCSSHARE AI Chatbot",
                        }
                    ),
                    timeout=20.0  # Timeout 20 giây mỗi lượt gọi để chuyển model khác nếu bị nghẽn
                )
                text = resp.choices[0].message.content or ""
                logger.info(f"✅ {model_name} phản hồi ({len(text)} ký tự)")
                return text, model_name

            except asyncio.TimeoutError:
                logger.warning(f"⏳ {model_name} bị quá thời gian (timeout 20s) → thử model tiếp theo")
                last_error = Exception("Timeout 20s")
                break  # Bỏ qua model bị nghẽn, thử model tiếp theo

            except Exception as e:
                err_str = str(e)
                last_error = e

                is_retryable = "503" in err_str or "UNAVAILABLE" in err_str or "502" in err_str or "504" in err_str
                is_quota     = "429" in err_str or "RESOURCE_EXHAUSTED" in err_str or "402" in err_str or "Insufficient Funds" in err_str
                is_not_found = "404" in err_str or "NOT_FOUND" in err_str

                if is_not_found:
                    logger.warning(f"⚠️  {model_name} không tồn tại → thử model tiếp theo")
                    break

                if is_quota:
                    logger.warning(f"⚠️  {model_name} hết quota hoặc lỗi số dư → thử model tiếp theo")
                    break

                if is_retryable and attempt < max_retries:
                    wait = retry_delay * (attempt + 1)
                    logger.warning(f"⚠️  {model_name} gặp lỗi kết nối, thử lại sau {wait:.0f}s...")
                    await asyncio.sleep(wait)
                    continue

                logger.error(f"❌ {model_name} lỗi: {err_str[:200]}")
                break  # Thử model tiếp theo

    # Tất cả model đều thất bại
    raise HTTPException(
        status_code=502,
        detail=f"Tất cả model OpenRouter đều lỗi. Lỗi cuối: {str(last_error)[:300]}"
    )


# ─── Endpoints ───────────────────────────────────────────────────────────────
@app.get("/health")
def health_check():
    return {
        "status": "ok",
        "models": MODEL_PRIORITY,
        "primary": MODEL_PRIORITY[0],
    }


@app.post("/query", response_model=ChatResponse)
async def query_chatbot(req: ChatRequest):
    """
    1. Tìm tài liệu từ SQLite (RAG-lite)
    2. Gọi Gemini với retry + fallback model
    3. Trả về HTML response + suggestions
    """
    if not req.message or not req.message.strip():
        return ChatResponse(
            response=(
                "<p>👋 <strong>Xin chào!</strong> Mình là Trợ lý ảo HAUDOCSSHARE — "
                "được hỗ trợ bởi <strong>AI OpenRouter (Gemini / Llama)</strong>.<br>"
                "Hỏi mình về tài liệu, cách sử dụng hệ thống hay bất cứ điều gì nhé!</p>"
            ),
            suggestions=["Tìm tài liệu Đại số", "Cách đăng tài liệu?", "Quy chế kiểm duyệt?"],
        )

    user_msg = req.message.strip()
    logger.info(f"📩 Câu hỏi: {user_msg[:100]}")

    # 1. Tìm tài liệu từ SQLite
    docs       = search_documents(user_msg)
    categories = get_all_categories()
    logger.info(f"📚 Tìm thấy {len(docs)} tài liệu")

    # 2. Build prompt
    user_prompt = build_user_prompt(user_msg, docs, categories)

    # 3. Build lịch sử chat (OpenAI format)
    chat_history = []
    if req.history:
        for h in req.history[-6:]:
            role = "assistant" if h.role == "model" else "user"
            chat_history.append({"role": role, "content": h.content})

    # 4. Gọi OpenRouter với retry + fallback (Async)
    raw_text, model_used = await call_openrouter_with_fallback(
        prompt=user_prompt,
        system=SYSTEM_PROMPT,
        history=chat_history,
    )

    # 5. Parse suggestions
    html_response, suggestions = parse_suggestions_from_response(raw_text)

    # 6. Append doc cards nếu AI chưa render
    if docs and "<div class='chat-doc-list'>" not in html_response:
        doc_block = build_html_docs_block(docs)
        html_response += f"\n{doc_block}"

    return ChatResponse(
        response=html_response,
        suggestions=suggestions,
        docs_found=len(docs),
        model_used=model_used,
    )


@app.get("/recent")
def get_recent():
    docs = get_recent_documents(5)
    return {"docs": docs}
