@echo off
echo ============================================
echo   HAUDOCSSHARE AI Chatbot Service v2.0
echo   Powered by Google Gemini + FastAPI
echo ============================================
echo.

cd /d "%~dp0"

:: Kiem tra Python
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [LOI] Khong tim thay Python. Vui long cai dat Python 3.11+
    pause
    exit /b 1
)

:: Cai dat thu vien neu chua co
echo [1/3] Kiem tra va cai dat thu vien Python...
pip install -r requirements.txt -q
if %errorlevel% neq 0 (
    echo [LOI] Khong the cai dat thu vien. Kiem tra ket noi mang.
    pause
    exit /b 1
)

:: Kiem tra .env
if not exist ".env" (
    echo [LOI] Khong tim thay file .env. Vui long tao file .env voi OPENROUTER_API_KEY.
    pause
    exit /b 1
)

echo [2/3] Thu vien da san sang!
echo [3/3] Khoi dong AI Chatbot Service tren cong 8001...
echo.
echo  URL: http://localhost:8001
echo  Health check: http://localhost:8001/health
echo  Docs: http://localhost:8001/docs
echo.
echo  Nhan Ctrl+C de dung service.
echo.

python -m uvicorn main:app --host 0.0.0.0 --port 8001 --reload

pause
