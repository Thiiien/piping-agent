"""I1 thử nghiệm: đồ thị trên giấy của isometric PDF, gán kích thước cho đoạn theo trục, kiểm tra khép kín bằng neo.

Chạy: .venv/Scripts/python tools/iso_graph.py docs/dataTemplate/335-PA-286.pdf
In: chuỗi đoạn ống theo thứ tự đi từ neo đầu, trục, kích thước đã gán, toạ độ thật suy ra tại từng đỉnh,
    và sai lệch khi tới các neo còn lại. Không in nguyên văn chữ khác của bản vẽ.
"""
import math
import re
import sys
from collections import defaultdict

import pymupdf

AXES = {30: "E", 150: "N", 90: "UP"}
AXIS_DIR = {"E": (math.cos(math.radians(30)), -math.sin(math.radians(30))),   # +E lên phải
            "N": (-math.cos(math.radians(30)), -math.sin(math.radians(30))),  # +N lên trái
            "UP": (0.0, -1.0)}                                                # +EL lên trên (y giấy giảm)
SNAP = 1.5


def axis_of(p1, p2, tol=4):
    a = math.degrees(math.atan2(-(p2[1] - p1[1]), p2[0] - p1[0])) % 180
    for deg, name in AXES.items():
        if abs(a - deg) <= tol:
            return name
    return None


def sign_along(axis, p1, p2):
    d = AXIS_DIR[axis]
    return 1 if (p2[0] - p1[0]) * d[0] + (p2[1] - p1[1]) * d[1] > 0 else -1


def seg_dist(pt, a, b):
    ax, ay = a; bx, by = b; px, py = pt
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = 0 if l2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / l2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy)), t


def main(path):
    page = pymupdf.open(path)[0]
    words = page.get_text("words")
    drawings = page.get_drawings()

    # 1. Nét ống dày, snap đầu mút
    segs = []
    for d in drawings:
        if (d.get("width") or 0) < 2.0:
            continue
        for it in d["items"]:
            if it[0] == "l":
                p1, p2 = (it[1].x, it[1].y), (it[2].x, it[2].y)
                ax = axis_of(p1, p2)
                if ax:
                    segs.append([p1, p2, ax])
    pts = []
    def snap(p):
        for q in pts:
            if math.dist(p, q) <= SNAP:
                return q
        pts.append(p)
        return p
    for s in segs:
        s[0], s[1] = snap(s[0]), snap(s[1])
    # Nối khe tại ký hiệu phụ kiện: đầu hở tới đỉnh gần nhất trên cùng một trục isometric, khe ngắn.
    def degrees():
        d = defaultdict(int)
        for s in segs:
            d[s[0]] += 1; d[s[1]] += 1
        return d
    deg = degrees()
    bridged = 0
    for _ in range(3):
        ends = [p for p in pts if deg[p] == 1]
        for p in ends:
            if deg[p] != 1:
                continue
            seg = next(s for s in segs if p in (s[0], s[1]))
            other = seg[1] if seg[0] == p else seg[0]
            best, bd = None, 40.0
            for q in pts:
                if q == p or q in (seg[0], seg[1]):
                    continue
                ax = axis_of(p, q)
                d = math.dist(p, q)
                # Khe tại ký hiệu phụ kiện nằm trên đường kéo dài của nét, cùng trục và cùng chiều đi tới.
                forward = (q[0] - p[0]) * (p[0] - other[0]) + (q[1] - p[1]) * (p[1] - other[1]) > 0
                if ax == seg[2] and forward and d < bd:
                    best, bd = q, d
            if best is not None:
                segs.append([p, best, axis_of(p, best)])
                deg = degrees()
                bridged += 1
    print(f"nối khe tại phụ kiện: {bridged}")
    print(f"nét ống: {len(segs)}, đỉnh: {len(pts)}, đầu hở: {[p for p in pts if deg[p] == 1]}")

    # 2. Neo toạ độ: nhóm E/N/EL + điểm trên hình gần chữ nhất (đầu hở hoặc đỉnh gần nhất)
    anchors = []
    for i, w in enumerate(words):
        if w[4] == "E" and i + 5 < len(words) and words[i + 2][4] == "N" and words[i + 4][4] == "EL":
            try:
                e, n, el = int(words[i + 1][4]), int(words[i + 3][4]), int(words[i + 5][4])
            except ValueError:
                continue
            c = ((w[0] + w[2]) / 2, (w[1] + w[3]) / 2)
            anchors.append({"E": e, "N": n, "EL": el, "text_at": c})
    # neo gắn với đầu hở gần nhất của mạng ống
    open_ends = [p for p in pts if deg[p] == 1]
    for a in anchors:
        a["node"] = min(open_ends, key=lambda p: math.dist(p, a["text_at"]))
        a["dist"] = round(math.dist(a["node"], a["text_at"]))
    print("neo:", [(a["E"], a["N"], a["EL"], "đỉnh", tuple(round(v) for v in a["node"]), "cách chữ", a["dist"]) for a in anchors])

    # 3. Kích thước. Số viết xoay theo trục thì trục là hướng chữ. Số viết ngang thì có đường chỉ nghiêng trỏ tới đường
    # kích thước; đi theo đường chỉ tới đầu kia, tìm đường kích thước nằm trên trục isometric đi qua đó.
    thin = []
    for d in drawings:
        if (d.get("width") or 0) < 1.5:
            for it in d["items"]:
                if it[0] == "l":
                    p1, p2 = (it[1].x, it[1].y), (it[2].x, it[2].y)
                    if math.dist(p1, p2) > 3:
                        thin.append((p1, p2, axis_of(p1, p2)))
    iso_thin = [t for t in thin if t[2]]
    anchor_values = {v for a in anchors for v in (a["E"], a["N"], a["EL"])}
    spans = []
    for b in page.get_text("dict")["blocks"]:
        for l in b.get("lines", []):
            dvec = l["dir"]
            ang = math.degrees(math.atan2(-dvec[1], dvec[0])) % 180
            for sp in l["spans"]:
                t = sp["text"].strip()
                if re.match(r"^\d{1,5}$", t) and int(t) not in anchor_values and sp["size"] > 7:
                    bb = sp["bbox"]
                    if bb[0] > page.rect.width * 0.66 or bb[1] > page.rect.height * 0.82:
                        continue
                    spans.append((int(t), ((bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2), ang, bb))
    dims = []
    unresolved = []
    for value, c, ang, bb in spans:
        axis = None
        dline = None
        text_axis = next((n for deg, n in AXES.items() if abs(ang - deg) <= 4), None)
        if text_axis:
            axis = text_axis
            near = [t for t in iso_thin if t[2] == axis and seg_dist(c, t[0], t[1])[0] <= 14]
            dline = min(near, key=lambda t: seg_dist(c, t[0], t[1])[0]) if near else None
        else:
            # đường chỉ: nét mỏng không trên trục, một đầu cách hộp chữ không quá 10 pt
            def near_box(pt):
                return bb[0] - 10 <= pt[0] <= bb[2] + 10 and bb[1] - 10 <= pt[1] <= bb[3] + 10
            leaders = [(t, t[1] if near_box(t[0]) else t[0]) for t in thin if not t[2] and (near_box(t[0]) or near_box(t[1]))]
            for t, far in leaders:
                hits = [u for u in iso_thin if seg_dist(far, u[0], u[1])[0] <= 4]
                if hits:
                    dline = max(hits, key=lambda u: math.dist(u[0], u[1]))
                    axis = dline[2]
                    break
        if axis is None:
            unresolved.append((value, (round(c[0]), round(c[1]))))
            continue
        ref = ((dline[0][0] + dline[1][0]) / 2, (dline[0][1] + dline[1][1]) / 2) if dline else c
        cands = [sg for sg in segs if sg[2] == axis]
        seg = min(cands, key=lambda sg: seg_dist(ref, sg[0], sg[1])[0])
        sd, t = seg_dist(ref, seg[0], seg[1])
        if sd > 90:
            unresolved.append((value, (round(c[0]), round(c[1])), "xa nét ống", round(sd)))
            continue
        dims.append({"value": value, "axis": axis, "seg": seg, "offset": round(sd), "t": round(t, 2), "at": c})
    print("chưa gán:", unresolved)
    print(f"kích thước gán được: {len(dims)}")
    by_axis = defaultdict(list)
    for d in dims:
        by_axis[d["axis"]].append(d["value"])
    for ax in ("E", "N", "UP"):
        print(f"  trục {ax}: {sorted(by_axis[ax])}  tổng {sum(by_axis[ax])}")

    # 4. Đi từ neo đầu, cộng kích thước theo đoạn
    start = anchors[0]
    pos = {start["node"]: (start["E"], start["N"], start["EL"])}
    order = []
    visited = set()
    stack = [start["node"]]
    while stack:
        p = stack.pop()
        for s in segs:
            if id(s) in visited or p not in (s[0], s[1]):
                continue
            visited.add(id(s))
            q = s[1] if s[0] == p else s[0]
            ax = s[2]
            sgn = sign_along(ax, p, q)
            vals = [d["value"] for d in dims if d["seg"] is s]
            length = sum(vals)
            x, y, z = pos[p]
            if ax == "E": x += sgn * length
            elif ax == "N": y += sgn * length
            else: z += sgn * length
            pos[q] = (x, y, z)
            order.append((ax, "+" if sgn > 0 else "-", vals, (x, y, z)))
            stack.append(q)
    print("đường đi từ neo đầu:")
    for ax, sg, vals, p in order:
        print(f"   {sg}{ax:<3} {str(vals):<28} -> E {p[0]} N {p[1]} EL {p[2]}")
    print("kiểm tra khép kín tại các neo khác:")
    for a in anchors[1:]:
        got = pos.get(a["node"])
        if got is None:
            print("   neo", (a["E"], a["N"], a["EL"]), "không tới được")
            continue
        print(f"   neo E {a['E']} N {a['N']} EL {a['EL']}: suy ra E {got[0]} N {got[1]} EL {got[2]}  lệch ({got[0]-a['E']}, {got[1]-a['N']}, {got[2]-a['EL']}) mm")


if __name__ == "__main__":
    main(sys.argv[1])
