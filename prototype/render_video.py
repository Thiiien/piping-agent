"""Render the demo video from the real prototype data.

Inputs : data/piping_plan_2d.dxf (the 2D drawing) and data/model.json (converter output).
Output : ../output/demo_2d_to_3d.mp4 plus still images for the pitch deck.

Usage:
  python render_video.py                 full video
  python render_video.py --still 12.5    one frame at t=12.5 s -> ../output/stills/t_12.50.png
  python render_video.py --deck-stills   clean images used in the slides
"""
import heapq
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

import ezdxf
import imageio_ffmpeg
import numpy as np
import pyvista as pv
from PIL import Image, ImageDraw, ImageFont

import geometry as g

HERE = Path(__file__).parent
OUT = HERE.parent / "output"
STILLS = OUT / "stills"
W, H, FPS = 1920, 1080, 30
DUR = 56.0

# ---- palette -------------------------------------------------------------
BG, BG_TOP, SHEET = "#0A121D", "#111F33", "#101F33"
ORANGE, CYAN, WHITE, MUTED = "#F26B21", "#35D6E8", "#F4F7FB", "#93A4BA"
LAYER_COLOR = {4: "#35D6E8", 3: "#7EE081", 6: "#E77BE0", 2: "#F4D35E", 8: "#8795A8", 7: "#DCE6F2"}
FONT_DIR = Path("C:/Windows/Fonts")


def font(size, weight="regular"):
    name = {"regular": "segoeui.ttf", "semibold": "seguisb.ttf", "bold": "segoeuib.ttf"}[weight]
    return ImageFont.truetype(str(FONT_DIR / name), size)


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def lerp(a, b, t):
    return a + (b - a) * t


def clamp01(x):
    return max(0.0, min(1.0, x))


def smooth(x):
    x = clamp01(x)
    return x * x * (3 - 2 * x)


def ease_out(x):
    x = clamp01(x)
    return 1 - (1 - x) ** 3


def ramp(t, t0, dur):
    return clamp01((t - t0) / dur)


def mix_hex(a, b, t):
    ra, rb = hex_rgb(a), hex_rgb(b)
    return tuple(lerp(ra[i], rb[i], t) / 255 for i in range(3))


# ---- timeline (seconds) --------------------------------------------------
T_TITLE_END = 3.5
T_DETECT = 9.0
T_TILT = 19.0
T_EQUIP = 21.3
T_PIPES = 24.0
PIPE_BUILD = 9.3
T_RESULT = 34.0
T_ORBIT = 36.5
T_END = 50.8

model = json.loads((HERE / "data" / "model.json").read_text(encoding="utf-8"))
stats = model["stats"]

# ---- read the 2D drawing for display -------------------------------------
doc = ezdxf.readfile(HERE / "data" / "piping_plan_2d.dxf")
layer_color = {l.dxf.name: LAYER_COLOR.get(l.dxf.color, "#DCE6F2") for l in doc.layers}
polys, texts = defaultdict(list), defaultdict(list)


def take(e, layer):
    kind = e.dxftype()
    if kind == "LWPOLYLINE":
        pts = [(p[0], p[1]) for p in e.get_points("xy")]
        if e.closed:
            pts.append(pts[0])
        polys[layer].append(pts)
    elif kind == "LINE":
        polys[layer].append([tuple(e.dxf.start)[:2], tuple(e.dxf.end)[:2]])
    elif kind == "CIRCLE":
        c, r = e.dxf.center, e.dxf.radius
        polys[layer].append([(c[0] + r * math.cos(a), c[1] + r * math.sin(a))
                             for a in np.linspace(0, 2 * math.pi, 73)])
    elif kind == "TEXT":
        texts[layer].append((e.dxf.text, tuple(e.dxf.insert)[:2], e.dxf.height))


for ent in doc.modelspace():
    if ent.dxftype() == "INSERT":
        for sub in ent.virtual_entities():
            take(sub, ent.dxf.layer)
    else:
        take(ent, ent.dxf.layer)


def lines_mesh(polylines, z):
    pts, cells, off = [], [], 0
    for pl in polylines:
        n = len(pl)
        pts.extend((x, y, z) for x, y in pl)
        cells.append([n] + list(range(off, off + n)))
        off += n
    m = pv.PolyData(np.array(pts, float))
    m.lines = np.hstack(cells)
    return m


def text_mesh(s, pos, h, z):
    m = pv.Text3D(s, depth=0.5, height=h)
    b = m.bounds
    return m.translate((pos[0] - b[0], pos[1] - b[2], z - b[4]), inplace=False)


# ---- scene ---------------------------------------------------------------
pv.OFF_SCREEN = True
pl = pv.Plotter(off_screen=True, window_size=(W, H), lighting="three lights")
pl.set_background(BG, top=BG_TOP)
SHEET_BOUNDS = (-1500, 31500, -3500, 22000)
SHEET_C = ((SHEET_BOUNDS[0] + SHEET_BOUNDS[1]) / 2, (SHEET_BOUNDS[2] + SHEET_BOUNDS[3]) / 2)
sheet = pv.Plane(center=(SHEET_C[0], SHEET_C[1], -8), direction=(0, 0, 1),
                 i_size=SHEET_BOUNDS[1] - SHEET_BOUNDS[0], j_size=SHEET_BOUNDS[3] - SHEET_BOUNDS[2])
pl.add_mesh(sheet, color=SHEET, lighting=False)

drawing_actors = []          # (actor, base color hex)
tag_boxes = []
for layer in sorted(set(polys) | set(texts)):
    color = layer_color.get(layer, "#DCE6F2")
    if polys[layer]:
        a = pl.add_mesh(lines_mesh(polys[layer], 2.0), color=color, lighting=False,
                        line_width=4.0 if layer == "PIPE" else 2.2)
        drawing_actors.append((a, color))
    if texts[layer]:
        meshes = [text_mesh(s, pos, h, 2.0) for s, pos, h in texts[layer]]
        if layer == "PIPE-TAG":
            tag_boxes = [m.bounds for m in meshes]
        a = pl.add_mesh(pv.merge(meshes), color=color, lighting=False)
        drawing_actors.append((a, color))

# highlight overlays for the "detection" step: (actor, appear time)
highlights = []


def add_highlight(mesh, color, t, **kw):
    a = pl.add_mesh(mesh, color=color, lighting=False, **kw)
    a.SetVisibility(False)
    highlights.append((a, t))


runs = model["runs"]
for i, r in enumerate(runs):
    tube = lines_mesh([[tuple(p) for p in r["plan_pts"]]], 12.0).tube(radius=75, n_sides=10)
    add_highlight(tube, ORANGE, T_DETECT + 0.4 + i * 0.17)
for i, b in enumerate(sorted(tag_boxes, key=lambda b: (-b[3], b[0]))):
    m = 110
    rect = [(b[0] - m, b[2] - m), (b[1] + m, b[2] - m), (b[1] + m, b[3] + m), (b[0] - m, b[3] + m),
            (b[0] - m, b[2] - m)]
    add_highlight(lines_mesh([rect], 14.0), "#B9F5BB", T_DETECT + 2.9 + i * 0.13, line_width=2.5)
for i, v in enumerate(model["valves"]):
    ring = [(v["p"][0] + 430 * math.cos(a), v["p"][1] + 430 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 49)]
    add_highlight(lines_mesh([ring], 14.0).tube(radius=38, n_sides=8), "#F6A8F1", T_DETECT + 5.0 + i * 0.16)
for i, tk in enumerate(model["tanks"]):
    ring = [(tk["c"][0] + tk["r"] * math.cos(a), tk["c"][1] + tk["r"] * math.sin(a))
            for a in np.linspace(0, 2 * math.pi, 97)]
    add_highlight(lines_mesh([ring], 14.0).tube(radius=65, n_sides=8), "#F4D35E", T_DETECT + 6.7 + i * 0.25)
for i, pm in enumerate(model["pumps"]):
    x0, x1, y0, y1 = pm["bounds"]
    add_highlight(lines_mesh([[(x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0)]], 14.0).tube(radius=65, n_sides=8),
                  "#F4D35E", T_DETECT + 7.3 + i * 0.25)
for i, c in enumerate(model["columns"]):
    s = 330
    sq = [(c[0] - s, c[1] - s), (c[0] + s, c[1] - s), (c[0] + s, c[1] + s), (c[0] - s, c[1] + s), (c[0] - s, c[1] - s)]
    add_highlight(lines_mesh([sq], 14.0).tube(radius=45, n_sides=8), "#C8D2DF", T_DETECT + 8.1 + i * 0.07)

# ---- 3D components with their animation ---------------------------------
MAT = dict(smooth_shading=True, split_sharp_edges=True, specular=0.35, specular_power=18,
           ambient=0.22, diffuse=0.85)
anims = []                   # dicts: actor, kind (grow|pop|rise), t0, dur, plus parameters


def add3d(mesh, color, **kw):
    a = pl.add_mesh(mesh, color=color, **{**MAT, **kw})
    a.SetVisibility(False)
    return a


for i, tk in enumerate(model["tanks"]):
    cx, cy, r, h = tk["c"][0], tk["c"][1], tk["r"], tk["h"]
    shell = pv.Cylinder(center=(cx, cy, h / 2), direction=(0, 0, 1), radius=r, height=h, resolution=120, capping=False)
    roof = pv.Cone(center=(cx, cy, h + r * 0.08), direction=(0, 0, 1), height=r * 0.16, radius=r, resolution=120, capping=False)
    anims.append(dict(actor=add3d(shell, "#CAD4DF", split_sharp_edges=False), kind="rise", t0=T_EQUIP + 0.25 * i, dur=1.5))
    anims.append(dict(actor=add3d(roof, "#B9C5D2", smooth_shading=False, split_sharp_edges=False), kind="rise",
                      t0=T_EQUIP + 0.25 * i, dur=1.5))
for i, pm in enumerate(model["pumps"]):
    base, body = g.pump(pm)
    anims.append(dict(actor=add3d(base, "#6B7A8C"), kind="rise", t0=T_EQUIP + 0.6 + 0.2 * i, dur=1.2))
    anims.append(dict(actor=add3d(body, "#2F7FD1"), kind="rise", t0=T_EQUIP + 0.6 + 0.2 * i, dur=1.2))
for i, c in enumerate(sorted(model["columns"], key=lambda c: (c[1], c[0]))):
    anims.append(dict(actor=add3d(g.column(c[0], c[1], model["tos"]), "#7C8A9A"),
                      kind="rise", t0=T_EQUIP + 0.9 + 0.08 * i, dur=1.2))
for i, (a, b) in enumerate(model["beams"]):
    anims.append(dict(actor=add3d(g.beam(a, b, model["tos"]), "#7C8A9A"),
                      kind="rise", t0=T_EQUIP + 1.1 + 0.05 * i, dur=1.2))

# build order of the piping: fronts travel along each connected line system
key = lambda p: tuple(int(round(v)) for v in p)
pipes = model["pipes"]
adj = defaultdict(list)
for i, e in enumerate(pipes):
    adj[key(e["a"])].append((key(e["b"]), i))
    adj[key(e["b"])].append((key(e["a"]), i))
node_info = {key(n["p"]): n for n in model["nodes"]}
seen, components = set(), []
for start in adj:
    if start in seen:
        continue
    comp, stack = [], [start]
    seen.add(start)
    while stack:
        n = stack.pop()
        comp.append(n)
        for m, _ in adj[n]:
            if m not in seen:
                seen.add(m)
                stack.append(m)
    components.append(comp)
components.sort(key=lambda comp: min(node_info[n]["line"] for n in comp))
dist = {}
for comp in components:
    ends = [n for n in comp if len(adj[n]) == 1]
    zmin = min(n[2] for n in ends)
    low = [n for n in ends if n[2] == zmin]
    xmin = min(n[0] for n in low)
    heap = [(0.0, n) for n in low if n[0] == xmin]
    local = {}
    while heap:
        d0, n = heapq.heappop(heap)
        if n in local:
            continue
        local[n] = d0
        for m, i in adj[n]:
            if m not in local:
                heapq.heappush(heap, (d0 + pipes[i]["length"], m))
    dist.update(local)
max_d = max(dist.values())
STAGGER = 1.05
speed = max_d / (PIPE_BUILD - STAGGER * (len(components) - 1) - 0.3)
comp_of = {n: ci for ci, comp in enumerate(components) for n in comp}


def arrive(n):
    return T_PIPES + STAGGER * comp_of[n] + dist[n] / speed


for e in pipes:
    a, b = np.array(e["a"], float), np.array(e["b"], float)
    ta, tb = arrive(key(e["a"])), arrive(key(e["b"]))
    if tb < ta:
        a, b, ta = b, a, tb
        trim0, trim1 = e["trim_b"], e["trim_a"]
    else:
        trim0, trim1 = e["trim_a"], e["trim_b"]
    u = (b - a) / np.linalg.norm(b - a)
    p0, p1 = a + u * trim0, b - u * trim1
    ex = np.cross(u, [0, 0, 1.0])
    if np.linalg.norm(ex) < 1e-6:
        ex = np.array([1.0, 0, 0])
    ex /= np.linalg.norm(ex)
    ey = np.cross(u, ex)
    unit_cyl = pv.Cylinder(center=(0, 0, 0.5), direction=(0, 0, 1), radius=e["od"] / 2, height=1.0,
                           resolution=28, capping=True)
    anims.append(dict(actor=add3d(unit_cyl, e["color"]), kind="grow", t0=ta + trim0 / speed,
                      dur=max(np.linalg.norm(p1 - p0) / speed, 1e-3), p0=p0, ex=ex, ey=ey, u=u,
                      length=float(np.linalg.norm(p1 - p0)), what="pipe"))
    for v in model["valves"]:
        vp = np.array(v["p"], float)
        if v["line"] == e["line"] and np.linalg.norm(np.cross(vp - a, u)) < 1 and 0 < np.dot(vp - a, u) < e["length"]:
            body, wheel = g.valve(v["p"], v["axis"], v["od"])
            tv = ta + np.dot(vp - a, u) / speed
            anims.append(dict(actor=add3d(body, "#C3CCD8"), kind="pop", t0=tv, dur=0.35, c=vp, what="valve"))
            anims.append(dict(actor=add3d(wheel, "#E5484D"), kind="pop", t0=tv, dur=0.35, c=vp, what=None))
for n in model["nodes"]:
    t0 = arrive(key(n["p"]))
    c = np.array(n["p"], float)
    if n["kind"] == "elbow":
        mesh = g.elbow(n["p"], n["dirs"][0], n["dirs"][1], n["r"], n["od"])
        anims.append(dict(actor=add3d(mesh, n["color"]), kind="pop", t0=t0 - n["r"] / speed, dur=0.3, c=c, what="elbow"))
    elif n["kind"] == "nozzle":
        mesh = g.flange(n["p"], n["dirs"][0], n["od"])
        anims.append(dict(actor=add3d(mesh, "#C3CCD8"), kind="pop", t0=t0, dur=0.3, c=c, what="flange"))
    elif n["kind"] == "tee":
        anims.append(dict(actor=None, kind="count", t0=t0, dur=0.1, what="tee"))
T_PIPES_DONE = max(a["t0"] + a["dur"] for a in anims if a["kind"] in ("grow", "pop"))
TOTALS = {k: sum(1 for a in anims if a.get("what") == k) for k in ("pipe", "elbow", "valve", "tee", "flange")}


def apply_anims(t):
    for a in anims:
        act = a["actor"]
        if act is None:
            continue
        s = (t - a["t0"]) / a["dur"]
        if s <= 0:
            act.SetVisibility(False)
            continue
        act.SetVisibility(True)
        M = np.eye(4)
        if a["kind"] == "grow":
            L = a["length"] * clamp01(s)
            M[:3, 0], M[:3, 1], M[:3, 2], M[:3, 3] = a["ex"], a["ey"], a["u"] * max(L, 1e-3), a["p0"]
        elif a["kind"] == "pop":
            k = ease_out(s)
            k = k + 0.18 * math.sin(math.pi * clamp01(s)) if s < 1 else 1.0
            M[:3, :3] *= k
            M[:3, 3] = a["c"] * (1 - k)
        else:  # rise
            M[2, 2] = max(ease_out(s), 1e-3)
        act.user_matrix = M


def counts(t):
    return {k: sum(1 for a in anims if a.get("what") == k and t >= a["t0"] + (a["dur"] if k == "pipe" else 0))
            for k in TOTALS}


# ---- camera --------------------------------------------------------------
VIEW_ANGLE = 24.0
SHEET_PX = (40, 108, 1178, 988)            # where the sheet sits on screen in the top view
vis_h = (SHEET_BOUNDS[3] - SHEET_BOUNDS[2]) * H / (SHEET_PX[3] - SHEET_PX[1])
D0 = vis_h / 2 / math.tan(math.radians(VIEW_ANGLE / 2))
WC0 = ((W / 2 - (SHEET_PX[0] + SHEET_PX[2]) / 2) / (W / 2), -(H / 2 - (SHEET_PX[1] + SHEET_PX[3]) / 2) / (H / 2))
F0 = np.array([SHEET_C[0], SHEET_C[1], 0.0])
F1 = np.array([14800.0, 10300.0, 2300.0])
WC1 = (0.25, 0.02)
#        t      elev   azim    dist    focal  window-centre
KEYS = [(0.0, 90.0, -90.0, D0, F0, WC0),
        (T_TILT, 90.0, -90.0, D0, F0, WC0),
        (T_PIPES - 0.3, 31.0, -63.0, 55000.0, F1, WC1),
        (T_ORBIT, 27.0, -38.0, 55000.0, F1, WC1),
        (T_END, 30.0, 128.0, 53000.0, F1, WC1),
        (DUR, 30.0, 140.0, 53000.0, F1, WC1)]


def camera_at(t):
    for (t0, *k0), (t1, *k1) in zip(KEYS, KEYS[1:]):
        if t <= t1:
            s = smooth((t - t0) / (t1 - t0)) if t1 > t0 else 1.0
            el, az, d = (lerp(k0[i], k1[i], s) for i in range(3))
            f = k0[3] + (k1[3] - k0[3]) * s
            wc = (lerp(k0[4][0], k1[4][0], s), lerp(k0[4][1], k1[4][1], s))
            break
    e, a = math.radians(el), math.radians(az)
    dirv = np.array([math.cos(e) * math.cos(a), math.cos(e) * math.sin(a), math.sin(e)])
    up = np.array([-math.sin(e) * math.cos(a), -math.sin(e) * math.sin(a), math.cos(e)])
    cam = pl.camera
    cam.position, cam.focal_point, cam.up = f + d * dirv, f, up
    cam.view_angle = VIEW_ANGLE
    cam.SetWindowCenter(*wc)
    pl.renderer.ResetCameraClippingRange()


def render_scene(t):
    camera_at(t)
    dim = 0.62 * smooth(ramp(t, T_TILT + 1.0, 3.0))
    for act, color in drawing_actors:
        act.prop.color = mix_hex(color, SHEET, dim)
    for act, t0 in highlights:
        act.SetVisibility(t0 <= t < T_TILT + 0.4)
    apply_anims(t)
    pl.render()
    return Image.fromarray(pl.screenshot(return_img=True)).convert("RGBA")


# ---- 2D overlay (titles, captions, panel) --------------------------------
F_STEP, F_TITLE, F_SUB = font(21, "bold"), font(36, "semibold"), font(22)
F_PANEL_T, F_ROW, F_VAL, F_SMALL = font(19, "bold"), font(23), font(25, "bold"), font(18)
vn = lambda x, nd=1: f"{x:.{nd}f}".replace(".", ",")
T_CONVERT_S = (model["timing_ms"]["read_dxf"] + model["timing_ms"]["convert"]) / 1000
T_EXPORT_S = model["timing_ms"]["export_3d_dxf"] / 1000

HEADERS = [
    (T_TITLE_END - 0.4, T_DETECT, "ĐẦU VÀO", "Bản vẽ mặt bằng đường ống 2D",
     f"File DXF: {model['source']}  ·  {stats['entities_2d']} đối tượng 2D  ·  đơn vị mm"),
    (T_DETECT, T_TILT, "BƯỚC 1", "Đọc bản vẽ và nhận diện đối tượng",
     "Đường tâm ống, nhãn đường kính và cao độ, ký hiệu van, thiết bị, kết cấu"),
    (T_TILT, T_RESULT, "BƯỚC 2", "Dựng mô hình 3D",
     "Mỗi tuyến ống được nâng lên đúng cao độ EL và dựng theo đường kính DN ghi trên bản vẽ"),
    (T_RESULT, T_END + 0.3, "KẾT QUẢ", "Mô hình đường ống 3D",
     f"Xuất ra file {Path('model_3d.dxf').name} gồm {stats['entities_3d']} khối 3D, chia layer theo từng tuyến ống"),
]


def fade_window(t, t0, t1, f=0.35):
    return clamp01(min((t - t0) / f, (t1 - t) / f))


def rgba(hexc, alpha):
    return hex_rgb(hexc) + (int(255 * clamp01(alpha)),)


PANEL_BG = "#0E1A2B"


def fade_to_bg(hexc, k):
    """Colour of a row that is fading in on top of the panel background."""
    a, b = hex_rgb(PANEL_BG), hex_rgb(hexc)
    return tuple(int(lerp(a[i], b[i], clamp01(k))) for i in range(3)) + (255,)


def box(d, x, y, w, h):
    d.rounded_rectangle((x, y, x + w, y + h), 16, fill=rgba(PANEL_BG, 0.90), outline=rgba("#2A3F5C", 1), width=2)


def panel(d, x, y, w, title, rows, footer=None):
    """rows: list of (dot color or None, label, value, visibility 0..1)."""
    h = 66 + 50 * len(rows) + (48 if footer else 14)
    box(d, x, y, w, h)
    d.text((x + 26, y + 22), title, font=F_PANEL_T, fill=rgba(CYAN, 1))
    yy = y + 66
    for dot, label, value, k in rows:
        if k > 0.02:
            if dot:
                d.ellipse((x + 26, yy + 11, x + 40, yy + 25), fill=fade_to_bg(dot, k))
            d.text((x + (54 if dot else 26), yy + 2), label, font=F_ROW, fill=fade_to_bg("#C9D5E3", k))
            tw = d.textlength(value, font=F_VAL)
            d.text((x + w - 26 - tw, yy), value, font=F_VAL, fill=fade_to_bg(WHITE, k))
        yy += 50
    if footer:
        d.text((x + 26, yy + 6), footer, font=F_SMALL, fill=rgba(MUTED, 1))
    return h


def count_up(t, t0, n, dur=1.2):
    return str(int(round(n * smooth(ramp(t, t0, dur)))))


def overlay(t):
    """All 2D graphics. Each group is drawn opaque on its own layer, then faded as a whole."""
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def group(alpha, draw_fn):
        nonlocal out
        if alpha <= 0:
            return
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        draw_fn(ImageDraw.Draw(layer))
        if alpha < 1:
            layer.putalpha(layer.getchannel("A").point(lambda v: int(v * alpha)))
        out = Image.alpha_composite(out, layer)

    for t0, t1, step, title, sub in HEADERS:
        def header(d, step=step, title=title, sub=sub):
            sw = d.textlength(step, font=F_STEP)
            d.rounded_rectangle((40, 22, 40 + sw + 28, 58), 8, fill=rgba(ORANGE, 1))
            d.text((54, 25), step, font=F_STEP, fill=(255, 255, 255, 255))
            d.text((40 + sw + 46, 12), title, font=F_TITLE, fill=rgba(WHITE, 1))
            d.text((40, 68), sub, font=F_SUB, fill=rgba(MUTED, 1))
        group(fade_window(t, t0, t1), header)

    PX, PW = 1228, 652

    def input_panels(d):
        n_layers = len({e.dxf.layer for e in doc.modelspace()})
        panel(d, PX, 130, PW, "BẢN VẼ ĐẦU VÀO", [
            (None, "Tên file", model["source"], 1),
            (None, "Số đối tượng 2D", str(stats["entities_2d"]), 1),
            (None, "Số layer", str(n_layers), 1),
            (None, "Thông tin cao độ", "chỉ có trong nhãn chữ", 1),
        ], footer="Bản vẽ chỉ có nét 2D và chữ, không có dữ liệu 3D")
        label = {"PIPE": "Đường tâm ống", "PIPE-TAG": "Nhãn tuyến, đường kính, cao độ", "VALVE": "Ký hiệu van",
                 "EQUIP": "Bồn và bơm", "STRUCT": "Giá đỡ ống", "BORDER": "Khung tên"}
        panel(d, PX, 472, PW, "LAYER TRONG BẢN VẼ", [(layer_color[n], n, v, 1) for n, v in label.items()])
    group(fade_window(t, T_TITLE_END, T_DETECT), input_panels)

    td = T_DETECT

    def detect_panel(d):
        r = lambda t0: smooth(ramp(t, t0, 0.4))
        panel(d, PX, 130, PW, "ĐỐI TƯỢNG NHẬN DIỆN", [
            (ORANGE, "Tuyến ống (đường tâm)", count_up(t, td + 0.4, stats["pipe_runs"], 1.9), r(td + 0.3)),
            ("#7EE081", "Nhãn DN và cao độ EL", count_up(t, td + 2.9, stats["tags_read"], 1.5), r(td + 2.8)),
            ("#E77BE0", "Van", count_up(t, td + 5.0, stats["valves"], 1.1), r(td + 4.9)),
            ("#F4D35E", "Bồn chứa", count_up(t, td + 6.7, stats["tanks"], 0.5), r(td + 6.6)),
            ("#F4D35E", "Bơm", count_up(t, td + 7.3, stats["pumps"], 0.5), r(td + 7.2)),
            ("#C8D2DF", "Cột giá đỡ ống", count_up(t, td + 8.1, stats["columns"], 0.6), r(td + 8.0)),
        ], footer="Cao độ và đường kính được đọc từ nhãn gắn với từng tuyến")
    group(fade_window(t, T_DETECT, T_TILT), detect_panel)

    def tag_panel(d):
        ex = next(r for r in runs if r["el"] > 4000 and r["dn"] == 150)
        y0 = 570
        box(d, PX, y0, PW, 300)
        d.text((PX + 26, y0 + 22), "CÁCH ĐỌC MỘT NHÃN", font=F_PANEL_T, fill=rgba(CYAN, 1))
        d.text((PX + 26, y0 + 62), f"{ex['line']}   DN{ex['dn']}   EL+{int(ex['el'])}", font=font(40, "bold"),
               fill=rgba("#7EE081", 1))
        for i, (k, v) in enumerate([("Số hiệu tuyến ống", ex["line"]), ("Đường kính danh nghĩa", f"{ex['dn']} mm"),
                                    ("Cao độ tâm ống", f"+{int(ex['el'])} mm")]):
            yy = y0 + 136 + 50 * i
            d.text((PX + 26, yy + 2), k, font=F_ROW, fill=rgba("#C9D5E3", 1))
            tw = d.textlength(v, font=F_VAL)
            d.text((PX + PW - 26 - tw, yy), v, font=F_VAL, fill=rgba(WHITE, 1))
    group(fade_window(t, T_DETECT, T_TILT) * smooth(ramp(t, td + 2.9, 0.5)), tag_panel)

    PX2, PW2 = 1418, 462

    def build_panel(d):
        c = counts(t)
        hh = panel(d, PX2, 130, PW2, "ĐANG DỰNG 3D", [
            (None, "Đoạn ống thẳng", f"{c['pipe']} / {TOTALS['pipe']}", 1),
            (None, "Co 90°", f"{c['elbow']} / {TOTALS['elbow']}", 1),
            (None, "Tê", f"{c['tee']} / {TOTALS['tee']}", 1),
            (None, "Van", f"{c['valve']} / {TOTALS['valve']}", 1),
            (None, "Mặt bích đầu thiết bị", f"{c['flange']} / {TOTALS['flange']}", 1),
        ])
        prog = clamp01((t - T_PIPES) / (T_PIPES_DONE - T_PIPES))
        d.rounded_rectangle((PX2, 130 + hh + 14, PX2 + PW2, 130 + hh + 24), 5, fill=rgba("#1B2B42", 1))
        if prog > 0.02:
            d.rounded_rectangle((PX2, 130 + hh + 14, PX2 + PW2 * prog, 130 + hh + 24), 5, fill=rgba(ORANGE, 1))
    group(fade_window(t, T_PIPES - 0.6, T_RESULT), build_panel)

    def result_panels(d):
        rows = [
            (None, "Tổng chiều dài ống", vn(stats["pipe_length_m"]) + " m", 1),
            (None, "Co 90° / Tê", f"{stats['elbows']} / {stats['tees']}", 1),
            (None, "Van", str(stats["valves"]), 1),
            (None, "Ống đứng nối hai cao độ", str(stats["risers"]), 1),
            (None, "Thiết bị", f"{stats['tanks']} bồn, {stats['pumps']} bơm", 1),
            (None, "Đọc bản vẽ + dựng mô hình", vn(T_CONVERT_S, 2) + " giây", 1),
            (None, "Xuất file DXF 3D", vn(T_EXPORT_S, 1) + " giây", 1),
        ]
        hh = panel(d, PX2, 130, PW2, "KẾT QUẢ", rows, footer="Thời gian đo trên máy tính cá nhân")
        ly = 130 + hh + 22
        box(d, PX2, ly, PW2, 56 + 34 * len(model["line_colors"]))
        d.text((PX2 + 26, ly + 16), "MÀU THEO TUYẾN ỐNG", font=F_SMALL, fill=rgba(CYAN, 1))
        names = {"P-100": "Ống hút, bồn về bơm", "P-101": "Ống đẩy, bơm lên giá đỡ",
                 "P-102": "Ống nạp bồn", "U-200": "Ống phụ trợ"}
        for i, (grp, col) in enumerate(model["line_colors"].items()):
            yy = ly + 46 + 34 * i
            d.rounded_rectangle((PX2 + 26, yy + 6, PX2 + 62, yy + 18), 5, fill=rgba(col, 1))
            d.text((PX2 + 76, yy - 2), f"{grp}x  ·  {names.get(grp, '')}", font=F_SMALL, fill=rgba("#C9D5E3", 1))
    group(fade_window(t, T_RESULT, T_END + 0.3), result_panels)

    def note(d):
        s_ = "Prototype v0.1  ·  Bản vẽ mẫu  ·  Nhận diện bằng quy tắc cố định"
        tw = d.textlength(s_, font=F_SMALL)
        d.text((W - 40 - tw, H - 42), s_, font=F_SMALL, fill=rgba(MUTED, 0.9))
    group(fade_window(t, T_TITLE_END - 0.4, T_END + 0.3), note)
    return out


def card(lines, footer=None, pill=None):
    """Full-screen text card. lines: list of (text, font, color, gap after)."""
    im = Image.new("RGBA", (W, H), hex_rgb(BG) + (255,))
    d = ImageDraw.Draw(im)
    for y in range(H):                                   # same vertical gradient as the 3D view
        c = [int(lerp(hex_rgb(BG_TOP)[i], hex_rgb(BG)[i], y / H)) for i in range(3)]
        d.line((0, y, W, y), fill=tuple(c) + (255,))
    total = sum(f.size + gap for _, f, _, gap in lines)
    y = (H - total) / 2 - (30 if pill else 0)
    if pill:
        pw = d.textlength(pill, font=F_STEP)
        d.rounded_rectangle((120, y - 70, 120 + pw + 32, y - 28), 9, fill=hex_rgb(ORANGE) + (255,))
        d.text((136, y - 65), pill, font=F_STEP, fill=(255, 255, 255, 255))
    for s, f, col, gap in lines:
        d.text((120, y), s, font=f, fill=hex_rgb(col) + (255,))
        y += f.size + gap
    if footer:
        d.text((120, H - 110), footer, font=font(20), fill=hex_rgb(MUTED) + (255,))
    return im


TITLE_CARD = card([
    ("Từ bản vẽ 2D", font(96, "bold"), WHITE, 6),
    ("đến mô hình 3D", font(96, "bold"), ORANGE, 44),
    ("Tự động dựng mô hình đường ống từ bản vẽ mặt bằng", font(36), "#C9D5E3", 0),
], pill="BẢN DEMO PROTOTYPE", footer="Dữ liệu trong video do prototype tạo ra từ một bản vẽ mẫu")
END_CARD = card([
    ("Bước tiếp theo", font(72, "bold"), WHITE, 46),
    ("1   Chạy trực tiếp trong AutoCAD và Smart 3D qua plug-in", font(36), "#DCE6F2", 26),
    ("2   AI agent xử lý bản vẽ thực tế, không theo một chuẩn cố định", font(36), "#DCE6F2", 26),
    ("3   Kỹ sư duyệt kết quả trước khi lưu vào mô hình dự án", font(36), "#DCE6F2", 0),
], footer="Prototype v0.1: bản vẽ mẫu, quy tắc nhận diện cố định. Hình 3D được dựng từ dữ liệu do prototype tạo ra.")


def frame(t):
    if t < T_TITLE_END - 0.6:
        return TITLE_CARD.convert("RGB")
    if t > T_END + 0.9:
        return END_CARD.convert("RGB")
    im = Image.alpha_composite(render_scene(t), overlay(t))
    if t < T_TITLE_END:
        im = Image.blend(TITLE_CARD, im, smooth(ramp(t, T_TITLE_END - 0.6, 0.6)))
    elif t > T_END + 0.3:
        im = Image.blend(im, END_CARD, smooth(ramp(t, T_END + 0.3, 0.6)))
    return im.convert("RGB")


def main():
    args = sys.argv[1:]
    try:
        pl.enable_anti_aliasing("ssaa")
    except Exception as exc:                              # pragma: no cover
        print("anti-aliasing unavailable:", exc)
    pl.enable_ssao(radius=900, bias=25, kernel_size=64, blur=True)
    STILLS.mkdir(parents=True, exist_ok=True)
    if args and args[0] == "--still":
        for ts in args[1:]:
            t = float(ts)
            frame(t).save(STILLS / f"t_{t:05.2f}.png")
            print("saved", STILLS / f"t_{t:05.2f}.png")
        return
    if args and args[0] == "--deck-stills":
        x0, y0, x1, y1 = SHEET_PX
        render_scene(8.0).convert("RGB").crop((x0 - 12, y0 - 12, x1 + 12, y1 + 12)).save(OUT / "still_2d_drawing.png")
        render_scene(17.5).convert("RGB").crop((x0 - 12, y0 - 12, x1 + 12, y1 + 12)).save(OUT / "still_detection.png")
        render_scene(T_ORBIT + 0.2).convert("RGB").save(OUT / "still_3d_model.png")
        render_scene(T_ORBIT + 8.5).convert("RGB").save(OUT / "still_3d_model_b.png")
        frame(T_ORBIT + 0.2).save(OUT / "video_poster.png")
        print("saved deck stills in", OUT)
        return
    path = OUT / "demo_2d_to_3d.mp4"
    writer = imageio_ffmpeg.write_frames(
        str(path), (W, H), fps=FPS, codec="libx264", quality=None, macro_block_size=1,
        output_params=["-crf", "19", "-preset", "medium", "-pix_fmt", "yuv420p", "-movflags", "+faststart"])
    writer.send(None)
    n = int(DUR * FPS)
    for i in range(n):
        writer.send(np.asarray(frame(i / FPS)).tobytes())
        if i % 150 == 0:
            print(f"frame {i}/{n}", flush=True)
    writer.close()
    print("wrote", path)


if __name__ == "__main__":
    main()
