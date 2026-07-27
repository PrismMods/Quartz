#!/usr/bin/env python3
"""Repo conventions the compiler cannot enforce.

Runs in CI (.github/workflows/ci.yml) and locally via `python3 scripts/check_conventions.py`.
Every rule here is one the tree already satisfies; each exists because it
regressed silently once and nothing caught it.

Exits 1 on the first failing rule so CI turns red.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIRS = ("Quartz", "modules", "Quartz.Tests", "sdk")
SKIP_PARTS = {"obj", "bin"}
MAX_LINES = 500

EMPTY_CATCH = re.compile(r"catch\s*(?:\(\s*[A-Za-z0-9_.]+\s*\))?\s*\{\s*\}")


def strip_literals(text):
    """Blank out string, char and comment content so patterns never match inside one."""
    out = list(text)
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == '"' and text.startswith('"""', i):
            j = text.find('"""', i + 3)
            j = n if j < 0 else j + 3
        elif c == "@" and i + 1 < n and text[i + 1] == '"':
            j = i + 2
            while j < n:
                if text[j] == '"':
                    if j + 1 < n and text[j + 1] == '"':
                        j += 2
                        continue
                    j += 1
                    break
                j += 1
        elif c in "\"'":
            j = i + 1
            while j < n:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == c:
                    j += 1
                    break
                if text[j] == "\n":
                    break
                j += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "/":
            j = text.find("\n", i)
            j = n if j < 0 else j
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            j = text.find("*/", i)
            j = n if j < 0 else j + 2
        else:
            i += 1
            continue
        for k in range(i, min(j, n)):
            if out[k] != "\n":
                out[k] = " "
        i = max(j, i + 1)
    return "".join(out)


def sources():
    for directory in SOURCE_DIRS:
        base = ROOT / directory
        if not base.is_dir():
            continue
        for path in sorted(base.rglob("*.cs")):
            if SKIP_PARTS & set(path.parts):
                continue
            yield path


def check_file_size(files):
    """CLAUDE.md: keep files under 500 lines. Partial classes make this cheap to obey."""
    bad = []
    for path, text, _ in files:
        count = len(text.splitlines())
        if count > MAX_LINES:
            bad.append(f"{count:5d} lines  {path.relative_to(ROOT)}")
    return bad, f"no source file exceeds {MAX_LINES} lines"


def check_no_silent_catch(files):
    """`catch { }` reads the same as "forgot to handle it".

    Every swallow states intent instead: Diag.Ignore(e) for a deliberate
    best-effort one, Diag.Warn(e, context) when the failure costs the user
    something.
    """
    bad = []
    for path, _, masked in files:
        for match in EMPTY_CATCH.finditer(masked):
            line = masked.count("\n", 0, match.start()) + 1
            bad.append(f"{path.relative_to(ROOT)}:{line}")
    return bad, "no empty catch blocks"


CHECKS = [
    ("file size", check_file_size),
    ("silent catch", check_no_silent_catch),
]


def main():
    files = []
    for path in sources():
        text = path.read_text(encoding="utf-8")
        files.append((path, text, strip_literals(text)))
    if not files:
        print("FAIL: no C# sources found - run this from the repo, not a copy")
        return 1
    status = 0
    for name, check in CHECKS:
        bad, ok_message = check(files)
        if bad:
            print(f"FAIL ({name}):")
            for entry in bad:
                print(f"  {entry}")
            status = 1
        else:
            print(f"ok: {ok_message}")
    return status


if __name__ == "__main__":
    sys.exit(main())
