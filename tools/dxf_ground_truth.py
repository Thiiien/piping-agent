"""Số liệu đối chứng cho việc đánh giá thư viện DXF (việc S0-13).

Đọc một tệp DXF bằng ezdxf và in ra JSON cùng cấu trúc với công cụ tools/DxfLibEval.
Cách dùng: python tools/dxf_ground_truth.py <tệp.dxf> > truth.json
"""
import json
import sys
from collections import Counter

import ezdxf


def main(path: str) -> None:
    doc = ezdxf.readfile(path)
    msp = doc.modelspace()
    counts = Counter(e.dxftype() for e in msp)
    vertex_total = 0
    texts = []
    inserts = []
    handles = 0
    for e in msp:
        if e.dxf.handle:
            handles += 1
        kind = e.dxftype()
        if kind == "LWPOLYLINE":
            vertex_total += len(e)
        elif kind == "POLYLINE":
            vertex_total += len(e.vertices)
        elif kind == "TEXT":
            texts.append(e.dxf.text)
        elif kind == "MTEXT":
            texts.append(e.text)
        elif kind == "INSERT":
            inserts.append(e.dxf.name)
    result = {
        "library": "ezdxf " + ezdxf.__version__,
        "counts": dict(sorted(counts.items())),
        "total": sum(counts.values()),
        "vertexTotal": vertex_total,
        "texts": sorted(texts),
        "inserts": sorted(inserts),
        "layerCount": len(doc.layers),
        "entitiesWithHandle": handles,
    }
    json.dump(result, sys.stdout, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main(sys.argv[1])
