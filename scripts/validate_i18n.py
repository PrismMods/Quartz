#!/usr/bin/env python3
"""Validate Quartz translation files.

Usage: validate_i18n.py <lang_dir> <en_us_reference.json>

Hard-fails (exit 1) on: invalid JSON, wrong top-level shape, or a missing/incorrect
0KTL sentinel. The mod's loader silently drops any language block whose 0KTL value
!= "DO_NOT_TRANSLATE_THIS_KEY!", which shows up to users as "the whole language
does nothing" — so that one is a hard error, not a warning.

Warns (exit 0) on: missing keys (they fall back to English) and unknown/dead keys
(not present in en-US). A translation in progress is allowed to lag the key set.

This is the trusted copy run by the i18n-pull workflow, which holds a cross-repo
token. It deliberately runs from the Quartz checkout — never from the quartz-i18n
checkout — so translator-controlled repo content cannot execute in a job that has
the token in its environment. It only reads i18n's JSON as data.
"""
import sys, os, json, glob

KTL_KEY = "0KTL"
KTL_VAL = "DO_NOT_TRANSLATE_THIS_KEY!"


def load_block(path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, dict) or len(data) != 1:
        raise ValueError(f"expected exactly one top-level language block, got {list(data)}")
    lang, block = next(iter(data.items()))
    if not isinstance(block, dict):
        raise ValueError(f"language block '{lang}' is not an object")
    return lang, block


def preview(items, n=15):
    return f"{items[:n]}{' …' if len(items) > n else ''}"


def main():
    if len(sys.argv) != 3:
        print("usage: validate_i18n.py <lang_dir> <en_us_reference.json>", file=sys.stderr)
        return 2
    lang_dir, en_path = sys.argv[1], sys.argv[2]
    _, en_block = load_block(en_path)
    en_keys = set(en_block)
    fail = False
    files = sorted(glob.glob(os.path.join(lang_dir, "*.json")))
    if not files:
        print(f"no *.json found in {lang_dir}", file=sys.stderr)
        return 1
    for path in files:
        base = os.path.basename(path)
        try:
            _, block = load_block(path)
        except Exception as e:
            print(f"::error file={base}::{e}")
            fail = True
            continue
        if base == "en-US.json":
            print(f"{base}: reference ({len(block)} keys)")
            continue
        if block.get(KTL_KEY) != KTL_VAL:
            print(f"::error file={base}::missing/wrong {KTL_KEY} sentinel — the mod will silently ignore this whole file")
            fail = True
        if "0NATIVELANG" not in block:
            print(f"::warning file={base}::no 0NATIVELANG — the picker will show the file code instead of a native name")
        keys = set(block)
        missing = sorted(en_keys - keys)
        extra = sorted(keys - en_keys)
        if missing:
            print(f"::warning file={base}::{len(missing)} missing key(s) will show English: {preview(missing)}")
        if extra:
            print(f"::warning file={base}::{len(extra)} unknown key(s) (dead, not in en-US): {preview(extra)}")
        print(f"{base}: {len(keys)} keys · {len(missing)} missing · {len(extra)} extra")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
