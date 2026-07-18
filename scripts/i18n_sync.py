#!/usr/bin/env python3
"""Merge Quartz language files between this repo and PrismMods/Quartz-i18n.

Usage: i18n_sync.py push <mod_lang_dir> <i18n_lang_dir>   (this repo  -> quartz-i18n)
       i18n_sync.py pull <i18n_lang_dir> <mod_lang_dir>   (quartz-i18n -> this repo)
       i18n_sync.py owned                                 (list owned codes, one per line)

Both directions merge. Neither ever deletes a key from a translation. The old
wholesale `cp` did: quartz-i18n's ko-KR lagged this repo by 31 keys, so every pull
proposed deleting 31 live Korean strings (PR #2, 2026-07-17). A key that exists on
the receiving side but not the sending side is kept, not dropped — a stale sender
can no longer erase work.

OWNERSHIP is the load-bearing rule. Every language has exactly ONE authoring side,
and both directions read the one list below. If the directions ever disagree about
a language — push claiming it, pull claiming it back — the on-push sync and the
hourly pull revert each other forever. Change OWNED_HERE and both agree by
construction; hardcode a language in a workflow instead and they will not.

  OWNED_HERE   authored in the mod repo. push overwrites i18n's copy; pull SKIPS.
  anything else
               authored by translators in quartz-i18n. push only SEEDS keys i18n
               is missing (never overwrites a translated value, so refinements
               survive); pull takes i18n's values but keeps keys i18n lacks.

Seeding deliberately does not invent values. A key absent from a community
translation stays absent, so validate_i18n.py keeps reporting it as missing and
translators can still find it; at runtime it already falls back to English. Copying
the English string in would mark it "translated" and hide the work forever.

Like validate_i18n.py this is the trusted copy, run from the Quartz checkout by a
job holding a cross-repo token. It only ever reads the i18n checkout as JSON data
and never executes anything from it.
"""
import sys, os, json, glob, shutil

OWNED_HERE = {"en-US", "ko-KR"}


def load(path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    base = os.path.basename(path)
    if not isinstance(data, dict) or len(data) != 1:
        raise ValueError(f"{base}: expected exactly one top-level language block, got {list(data)}")
    lang, block = next(iter(data.items()))
    if not isinstance(block, dict):
        raise ValueError(f"{base}: language block '{lang}' is not an object")
    return lang, block


def dump(path, lang, block):
    # Reproduces the existing on-disk formatting byte for byte. Any drift here
    # rewrites every language file and changes every manifest hash, which makes
    # every user re-download every language once.
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps({lang: block}, indent=2, ensure_ascii=False) + "\n")


def ordered(block, ref_keys):
    """Order keys like en-US so merged-in keys land next to their neighbours and the
    diff stays readable. Keys absent from en-US keep their relative order at the end."""
    out = {k: block[k] for k in ref_keys if k in block}
    out.update({k: v for k, v in block.items() if k not in out})
    return out


def main():
    # `owned` exists so i18n-pull's belt-and-braces "reset the files we author"
    # guard reads OWNED_HERE too, instead of a second hardcoded list that could
    # silently drift out of step with it.
    if len(sys.argv) == 2 and sys.argv[1] == "owned":
        print("\n".join(sorted(OWNED_HERE)))
        return 0
    if len(sys.argv) != 4 or sys.argv[1] not in ("push", "pull"):
        print(__doc__.strip().split("\n\n")[1], file=sys.stderr)
        return 2
    direction, src_dir, dst_dir = sys.argv[1], sys.argv[2], sys.argv[3]

    # en-US is authoritative for key order, and this repo owns it in both directions.
    mod_lang_dir = src_dir if direction == "push" else dst_dir
    ref_keys = list(load(os.path.join(mod_lang_dir, "en-US.json"))[1])

    touched = []
    for src in sorted(glob.glob(os.path.join(src_dir, "*.json"))):
        base = os.path.basename(src)
        code = base[:-len(".json")]
        dst = os.path.join(dst_dir, base)
        owned = code in OWNED_HERE

        if direction == "pull" and owned:
            print(f"{base}: skipped — authored in this repo, never pulled back")
            continue

        if not os.path.exists(dst):
            # A language the receiving side has never seen: take it wholesale. There
            # is nothing on that side to merge with and nothing to lose.
            shutil.copyfile(src, dst)
            touched.append(base)
            print(f"{base}: new language, copied ({len(load(src)[1])} keys)")
            continue

        src_lang, src_block = load(src)
        dst_lang, dst_block = load(dst)
        if src_lang != dst_lang:
            print(f"::error file={base}::language block is '{src_lang}' on one side and '{dst_lang}' on the other")
            return 1

        before = dict(dst_block)
        if direction == "push" and owned:
            merged = dict(src_block)          # this repo authors it: its copy wins outright
            what = "overwrote (owned here)"
        elif direction == "push":
            merged = dict(dst_block)          # translators author it: only fill gaps
            added = [k for k in src_block if k not in merged]
            merged.update({k: src_block[k] for k in added})
            what = f"seeded {len(added)} missing key(s)" if added else "no gaps to seed"
        else:
            merged = dict(dst_block)          # pull: start from ours so our keys survive...
            merged.update(src_block)          # ...then let translator values win
            what = "merged translator values"

        # The whole point. Only an owned overwrite may drop keys (this repo authors
        # that file, so removing a key there is a real edit); a translation must not
        # lose one to a stale sender, ever.
        dropped = sorted(set(before) - set(merged))
        if dropped and not (direction == "push" and owned):
            print(f"::error file={base}::refusing to drop {len(dropped)} key(s): {dropped[:15]}")
            return 1

        merged = ordered(merged, ref_keys)
        if merged == before:
            print(f"{base}: unchanged")
            continue
        dump(dst, dst_lang, merged)
        touched.append(base)
        gained = sorted(set(merged) - set(before))
        revalued = sorted(k for k in set(merged) & set(before) if merged[k] != before[k])
        print(f"{base}: {what} · {len(before)} -> {len(merged)} keys · +{len(gained)} new · {len(revalued)} changed")

    print(f"\n{direction}: {len(touched)} file(s) changed" + (f" ({', '.join(touched)})" if touched else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
