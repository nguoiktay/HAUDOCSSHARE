"""
db_search.py — Truy vấn tài liệu từ SQLite cho chatbot AI HAUDOCSSHARE
"""
import sqlite3
import os
from typing import Optional


def get_db_path() -> str:
    """Lấy đường dẫn tới file database SQLite."""
    db_path = os.getenv("DB_PATH", "../documentshare.db")
    # Resolve relative to this file's directory
    if not os.path.isabs(db_path):
        base = os.path.dirname(os.path.abspath(__file__))
        db_path = os.path.normpath(os.path.join(base, db_path))
    return db_path


def search_documents(query: str, limit: int = 6) -> list[dict]:
    """
    Tìm kiếm tài liệu đã duyệt (IsApproved=1) theo từ khóa.
    Tìm kiếm trong: Title, Description, Tags, Author.
    Trả về list dict gồm thông tin cần thiết để nhúng vào context AI.
    """
    db_path = get_db_path()
    if not os.path.exists(db_path):
        return []

    keyword = f"%{query.lower()}%"

    sql = """
        SELECT
            d.Id,
            d.Title,
            d.Description,
            d.Author,
            d.Tags,
            d.FileExtension,
            d.FileSize,
            d.DownloadCount,
            d.ViewCount,
            d.Level,
            d.Language,
            c.Name AS CategoryName
        FROM Documents d
        LEFT JOIN Categories c ON d.CategoryId = c.Id
        WHERE d.IsApproved = 1
          AND d.ParentId IS NULL
          AND (
            LOWER(d.Title)       LIKE ?
            OR LOWER(d.Description) LIKE ?
            OR LOWER(d.Tags)     LIKE ?
            OR LOWER(d.Author)   LIKE ?
            OR LOWER(c.Name)     LIKE ?
          )
        ORDER BY d.DownloadCount DESC, d.ViewCount DESC
        LIMIT ?
    """

    try:
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        cur = conn.execute(sql, (keyword, keyword, keyword, keyword, keyword, limit))
        rows = cur.fetchall()
        conn.close()
        return [dict(r) for r in rows]
    except Exception as e:
        print(f"[db_search] Error: {e}")
        return []


def get_all_categories() -> list[dict]:
    """Lấy tất cả danh mục môn học."""
    db_path = get_db_path()
    if not os.path.exists(db_path):
        return []

    sql = "SELECT Id, Name, Description FROM Categories ORDER BY Id"
    try:
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        cur = conn.execute(sql)
        rows = cur.fetchall()
        conn.close()
        return [dict(r) for r in rows]
    except Exception as e:
        print(f"[db_search] Categories error: {e}")
        return []


def get_recent_documents(limit: int = 5) -> list[dict]:
    """Lấy các tài liệu mới nhất."""
    db_path = get_db_path()
    if not os.path.exists(db_path):
        return []

    sql = """
        SELECT d.Id, d.Title, d.Author, d.FileExtension, c.Name AS CategoryName
        FROM Documents d
        LEFT JOIN Categories c ON d.CategoryId = c.Id
        WHERE d.IsApproved = 1 AND d.ParentId IS NULL
        ORDER BY d.UploadDate DESC
        LIMIT ?
    """
    try:
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        cur = conn.execute(sql, (limit,))
        rows = cur.fetchall()
        conn.close()
        return [dict(r) for r in rows]
    except Exception as e:
        print(f"[db_search] Recent docs error: {e}")
        return []
