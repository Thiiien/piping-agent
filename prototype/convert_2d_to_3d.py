"""Prototype: 2D piping plan (DXF) -> 3D piping model.

Reads ONLY the DXF file. Rules used (fixed, rule-based, no AI in this version):
  * polylines on layer PIPE are pipe centerlines, one polyline per constant elevation
  * a text "LINE DNxxx EL+zzzz" on layer PIPE-TAG gives size and centerline elevation;
    it belongs to the pipe its leader touches, otherwise to the nearest pipe
  * runs at different elevations meeting at the same plan point are joined by a riser
  * direction changes become 90-degree long-radius elbows, 3-way joints become tees
  * VALVE_GATE blocks become valves on the pipe they sit on
  * circles on EQUIP are vertical tanks (height from "H=..."), rectangles are pumps
  * COLUMN blocks and lines on STRUCT are a pipe rack, top of steel from "TOS EL+..."
Outputs: data/model.json and data/model_3d.dxf (3D meshes for AutoCAD).
"""
import json
import math
import re
import time
from collections import defaultdict
from pathlib import Path

import ezdxf
import numpy as np

HERE = Path(__file__).parent
SRC = HERE / "data" / "piping_plan_2d.dxf"
OUT_JSON = HERE / "data" / "model.json"
OUT_DXF = HERE / "data" / "model_3d.dxf"
TOL = 1.0
OD = {50: 60.3, 80: 88.9, 100: 114.3, 150: 168.3, 200: 219.1, 250: 273.0, 300: 323.9}
PALETTE = ["#1FB5A9", "#F26B21", "#F2C230", "#9FB3C8", "#B48CF2", "#6FCF5F"]
TAG_RE = re.compile(r"^\s*([A-Z]+-\d+)\s+DN\s*(\d+)\s+EL\s*([+-]?\d+)\s*$")


def seg_dist(p, a, b):
    p, a, b = np.asarray(p, float), np.asarray(a, float), np.asarray(b, float)
    ab = b - a
    t = np.clip(np.dot(p - a, ab) / np.dot(ab, ab), 0, 1)
    return float(np.linalg.norm(p - (a + t * ab))), float(t)


def text_box_points(t):
    x, y = t["pos"]
    w, h = 0.62 * t["h"] * len(t["s"]), t["h"]
    return [(x + w * i / 8, y + h * j / 2) for i in range(9) for j in range(3)]


def read_drawing(path):
    doc = ezdxf.readfile(path)
    d = defaultdict(list)
    for e in doc.modelspace():
        layer, kind = e.dxf.layer, e.dxftype()
        if kind == "LWPOLYLINE":
            pts = [(round(p[0], 3), round(p[1], 3)) for p in e.get_points("xy")]
            d[(layer, "poly")].append({"pts": pts, "closed": bool(e.closed)})
        elif kind == "LINE":
            d[(layer, "line")].append((tuple(e.dxf.start)[:2], tuple(e.dxf.end)[:2]))
        elif kind == "CIRCLE":
            d[(layer, "circle")].append({"c": tuple(e.dxf.center)[:2], "r": e.dxf.radius})
        elif kind == "TEXT":
            d[(layer, "text")].append({"s": e.dxf.text, "pos": tuple(e.dxf.insert)[:2], "h": e.dxf.height})
        elif kind == "INSERT":
            d[(layer, "insert")].append({"name": e.dxf.name, "pos": tuple(e.dxf.insert)[:2],
                                         "rot": e.dxf.get("rotation", 0.0)})
    return d, len(doc.modelspace())


def convert(d):
    warnings = []

    # ---- 1. pipe runs and their tags ---------------------------------
    runs = [{"pts": list(p["pts"])} for p in d[("PIPE", "poly")]]
    tags = []
    for t in d[("PIPE-TAG", "text")]:
        m = TAG_RE.match(t["s"])
        if m:
            tags.append({**t, "line": m.group(1), "dn": int(m.group(2)), "el": float(m.group(3))})
        else:
            warnings.append(f"unreadable tag: {t['s']!r}")

    def nearest_run(pt):
        best = (1e18, None)
        for i, r in enumerate(runs):
            for a, b in zip(r["pts"], r["pts"][1:]):
                dist = seg_dist(pt, a, b)[0]
                if dist < best[0]:
                    best = (dist, i)
        return best

    used = set()
    for a, b in d[("PIPE-TAG", "line")]:                       # tags with a leader
        (da, ia), (db, ib) = nearest_run(a), nearest_run(b)
        on_pipe, free, run_i = (a, b, ia) if da <= db else (b, a, ib)
        if min(da, db) > TOL:
            warnings.append("leader does not touch a pipe")
            continue
        k = min((i for i in range(len(tags)) if i not in used),
                key=lambda i: min(math.dist(free, q) for q in text_box_points(tags[i])))
        used.add(k)
        runs[run_i].update(line=tags[k]["line"], dn=tags[k]["dn"], el=tags[k]["el"],
                           tag_by="leader", tag_pos=list(tags[k]["pos"]))
    for k, t in enumerate(tags):                               # tags placed next to the pipe
        if k in used:
            continue
        dist, run_i = min(nearest_run(q) for q in text_box_points(t))
        if "dn" in runs[run_i]:
            warnings.append(f"run already tagged, ignoring {t['s']!r}")
            continue
        runs[run_i].update(line=t["line"], dn=t["dn"], el=t["el"], tag_by="nearest",
                           tag_pos=list(t["pos"]))
    for r in runs:
        if "dn" not in r:
            raise SystemExit(f"pipe run without tag: {r['pts']}")
        r["plan_pts"] = [list(p) for p in r["pts"]]            # as drawn, before splitting

    # ---- 2. split runs where another run ends on them (tees / risers) -
    risers = {}
    for i, ri in enumerate(runs):
        for end in (ri["pts"][0], ri["pts"][-1]):
            for j, rj in enumerate(runs):
                if i == j:
                    continue
                hit = any(math.dist(end, v) < TOL for v in rj["pts"])
                if not hit:
                    for k, (a, b) in enumerate(zip(rj["pts"], rj["pts"][1:])):
                        dist, t = seg_dist(end, a, b)
                        if dist < TOL and 0 < t < 1:
                            rj["pts"].insert(k + 1, end)
                            hit = True
                            break
                if hit and rj["el"] != ri["el"]:
                    key = (round(end[0]), round(end[1]), min(ri["el"], rj["el"]), max(ri["el"], rj["el"]))
                    risers.setdefault(key, {"dn": ri["dn"], "line": ri["line"]})

    # ---- 3. 3D graph ---------------------------------------------------
    def nk(x, y, z):
        return (round(x), round(y), round(z))

    edges = []
    for r in runs:
        for a, b in zip(r["pts"], r["pts"][1:]):
            edges.append({"a": nk(*a, r["el"]), "b": nk(*b, r["el"]), "dn": r["dn"], "line": r["line"],
                          "kind": "run"})
    for (x, y, z0, z1), info in risers.items():
        edges.append({"a": nk(x, y, z0), "b": nk(x, y, z1), "dn": info["dn"], "line": info["line"],
                      "kind": "riser"})
    adj = defaultdict(list)
    for i, e in enumerate(edges):
        adj[e["a"]].append(i)
        adj[e["b"]].append(i)

    # ---- 4. equipment --------------------------------------------------
    tanks, pumps = [], []
    eq_text = d[("EQUIP-TAG", "text")]
    for c in d[("EQUIP", "circle")]:
        inside = [t["s"] for t in eq_text if math.dist(t["pos"], c["c"]) < c["r"]]
        h = next((float(m.group(1)) for s in inside if (m := re.match(r"H\s*=\s*(\d+)", s))), None)
        tag = next((s for s in inside if re.match(r"^[A-Z]+-\d+", s)), "?")
        if h is None:
            warnings.append(f"tank {tag}: no height, assumed 5000")
            h = 5000.0
        tanks.append({"tag": tag, "c": list(c["c"]), "r": c["r"], "h": h})
    for p in d[("EQUIP", "poly")]:
        if not p["closed"]:
            continue
        xs, ys = [q[0] for q in p["pts"]], [q[1] for q in p["pts"]]
        b = (min(xs), max(xs), min(ys), max(ys))
        tag = next((t["s"] for t in eq_text if b[0] < t["pos"][0] < b[1] and b[2] < t["pos"][1] < b[3]), "?")
        pumps.append({"tag": tag, "bounds": list(b), "nozzles": []})

    def equipment_at(x, y):
        for t in tanks:
            if abs(math.dist((x, y), t["c"]) - t["r"]) < 5:
                return t["tag"], None
        for p in pumps:
            x0, x1, y0, y1 = p["bounds"]
            on_x = (abs(x - x0) < 5 or abs(x - x1) < 5) and y0 - 5 <= y <= y1 + 5
            on_y = (abs(y - y0) < 5 or abs(y - y1) < 5) and x0 - 5 <= x <= x1 + 5
            if on_x or on_y:
                return p["tag"], p
        return None, None

    # ---- 5. fittings ---------------------------------------------------
    nodes = []
    trim = defaultdict(float)                                   # (edge index, node) -> length cut for elbow
    for n, idx in adj.items():
        dirs = []
        for i in idx:
            other = edges[i]["b"] if edges[i]["a"] == n else edges[i]["a"]
            v = np.subtract(other, n).astype(float)
            dirs.append((v / np.linalg.norm(v)).tolist())
        dn = max(edges[i]["dn"] for i in idx)
        node = {"p": list(n), "dn": dn, "od": OD[dn], "line": edges[idx[0]]["line"], "dirs": dirs}
        if len(idx) == 1:
            tag, pump = equipment_at(n[0], n[1])
            node["kind"] = "nozzle" if tag else "open_end"
            if tag:
                node["equip"] = tag
            if pump is not None:
                pump["nozzles"].append({"p": list(n), "od": OD[dn], "dir": dirs[0]})
        elif len(idx) == 2:
            if abs(np.dot(dirs[0], dirs[1]) + 1) < 1e-6:
                node["kind"] = "inline"
            else:
                node["kind"] = "elbow"
                node["r"] = 1.5 * dn
                for i in idx:
                    trim[(i, n)] = node["r"]
        else:
            node["kind"] = "tee" if len(idx) == 3 else "cross"
        nodes.append(node)
    for i, e in enumerate(edges):
        e["trim_a"], e["trim_b"] = trim[(i, e["a"])], trim[(i, e["b"])]
        e["od"] = OD[e["dn"]]
        e["length"] = math.dist(e["a"], e["b"])
        if e["trim_a"] + e["trim_b"] > e["length"] - 1:
            warnings.append(f"segment too short for elbows on {e['line']}")
        e["a"], e["b"] = list(e["a"]), list(e["b"])

    for p in pumps:                                             # shaft level and casing position
        x0, x1, y0, y1 = p["bounds"]
        horiz = (x1 - x0) >= (y1 - y0)
        k = 0 if horiz else 1                                   # shaft axis index
        p["cl_z"] = p["nozzles"][0]["p"][2] if p["nozzles"] else 600.0
        mid = [(x0 + x1) / 2, (y0 + y1) / 2]
        lo, hi = (x0, x1) if horiz else (y0, y1)
        axial = next((z for z in p["nozzles"] if abs(z["dir"][k]) > 0.5), None)
        side = next((z for z in p["nozzles"] if abs(z["dir"][k]) <= 0.5), None)
        end = axial["p"][k] if axial else lo
        p["casing_side"] = -1 if abs(end - lo) < abs(end - hi) else 1
        if side:
            pos = side["p"][k]
        else:
            pos = lo + 0.2 * (hi - lo) if p["casing_side"] < 0 else hi - 0.2 * (hi - lo)
        p["casing_center"] = [pos, mid[1], p["cl_z"]] if horiz else [mid[0], pos, p["cl_z"]]

    # ---- 6. valves -----------------------------------------------------
    valves = []
    for ins in d[("VALVE", "insert")]:
        best = None
        for e in edges:
            if e["a"][2] != e["b"][2]:
                continue
            dist, t = seg_dist(ins["pos"], e["a"][:2], e["b"][:2])
            if dist < 5 and (best is None or dist < best[0]):
                best = (dist, e)
        if best is None:
            warnings.append(f"valve at {ins['pos']} is not on a pipe")
            continue
        e = best[1]
        axis = (np.subtract(e["b"], e["a"]) / e["length"]).tolist()
        sym = [math.cos(math.radians(ins["rot"])), math.sin(math.radians(ins["rot"]))]
        if abs(abs(np.dot(sym, axis[:2])) - 1) > 1e-3:
            warnings.append(f"valve symbol at {ins['pos']} is not aligned with its pipe")
        valves.append({"p": [ins["pos"][0], ins["pos"][1], e["a"][2]], "axis": axis, "dn": e["dn"],
                       "od": e["od"], "line": e["line"], "type": ins["name"]})

    # ---- 7. pipe rack --------------------------------------------------
    tos = next((float(m.group(1)) for t in d[("STRUCT-TAG", "text")]
                if (m := re.search(r"TOS\s+EL\s*\+?(\d+)", t["s"]))), None)
    columns = [list(i["pos"]) for i in d[("STRUCT", "insert")] if i["name"] == "COLUMN"]
    beams = [[list(a), list(b)] for a, b in d[("STRUCT", "line")]]
    if (columns or beams) and tos is None:
        warnings.append("pipe rack without TOS elevation, assumed 4000")
        tos = 4000.0

    # ---- 8. checks and summary -----------------------------------------
    riser_xy = {(k[0], k[1]) for k in risers}
    sym = d[("PIPE-RISER", "circle")]
    matched = sum(1 for c in sym if (round(c["c"][0]), round(c["c"][1])) in riser_xy)
    groups = sorted({e["line"][:5] for e in edges})
    colors = {g: PALETTE[i % len(PALETTE)] for i, g in enumerate(groups)}
    for e in edges:
        e["color"] = colors[e["line"][:5]]
    for n in nodes:
        n["color"] = colors[n["line"][:5]]
    length_by_dn = defaultdict(float)
    for e in edges:
        length_by_dn[e["dn"]] += e["length"] / 1000
    kinds = defaultdict(int)
    for n in nodes:
        kinds[n["kind"]] += 1
    for r in runs:
        r["pts"] = [list(p) for p in r["pts"]]
    stats = {
        "pipe_runs": len(runs), "pipe_segments": len(edges), "tags_read": len(tags),
        "lines": sorted({e["line"] for e in edges}),
        "risers": len(risers), "riser_symbols_matched": f"{matched}/{len(sym)}",
        "elbows": kinds["elbow"], "tees": kinds["tee"], "nozzles": kinds["nozzle"],
        "open_ends": kinds["open_end"], "valves": len(valves),
        "tanks": len(tanks), "pumps": len(pumps), "columns": len(columns), "beams": len(beams),
        "pipe_length_m": round(sum(length_by_dn.values()), 1),
        "pipe_length_by_dn_m": {f"DN{k}": round(v, 1) for k, v in sorted(length_by_dn.items())},
        "warnings": warnings,
    }
    return {"units": "mm", "source": SRC.name, "runs": runs, "pipes": edges, "nodes": nodes,
            "valves": valves, "tanks": tanks, "pumps": pumps, "columns": columns, "beams": beams,
            "tos": tos, "line_colors": colors, "stats": stats}


def export_dxf(model, path):
    import geometry as g
    from ezdxf.colors import rgb2int
    from ezdxf.math import Vec3
    from ezdxf.render import MeshBuilder

    doc = ezdxf.new("R2010", setup=True)
    doc.units = ezdxf.units.MM
    msp = doc.modelspace()

    def hex_rgb(h):
        return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))

    def add(mesh, layer, color):
        if layer not in doc.layers:
            doc.layers.add(layer, true_color=rgb2int(hex_rgb(color)))
        tri = mesh.triangulate().clean()
        mb = MeshBuilder()
        mb.vertices = [Vec3(*map(float, q)) for q in tri.points]
        mb.faces = [tuple(int(i) for i in f) for f in tri.faces.reshape(-1, 4)[:, 1:]]
        mb.render_polyface(msp, dxfattribs={"layer": layer})

    for e in model["pipes"]:
        a, b = np.array(e["a"], float), np.array(e["b"], float)
        u = (b - a) / np.linalg.norm(b - a)
        add(g.pipe(a + u * e["trim_a"], b - u * e["trim_b"], e["od"]), "3D-PIPE-" + e["line"], e["color"])
    for n in model["nodes"]:
        layer = "3D-PIPE-" + n["line"]
        if n["kind"] == "elbow":
            add(g.elbow(n["p"], n["dirs"][0], n["dirs"][1], n["r"], n["od"]), layer, n["color"])
        elif n["kind"] == "nozzle":
            add(g.flange(n["p"], n["dirs"][0], n["od"]), layer, n["color"])
    for v in model["valves"]:
        body, wheel = g.valve(v["p"], v["axis"], v["od"])
        add(body, "3D-VALVE", "#3A4658")
        add(wheel, "3D-VALVE-HANDWHEEL", "#D93B3B")
    for t in model["tanks"]:
        add(g.tank(t["c"][0], t["c"][1], t["r"], t["h"]), "3D-EQUIP-TANK", "#C9D3DE")
    for p in model["pumps"]:
        base, body = g.pump(p)
        add(base, "3D-EQUIP-BASE", "#6B7A8C")
        add(body, "3D-EQUIP-PUMP", "#2F7FD1")
    for c in model["columns"]:
        add(g.column(c[0], c[1], model["tos"]), "3D-STRUCT", "#7C8A9A")
    for a, b in model["beams"]:
        add(g.beam(a, b, model["tos"]), "3D-STRUCT", "#7C8A9A")
    doc.saveas(path)
    return len(msp)


if __name__ == "__main__":
    t0 = time.perf_counter()
    drawing, n_entities = read_drawing(SRC)
    t1 = time.perf_counter()
    model = convert(drawing)
    t2 = time.perf_counter()
    n3d = export_dxf(model, OUT_DXF)
    t3 = time.perf_counter()
    model["stats"]["entities_2d"] = n_entities
    model["stats"]["entities_3d"] = n3d
    model["timing_ms"] = {"read_dxf": round((t1 - t0) * 1000, 1), "convert": round((t2 - t1) * 1000, 1),
                          "export_3d_dxf": round((t3 - t2) * 1000, 1), "total": round((t3 - t0) * 1000, 1)}
    OUT_JSON.write_text(json.dumps(model, indent=1), encoding="utf-8")
    print(json.dumps(model["stats"], indent=1))
    print(json.dumps(model["timing_ms"]))
