"""
prompts.py — Xây dựng system prompt và user prompt cho OpenRouter AI
"""

SYSTEM_PROMPT = """Bạn là Trợ lý ảo thông minh của **HAUDOCSSHARE** — nền tảng chia sẻ tài liệu học tập dành riêng cho sinh viên Trường Đại học Kiến trúc Hà Nội (HAU).

## Nhiệm vụ của bạn:
1. Giúp sinh viên tìm kiếm tài liệu học tập một cách thông minh, hiểu câu hỏi tự nhiên
2. Hướng dẫn cách đăng tải, tải xuống và sử dụng hệ thống
3. Tư vấn tài liệu phù hợp theo năm học, ngành học, môn học
4. Trả lời câu hỏi về quy trình kiểm duyệt, quy chế hệ thống

## Quy tắc phản hồi:
- **QUAN TRỌNG**: Trả lời bằng **tiếng Việt** 100%
- **QUAN TRỌNG**: Định dạng bằng **HTML** (dùng <p>, <ul>, <li>, <strong>, <em>, <a>) — KHÔNG dùng Markdown
- Giữ giọng văn thân thiện, gần gũi như người bạn đồng học
- Phản hồi súc tích, không quá 300 từ
- Sử dụng emoji phù hợp để tạo cảm giác sinh động

## Thông tin hệ thống HAUDOCSSHARE:
- **URL đăng tải tài liệu**: /Document/Upload
- **URL duyệt tài liệu**: /Document/Browse  
- **Thời gian duyệt bài**: 1–4 giờ sau khi đăng
- **Định dạng file hỗ trợ**: PDF, DOCX, XLSX, PPTX, ZIP, hình ảnh (tối đa 50MB)
- **Link Google Drive**: Hỗ trợ nhúng preview video bài giảng
- **Email admin**: admin@haudocsshare.local
- **Văn phòng**: Trường Đại học Kiến trúc Hà Nội

## Khi sinh viên hỏi về tài liệu:
- Nếu context có danh sách tài liệu → Hiển thị tối đa 5 tài liệu dưới dạng HTML card với link chi tiết
- Nếu không có tài liệu phù hợp → Gợi ý từ khóa tìm kiếm khác hoặc hướng dẫn vào /Document/Browse
- KHÔNG bịa đặt thông tin về tài liệu không có trong database

## Quy trình đăng tài liệu:
1. Truy cập /Document/Upload trên thanh điều hướng
2. Chọn: Tải file từ máy tính (hỗ trợ PDF, DOCX, ZIP… tối đa 50MB) hoặc Link Google Drive
3. Nhập tiêu đề, danh mục, cấp độ, tác giả, mô tả
4. Nhấn "Gửi để duyệt" → Bài hiển thị sau khi Admin phê duyệt (1–4 giờ)

## Quy trình kiểm duyệt:
- Tất cả tài liệu đăng lên ở trạng thái Chờ duyệt (Pending)
- Admin kiểm tra nội dung, định dạng file, tính an toàn
- Sau khi được duyệt → hiển thị công khai cho toàn bộ cộng đồng

Hãy trả lời câu hỏi của sinh viên dưới đây một cách thông minh và hữu ích nhất có thể."""


def build_docs_context(docs: list[dict]) -> str:
    """Định dạng danh sách tài liệu thành context text cho AI."""
    if not docs:
        return "Không tìm thấy tài liệu nào trong cơ sở dữ liệu khớp với câu hỏi này."

    lines = ["Dưới đây là các tài liệu có trong hệ thống HAUDOCSSHARE phù hợp với câu hỏi:\n"]
    for i, d in enumerate(docs, 1):
        ext = (d.get("FileExtension") or "").lstrip(".").upper() or "LINK"
        size_mb = d.get("FileSize", 0) or 0
        size_str = f" ({size_mb / 1024 / 1024:.1f}MB)" if size_mb > 0 else ""
        lines.append(
            f"{i}. [ID:{d['Id']}] {d['Title']}\n"
            f"   - Loại: {ext}{size_str} | Tác giả: {d.get('Author','Ẩn danh')} | Danh mục: {d.get('CategoryName','Chưa phân loại')}\n"
            f"   - Mô tả: {(d.get('Description') or '')[:120]}...\n"
            f"   - Lượt tải: {d.get('DownloadCount',0)} | Lượt xem: {d.get('ViewCount',0)}\n"
            f"   - Link chi tiết: /Document/Details/{d['Id']}\n"
        )
    return "\n".join(lines)


def build_html_docs_block(docs: list[dict]) -> str:
    """Tạo HTML block hiển thị danh sách tài liệu trong chat."""
    if not docs:
        return ""

    items = []
    for d in docs:
        ext = (d.get("FileExtension") or "").lstrip(".").upper() or "LINK"
        author = d.get("Author") or "Ẩn danh"
        items.append(
            f"<div class='chat-doc-item'>"
            f"  <a href='/Document/Details/{d['Id']}' target='_blank' class='chat-doc-link'>"
            f"    <i class='bi bi-file-earmark-text text-info'></i>"
            f"    <strong>{d['Title']}</strong>"
            f"  </a>"
            f"  <div class='chat-doc-meta'>"
            f"    <span class='badge bg-secondary' style='font-size:.65rem;'>{ext}</span>"
            f"    <span style='font-size:.7rem;color:var(--text-muted);margin-left:.5rem;'>Tác giả: {author}</span>"
            f"  </div>"
            f"</div>"
        )
    return "<div class='chat-doc-list'>" + "".join(items) + "</div>"


def build_user_prompt(user_message: str, docs: list[dict], categories: list[dict]) -> str:
    """Ghép đầy đủ prompt gửi lên OpenRouter."""
    docs_context = build_docs_context(docs)
    cat_names = ", ".join(c["Name"] for c in categories) if categories else ""

    return (
        f"## Context từ cơ sở dữ liệu HAUDOCSSHARE:\n\n"
        f"### Các danh mục tài liệu hiện có:\n{cat_names}\n\n"
        f"### Kết quả tìm kiếm tài liệu liên quan:\n{docs_context}\n\n"
        f"---\n"
        f"## Câu hỏi của sinh viên:\n{user_message}\n\n"
        f"---\n"
        f"Hãy trả lời câu hỏi trên bằng HTML (không dùng Markdown). "
        f"Nếu có tài liệu phù hợp trong danh sách, hãy tạo link HTML dẫn đến /Document/Details/ID tương ứng. "
        f"Kết thúc bằng 3 gợi ý câu hỏi tiếp theo dưới dạng JSON array trong thẻ <!--SUGGESTIONS-->[\"gợi ý 1\",\"gợi ý 2\",\"gợi ý 3\"]<!--/SUGGESTIONS-->."
    )


def parse_suggestions_from_response(text: str) -> tuple[str, list[str]]:
    """
    Tách suggestions (JSON array) ra khỏi response HTML.
    Trả về (html_content, suggestions_list).
    """
    import re
    import json

    suggestions = ["Tìm tài liệu học tập", "Cách đăng tài liệu?", "Liên hệ Admin hỗ trợ"]
    html = text

    pattern = r"<!--SUGGESTIONS-->(.*?)<!--/SUGGESTIONS-->"
    match = re.search(pattern, text, re.DOTALL)
    if match:
        raw = match.group(1).strip()
        try:
            parsed = json.loads(raw)
            if isinstance(parsed, list) and len(parsed) >= 1:
                suggestions = [str(s) for s in parsed[:4]]
        except Exception:
            pass
        html = re.sub(pattern, "", text, flags=re.DOTALL).strip()

    return html, suggestions
