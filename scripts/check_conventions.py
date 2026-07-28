#!/usr/bin/env python3
"""Repo conventions the compiler cannot enforce.

Runs in CI (.github/workflows/ci.yml) and locally via `python3 scripts/check_conventions.py`.
Every rule here is one the tree already satisfies; each exists because it
regressed silently once and nothing caught it.

Exits 1 on the first failing rule so CI turns red.
"""
import json
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


SILENT_EXEMPT_DIRS = ("Quartz.Tests",)
HANDLED = ("Diag.", "throw", "Log", "Err(", "Warn(", "Error(", "Console.")
CATCH_HEAD = re.compile(
    r"\bcatch\b[ \t]*"
    r"(?:\(\s*[A-Za-z0-9_.]+(?:\s+(?P<var>[A-Za-z_]\w*))?\s*\)[ \t]*)?"
    r"(?:when\s*\([^)]*\)[ \t]*)?\{"
)


def catch_bodies(masked):
    """(offset, body) for every catch block, brace-matched."""
    for match in CATCH_HEAD.finditer(masked):
        depth = 0
        open_brace = match.end() - 1
        for index in range(open_brace, len(masked)):
            if masked[index] == "{":
                depth += 1
            elif masked[index] == "}":
                depth -= 1
                if depth == 0:
                    yield match, masked[open_brace + 1:index]
                    break


def check_no_silent_swallow(files):
    """A swallow must state its intent, whatever shape the body is.

    The empty-catch rule above only sees `catch { }`. `catch { return false; }` reads
    exactly the same to a reader and hides exactly as much - and 191 of those had
    accumulated behind the narrower rule. A catch is fine when it rethrows, logs, or
    even just names the exception; it is a silent swallow when the failure leaves no
    trace at all, and then it says Diag.Ignore(e) so the reader knows that was meant.

    Quartz.Tests is exempt: its Diag tests assert on the swallow counter, so seeding
    Diag calls into their own catches would change what they measure.
    """
    bad = []
    for path, _, masked in files:
        relative = path.relative_to(ROOT)
        if relative.parts[0] in SILENT_EXEMPT_DIRS:
            continue
        for match, body in catch_bodies(masked):
            if any(token in body for token in HANDLED):
                continue
            var = match.group("var")
            if var and re.search(r"\b" + re.escape(var) + r"\b", body):
                continue
            line = masked.count("\n", 0, match.start()) + 1
            bad.append(f"{relative}:{line}")
    return bad, "every exception swallow states its intent"


def check_patch_targets(files):
    """Every Harmony patch target must still exist in the game.

    Mono drops a whole patched method at JIT time when a prefix names a game API
    that is gone, so the feature dies silently - sometimes with the feature switched
    OFF - and the only trace is a line in Player.log. Patch targets are named by
    string (nameof, or Harmony's string form), so neither the compiler nor the
    stub-backed build can catch it.

    tools/StubGen resolves every one of those names against the installed game and
    records the verdict in stubs/PATCH-TARGETS.json. That needs the game, so it runs
    on a maintainer's machine (tools/gen-stubs.sh); this check reads the committed
    result, which needs nothing.
    """
    manifest = ROOT / "stubs" / "PATCH-TARGETS.json"
    if not manifest.is_file():
        return ["stubs/PATCH-TARGETS.json is missing - run tools/gen-stubs.sh"], ""
    try:
        data = json.loads(manifest.read_text(encoding="utf-8"))
    except ValueError as error:
        return [f"stubs/PATCH-TARGETS.json is not valid JSON: {error}"], ""
    broken = [t for t in data.get("targets", []) if not t.get("resolved")]
    if not data.get("targets"):
        return ["stubs/PATCH-TARGETS.json lists no targets - run tools/gen-stubs.sh"], ""
    bad = [
        f"{t.get('type')}.{t.get('member')} (patched, but not in {t.get('assembly')})"
        for t in broken
    ]
    return bad, f"all {len(data['targets'])} Harmony patch targets resolve"


CHECKS = [
    ("file size", check_file_size),
    ("silent catch", check_no_silent_catch),
    ("silent swallow", check_no_silent_swallow),
    ("patch targets", check_patch_targets),
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
