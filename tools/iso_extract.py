"""Thử nghiệm trích dữ liệu từ isometric PDF vector: nét ống theo ba trục, toạ độ neo E/N/EL, kích thước, bảng vật tư.

Chạy: .venv/Scripts/python tools/iso_extract.py docs/dataTemplate/335-PA-286.pdf
Mục đích: chứng minh thông tin cần cho 3D có trong PDF và lấy được bằng máy. Chưa phải bộ đọc sản phẩm.
"""
import math
import re
import sys
from collections import Counter

import pymupdf

ISO_AXES = {30: "E", 150: "N", 90: "UP"}  # góc nét trên giấy ứng với trục thế giới, kiểm tra lại bằng mũi tên Bắc


def axis_of(p1, p2, tol=4):
    a = math.degrees(math.atan2(-(p2.y - p1.y), p2.x - p1.x)) % 180
    for deg, name in ISO_AXES.items():
        if abs(a - deg) <= tol:
            return name
    return None


def main(path):
    page = pymupdf.open(path)[0]
    words = page.get_text("words")
    drawings = page.get_drawings()

    # 1. Nét ống: nét dày nhất nằm trên trục isometric
    thick = [d for d in drawings if (d.get("width") or 0) >= 2.0]
    runs = []
    for d in thick:
        for it in d["items"]:
            if it[0] == "l":
                ax = axis_of(it[1], it[2])
                runs.append((ax, round(it[1].x, 1), round(it[1].y, 1), round(it[2].x, 1), round(it[2].y, 1), round(math.dist(it[1], it[2]), 1)))
    print(f"nét ống (dày >= 2 pt): {len(runs)}, theo trục: {Counter(r[0] for r in runs)}")
    for r in runs:
        print("   ", r)

    # 2. Toạ độ neo: nhóm E / N / EL kề nhau
    coords = []
    for i, w in enumerate(words):
        m = re.match(r"^(E|N|EL)$", w[4])
        if m and i + 1 < len(words):
            v = words[i + 1][4]
            if re.match(r"^[+-]?\d{5,}$", v):
                coords.append((w[4], int(v), round(w[0]), round(w[1])))
    print("toạ độ neo:", coords)

    # 3. Kích thước: số nguyên 2 đến 5 chữ số không phải toạ độ, cùng dạng chữ
    dims = [(int(w[4]), round(w[0]), round(w[1])) for w in words if re.match(r"^\d{2,5}$", w[4]) and int(w[4]) < 100000]
    dims = [d for d in dims if d[0] not in {v for _, v, _, _ in coords}]
    print(f"kích thước ứng viên: {len(dims)}:", sorted(d[0] for d in dims))

    # 4. Số hiệu tuyến, cỡ ống
    line = [w[4] for w in words if re.match(r"^\d+-[A-Z]+-\d+-\d+\"-", w[4])]
    nps = Counter(w[4] for w in words if re.match(r"^\d+(X\d+(/\d+)?)?\"?NPD$", w[4]))
    print("số hiệu tuyến:", [re.sub(r"\d", "9", s) for s in line], " cỡ:", dict(nps))

    # 5. Bảng vật tư: dòng bắt đầu bằng số thứ tự ở cột PT NO
    bom = []
    rows = {}
    for w in words:
        if w[0] > page.rect.width * 0.68:
            rows.setdefault(round(w[1] / 4), []).append(w)
    for y in sorted(rows):
        ws = sorted(rows[y], key=lambda w: w[0])
        if re.match(r"^\d{1,2}$", ws[0][4]) and len(ws) > 3:
            bom.append(" ".join(w[4] for w in ws)[:90])
    print(f"bảng vật tư: {len(bom)} dòng")
    for b in bom:
        print("   ", b)

    # 6. Mũi tên Bắc: chữ N ở góc trên trái
    north = [w for w in words if w[4] == "N" and w[1] < page.rect.height * 0.1]
    print("mũi tên Bắc tại:", [(round(w[0]), round(w[1])) for w in north])


if __name__ == "__main__":
    main(sys.argv[1])
