"""Phân tích chữ trên layer nhãn của một bản dump đối tượng (SourceDump JSON) để mở rộng mẫu nhãn.

Chạy: .venv/Scripts/python tools/analyze_tags.py data/private/A410B4001.dxfreader.json data/profiles/nsrp-piping-layout.json
In ra: số chữ khớp từng mẫu, và các dạng chữ chưa khớp đã che số (chữ số -> n, chữ hoa -> X) kèm số lần gặp.
Không in nguyên văn chữ của khách hàng.
"""
import json
import re
import sys
from collections import Counter
from pathlib import Path


def mask(s: str) -> str:
    s = re.sub(r"\d", "n", s)
    s = re.sub(r"[A-Z]", "X", s)
    s = re.sub(r"[a-z]", "x", s)
    s = re.sub(r"9{2,}", "9+", s)
    s = re.sub(r"X{2,}", "X+", s)
    s = re.sub(r"x{2,}", "x+", s)
    return s


def main(dump_path: str, profile_path: str, layers: str = "TEXT,DIMN"):
    dump = json.loads(Path(dump_path).read_text(encoding="utf-8"))
    profile = json.loads(Path(profile_path).read_text(encoding="utf-8"))
    patterns = [(p["name"], re.compile(p["regex"].replace("(?<", "(?P<"))) for p in profile.get("tagPatterns", []) + profile.get("elevationPatterns", [])]
    wanted = {l.upper() for l in layers.split(",")}
    texts = [e for e in dump["entities"] if e.get("kind") in ("text", "mText") and e.get("layer", "").upper() in wanted and e.get("text")]
    print(f"{len(texts)} chữ trên layer {sorted(wanted)}")
    hits = Counter()
    unmatched = Counter()
    for e in texts:
        t = " ".join(e["text"].split())
        for name, rx in patterns:
            if rx.match(t):
                hits[name] += 1
                break
        else:
            unmatched[mask(t)] += 1
    for name, n in hits.most_common():
        print(f"  {name:40} {n}")
    print(f"Chưa khớp: {sum(unmatched.values())} chữ, {len(unmatched)} dạng. Dạng gặp nhiều nhất:")
    for shape, n in unmatched.most_common(40):
        print(f"  {n:5}  {shape}")


if __name__ == "__main__":
    main(*sys.argv[1:])
