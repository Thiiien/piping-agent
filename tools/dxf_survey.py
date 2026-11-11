"""Khảo sát một tệp DXF cho việc S0-12: layer, loại đối tượng, block, chữ, đơn vị.

Chạy: .venv/Scripts/python tools/dxf_survey.py <tệp.dxf> [--texts N]
Chỉ in thống kê và mẫu chữ. Không ghi gì ra đĩa.
"""
import argparse
import re
import sys
from collections import Counter, defaultdict

import ezdxf


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("path")
    parser.add_argument("--texts", type=int, default=60, help="số mẫu chữ in ra")
    args = parser.parse_args()

    doc = ezdxf.readfile(args.path)
    msp = doc.modelspace()
    hdr = doc.header

    print(f"== Tệp: {args.path}")
    print(f"Phiên bản DXF: {doc.dxfversion}  INSUNITS={hdr.get('$INSUNITS')}  "
          f"MEASUREMENT={hdr.get('$MEASUREMENT')}  LUNITS={hdr.get('$LUNITS')}  "
          f"DIMSCALE={hdr.get('$DIMSCALE')}  LTSCALE={hdr.get('$LTSCALE')}")
    ext = (hdr.get("$EXTMIN"), hdr.get("$EXTMAX"))
    print(f"EXTMIN={ext[0]}  EXTMAX={ext[1]}")

    layouts = [l.name for l in doc.layouts]
    print(f"Layout: {layouts}")
    for name in layouts:
        if name == "Model":
            continue
        ps = doc.layouts.get(name)
        print(f"  Không gian giấy '{name}': {len(ps)} đối tượng, "
              f"viewport: {sum(1 for e in ps if e.dxftype() == 'VIEWPORT')}")

    print(f"\n== Không gian mô hình: {len(msp)} đối tượng")
    kinds = Counter(e.dxftype() for e in msp)
    for k, v in kinds.most_common():
        print(f"  {k:18s} {v}")

    print("\n== Layer có đối tượng trong không gian mô hình (layer: tổng, theo loại)")
    by_layer: dict[str, Counter] = defaultdict(Counter)
    for e in msp:
        by_layer[e.dxf.layer][e.dxftype()] += 1
    for layer, c in sorted(by_layer.items(), key=lambda kv: -sum(kv[1].values())):
        top = ", ".join(f"{k} {v}" for k, v in c.most_common(5))
        lay = doc.layers.get(layer) if layer in doc.layers else None
        color = lay.color if lay else "?"
        print(f"  {layer:28s} {sum(c.values()):6d}  màu {color!s:>4}  | {top}")
    print(f"Tổng số layer định nghĩa: {len(doc.layers)}, có đối tượng: {len(by_layer)}")

    print("\n== Block được chèn (tên: số lần chèn, số đối tượng bên trong, có ATTRIB, có block lồng)")
    inserts = Counter()
    attribs_per_block: dict[str, Counter] = defaultdict(Counter)
    for e in msp.query("INSERT"):
        inserts[e.dxf.name] += 1
        for a in e.attribs:
            attribs_per_block[e.dxf.name][a.dxf.tag] += 1
    for name, n in inserts.most_common():
        blk = doc.blocks.get(name)
        inner = Counter(x.dxftype() for x in blk) if blk else Counter()
        nested = inner.get("INSERT", 0)
        attdef = inner.get("ATTDEF", 0)
        tags = ",".join(sorted(attribs_per_block[name])) if attribs_per_block[name] else "-"
        print(f"  {name:32s} x{n:4d}  trong: {sum(inner.values()):4d} đối tượng, "
              f"ATTDEF {attdef}, lồng {nested}, thẻ ATTRIB: {tags}")
    print(f"Tổng định nghĩa block: {len(doc.blocks)}, được chèn trong mô hình: {len(inserts)}, "
          f"lần chèn: {sum(inserts.values())}")

    xrefs = [b.name for b in doc.blocks if b.block.is_xref]
    print(f"Xref: {xrefs or 'không'}")

    print("\n== Chữ trong không gian mô hình")
    texts = []
    for e in msp:
        t = e.dxftype()
        if t == "TEXT":
            texts.append((e.dxf.layer, e.dxf.text, e.dxf.height))
        elif t == "MTEXT":
            texts.append((e.dxf.layer, e.plain_text().replace("\n", " / "), e.dxf.char_height))
    print(f"Số chữ: {len(texts)}")
    heights = Counter(round(h, 1) for _, _, h in texts)
    print(f"Chiều cao chữ phổ biến: {heights.most_common(8)}")

    pat_tag = re.compile(r"^\s*[A-Z0-9]{1,6}-[A-Z0-9]{1,6}-\d{2,4}", re.I)
    pat_size = re.compile(r"(\d+(\.\d+)?\"|DN\s?\d+|NPS\s?\d+|\d+\s?NB|\d+/\d+\")", re.I)
    pat_el = re.compile(r"(EL\.?\s*[+-]?\s*\d|BOP|TOP|TOS|C\.?L\.?)", re.I)
    n_tag = sum(1 for _, s, _ in texts if pat_tag.search(s))
    n_size = sum(1 for _, s, _ in texts if pat_size.search(s))
    n_el = sum(1 for _, s, _ in texts if pat_el.search(s))
    print(f"Chữ giống mã tuyến: {n_tag}, có kích cỡ ống: {n_size}, có cao độ: {n_el}")

    print(f"\nMẫu chữ ({min(args.texts, len(texts))} đầu tiên theo layer):")
    for layer, s, h in sorted(texts, key=lambda x: (x[0], x[1]))[: args.texts]:
        print(f"  [{layer}] h={h:.1f}  {s[:90]}")

    print("\n== Đường dẫn và kích thước")
    print(f"  LEADER {kinds.get('LEADER', 0)}, MULTILEADER {kinds.get('MULTILEADER', 0)}, "
          f"DIMENSION {kinds.get('DIMENSION', 0)}, HATCH {kinds.get('HATCH', 0)}")

    print("\n== Kiểu đường của LINE/LWPOLYLINE (linetype: số)")
    lt = Counter()
    for e in msp.query("LINE LWPOLYLINE POLYLINE"):
        name = e.dxf.linetype
        if name == "BYLAYER":
            lay = doc.layers.get(e.dxf.layer) if e.dxf.layer in doc.layers else None
            name = f"BYLAYER({lay.dxf.linetype if lay else '?'})"
        lt[name] += 1
    for k, v in lt.most_common(10):
        print(f"  {k:28s} {v}")

    print("\n== Đối tượng proxy hoặc lạ")
    odd = Counter(e.dxftype() for e in msp if e.dxftype() in ("ACAD_PROXY_ENTITY", "UNKNOWN") or e.dxftype().startswith("AEC"))
    print(f"  {dict(odd) or 'không'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
