"""
Database maintenance (destructive options live under Database/danger/).

  • Default: apply numbered SQL files in Database/migrations/ (skips ones already recorded).
  • --reset-schema: replay netflower_schema.sql (DROPS tables — wipes data).

Requires .env at project root with DB_HOST, DB_PORT, DB_USER, DB_PASSWORD, DB_NAME.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

import mysql.connector
from dotenv import load_dotenv

_SCRIPT_DIR = Path(__file__).resolve().parent
_DATABASE_DIR = _SCRIPT_DIR.parent
_PROJECT_ROOT = _DATABASE_DIR.parent
_SCHEMA_FILE = _DATABASE_DIR / "netflower_schema.sql"
_MIGRATIONS_DIR = _DATABASE_DIR / "migrations"

load_dotenv(dotenv_path=_PROJECT_ROOT / ".env")


def _connect():
    port = os.getenv("DB_PORT", "3306")
    try:
        port = int(port)
    except ValueError:
        port = 3306
    return mysql.connector.connect(
        host=os.getenv("DB_HOST"),
        port=port,
        user=os.getenv("DB_USER"),
        password=os.getenv("DB_PASSWORD"),
        database=os.getenv("DB_NAME"),
        autocommit=False,
    )


def _ensure_migrations_table(cursor) -> None:
    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            filename VARCHAR(255) NOT NULL PRIMARY KEY,
            applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """
    )


def _already_applied(cursor, name: str) -> bool:
    cursor.execute(
        "SELECT 1 FROM schema_migrations WHERE filename = %s LIMIT 1",
        (name,),
    )
    return cursor.fetchone() is not None


def _record_migration(cursor, name: str) -> None:
    cursor.execute(
        "INSERT INTO schema_migrations (filename) VALUES (%s)",
        (name,),
    )


def execute_multi_statement_sql(cursor, sql: str) -> None:
    """Run a script that may contain multiple statements (no SOURCE directive)."""
    sql = sql.strip()
    if not sql:
        return
    for _ in cursor.execute(sql, multi=True):
        pass


def apply_migrations(*, force: bool = False) -> int:
    """
    Apply *.sql in Database/migrations/ in sorted order.
    If force=False, skip files already listed in schema_migrations.
    Returns number of files applied this run.
    """
    if not _MIGRATIONS_DIR.is_dir():
        print(f"No migrations directory: {_MIGRATIONS_DIR}", file=sys.stderr)
        return 0

    files = sorted(p for p in _MIGRATIONS_DIR.glob("*.sql") if p.is_file())
    if not files:
        print("No .sql files in migrations/.")
        return 0

    conn = _connect()
    applied = 0
    try:
        cursor = conn.cursor()
        _ensure_migrations_table(cursor)
        conn.commit()

        for path in files:
            name = path.name
            if not force and _already_applied(cursor, name):
                print(f"  skip (already applied): {name}")
                continue
            body = path.read_text(encoding="utf-8")
            print(f"  applying: {name}")
            try:
                execute_multi_statement_sql(cursor, body)
                if not force:
                    _record_migration(cursor, name)
                conn.commit()
                applied += 1
            except mysql.connector.Error as e:
                conn.rollback()
                # 1060 = ER_DUP_FIELDNAME, 1050 = ER_TABLE_EXISTS_ERROR
                if e.errno in (1060, 1050, 1061, 1091):
                    print(
                        f"  warning: {name} partially skipped ({e.errno}: {e.msg}). "
                        "Schema may already match.",
                        file=sys.stderr,
                    )
                    if not force:
                        _record_migration(cursor, name)
                    conn.commit()
                    applied += 1
                else:
                    raise
        cursor.close()
    finally:
        conn.close()

    print(f"Applied {applied} migration file(s).")
    return applied


def apply_full_schema() -> None:
    """
    Run netflower_schema.sql — includes DROP TABLE. All application data in those tables is lost.
    """
    if not _SCHEMA_FILE.is_file():
        raise FileNotFoundError(_SCHEMA_FILE)
    sql = _SCHEMA_FILE.read_text(encoding="utf-8")
    conn = _connect()
    try:
        cursor = conn.cursor()
        print(f"Executing full schema from {_SCHEMA_FILE.name} …")
        execute_multi_statement_sql(cursor, sql)
        conn.commit()
        cursor.close()
        print("Full schema applied.")
    finally:
        conn.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--reset-schema",
        action="store_true",
        help="Replay netflower_schema.sql (DROPS TABLES — destructive).",
    )
    parser.add_argument(
        "--force-migrations",
        action="store_true",
        help="Re-apply every migration file even if already recorded (duplicate DDL may warn).",
    )
    args = parser.parse_args()

    required = ("DB_HOST", "DB_USER", "DB_PASSWORD", "DB_NAME")
    missing = [k for k in required if not os.getenv(k)]
    if missing:
        print(f"Missing env vars: {missing}", file=sys.stderr)
        sys.exit(1)

    if args.reset_schema:
        print("*** WARNING: This will DROP and recreate tables from netflower_schema.sql ***")
        confirm = input("Type YES to continue: ").strip()
        if confirm != "YES":
            print("Aborted.")
            sys.exit(0)
        apply_full_schema()
        print("Applying migrations (e.g. lobby) on top of base schema…")

    apply_migrations(force=args.force_migrations)


if __name__ == "__main__":
    main()
