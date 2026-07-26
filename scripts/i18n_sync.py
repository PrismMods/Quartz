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

# Every feature now ships as a module with its own Lang/ directory, so this repo has
# many authoring roots (core + modules/*/Lang) while quartz-i18n stays ONE file per
# language. That asymmetry is deliberate: translators should not have to open 27
# files, and at runtime every block merges into one dictionary anyway.
#
#   push  unions all roots into the single i18n file.
#   pull  routes each translated key BACK to the root whose en-US declares it, so a
#         module keeps its own translations and the core file does not swallow them.
#         A key no root claims is dropped with a warning — it is dead, or it belongs
#         to a module this checkout does not have.


def roots(core_lang_dir):
    """The core Lang dir first (it owns key order), then every module Lang dir."""
    found = [core_lang_dir]
    repo = os.path.abspath(os.path.join(core_lang_dir, "..", "..", "..", ".."))
    for d in sorted(glob.glob(os.path.join(repo, "modules", "*", "Lang"))):
        if os.path.isdir(d):
            found.append(d)
    return found


def owner_map(all_roots):
    """key -> root that declares it in en-US. First declaration wins; a duplicate key
    across two modules would otherwise ping-pong between them on every pull."""
    owners = {}
    for root in all_roots:
        en = os.path.join(root, "en-US.json")
        if not os.path.exists(en):
            continue
        for key in load(en)[1]:
            owners.setdefault(key, root)
    return owners


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
    if len(sys.argv) == 4 and sys.argv[1] == "merged-ref":
        # The i18n side holds ONE file per language covering core + every module, so
        # validating it against core's en-US alone would report every module key as
        # dead. This writes the union en-US that the i18n side is measured against.
        all_roots = roots(sys.argv[2])
        merged = {}
        for root in all_roots:
            en = os.path.join(root, "en-US.json")
            if os.path.exists(en):
                lang, block = load(en)
                merged.update(block)
        dump(sys.argv[3], "en-US", merged)
        print(f"merged en-US reference: {len(merged)} keys from {len(all_roots)} root(s)")
        return 0
    if len(sys.argv) == 2 and sys.argv[1] == "owned":
        print("\n".join(sorted(OWNED_HERE)))
        return 0
    if len(sys.argv) != 4 or sys.argv[1] not in ("push", "pull"):
        print(__doc__.strip().split("\n\n")[1], file=sys.stderr)
        return 2
    direction, src_dir, dst_dir = sys.argv[1], sys.argv[2], sys.argv[3]

    # en-US is authoritative for key order, and this repo owns it in both directions.
    core_lang_dir = src_dir if direction == "push" else dst_dir
    all_roots = roots(core_lang_dir)
    ref_keys = []
    for root in all_roots:
        en = os.path.join(root, "en-US.json")
        if os.path.exists(en):
            ref_keys.extend(k for k in load(en)[1] if k not in ref_keys)
    if len(all_roots) > 1:
        print(f"lang roots: core + {len(all_roots) - 1} module(s)")

    if direction == "push":
        return push_all(all_roots, dst_dir, ref_keys)
    return pull_all(src_dir, all_roots, ref_keys)


def push_all(all_roots, dst_dir, ref_keys):
    """Union every root into one file per language in quartz-i18n."""
    codes = set()
    for root in all_roots:
        for path in glob.glob(os.path.join(root, "*.json")):
            codes.add(os.path.basename(path)[:-len(".json")])
    touched = []
    for code in sorted(codes):
        base = code + ".json"
        merged_src, lang = {}, None
        for root in all_roots:
            path = os.path.join(root, base)
            if not os.path.exists(path):
                continue
            lang, block = load(path)
            merged_src.update(block)
        if lang is None:
            continue
        dst = os.path.join(dst_dir, base)
        if not os.path.exists(dst):
            dump(dst, lang, ordered(merged_src, ref_keys))
            touched.append(base)
            print(f"{base}: new language, copied ({len(merged_src)} keys)")
            continue
        dst_lang, dst_block = load(dst)
        if lang != dst_lang:
            print(f"::error file={base}::language block is '{lang}' on one side and '{dst_lang}' on the other")
            return 1
        before = dict(dst_block)
        if code in OWNED_HERE:
            merged = dict(merged_src)
            what = "overwrote (owned here)"
        else:
            merged = dict(dst_block)
            added = [k for k in merged_src if k not in merged]
            merged.update({k: merged_src[k] for k in added})
            what = f"seeded {len(added)} missing key(s)" if added else "no gaps to seed"
            dropped = sorted(set(before) - set(merged))
            if dropped:
                print(f"::error file={base}::refusing to drop {len(dropped)} key(s): {dropped[:15]}")
                return 1
        merged = ordered(merged, ref_keys)
        if merged == before:
            print(f"{base}: unchanged")
            continue
        dump(dst, dst_lang, merged)
        touched.append(base)
        print(f"{base}: {what} · {len(before)} -> {len(merged)} keys")
    print(f"\n{len(touched)} file(s) changed" if touched else "\nnothing to do")
    return 0


def pull_all(src_dir, all_roots, ref_keys):
    """Split each translated file back to the root that declares each key."""
    owners = owner_map(all_roots)
    touched = []
    for src in sorted(glob.glob(os.path.join(src_dir, "*.json"))):
        base = os.path.basename(src)
        code = base[:-len(".json")]
        if code in OWNED_HERE:
            print(f"{base}: skipped — authored in this repo, never pulled back")
            continue
        lang, block = load(src)
        homeless = [k for k in block if k not in owners and k != "0KTL"]
        if homeless:
            print(f"{base}: {len(homeless)} key(s) match no module here, left out: {homeless[:10]}")
        for root in all_roots:
            share = {k: v for k, v in block.items() if owners.get(k) == root}
            if not share and root != all_roots[0]:
                continue
            share["0KTL"] = block.get("0KTL", "DO_NOT_TRANSLATE_THIS_KEY!")
            dst = os.path.join(root, base)
            en = os.path.join(root, "en-US.json")
            root_keys = list(load(en)[1]) if os.path.exists(en) else ref_keys
            if os.path.exists(dst):
                dst_lang, dst_block = load(dst)
                if lang != dst_lang:
                    print(f"::error file={dst}::language block is '{lang}' on one side and '{dst_lang}' on the other")
                    return 1
                before = dict(dst_block)
                merged = dict(dst_block)
                merged.update(share)
            else:
                dst_lang, before, merged = lang, {}, dict(share)
            merged = ordered(merged, root_keys)
            if merged == before:
                continue
            dump(dst, dst_lang, merged)
            touched.append(os.path.relpath(dst))
            print(f"{os.path.relpath(dst)}: {len(before)} -> {len(merged)} keys")
    print(f"\n{len(touched)} file(s) changed" if touched else "\nnothing to do")
    return 0


if __name__ == "__main__":
    sys.exit(main())
