"""Khảo sát một bản vẽ isometric PDF: vector hay ảnh, số nét, chữ trích được, dạng chữ.

Chạy: .venv/Scripts/python tools/iso_survey.py docs/dataTemplate/335-PA-286.pdf
Không in nguyên văn chữ của khách hàng ngoài vài mẫu đã che số.
"""
import re
import sys
from collections import Counter

import fitz  # pymupdf


def mask(s: str) -> str:
    s = re.sub(r"\d", "9", s)
    s = re.sub(r"[A-Z]", "X", s)
    s = re.sub(r"9{2,}", "9+", s)
    s = re.sub(r"X{2,}", "X+", s)
    return s


def main(path: str) -> None:
    doc = fitz.open(path)
    print("trang:", len(doc))
    for i, page in enumerate(doc):
        print(f"--- trang {i + 1}: kích thước {page.rect.width:.0f}x{page.rect.height:.0f} pt, xoay {page.rotation}")
        drawings = page.get_drawings()
        images = page.get_images()
        words = page.get_text("words")
        print("đối tượng vẽ:", len(drawings), " ảnh:", len(images), " từ:", len(words))
        kinds = Counter()
        widths = Counter()
        angles = Counter()
        for d in drawings:
            widths[round(d.get("width") or 0, 2)] += 1
            for it in d["items"]:
                kinds[it[0]] += 1
                if it[0] == "l":
                    p1, p2 = it[1], it[2]
                    import math
                    a = math.degrees(math.atan2(p2.y - p1.y, p2.x - p1.x)) % 180
                    angles[round(a / 5) * 5] += 1
        print("loại nét:", dict(kinds))
        print("độ dày nét:", widths.most_common(6))
        print("góc nét thẳng (độ, bước 5):", sorted(angles.items(), key=lambda kv: -kv[1])[:8])
        fonts = Counter()
        for b in page.get_text("dict")["blocks"]:
            for l in b.get("lines", []):
                for s in l.get("spans", []):
                    fonts[(s["font"], round(s["size"], 1))] += 1
        print("font:", fonts.most_common(5))
        shapes = Counter(mask(w[4]) for w in words)
        print("dạng chữ hay gặp:", shapes.most_common(25))
        sample = [w[4] for w in words if re.search(r"EL|BOP|TOS|NORTH|N\.|PIPE|LINE|REV|SHEET|SCALE|DN|\"", w[4], re.I)][:20]
        print("mẫu chữ liên quan:", [mask(s) for s in sample])


if __name__ == "__main__":
    main(sys.argv[1])
