"""Parametric 3D shapes for piping components (pyvista meshes, units = mm)."""
import numpy as np
import pyvista as pv

OD = {50: 60.3, 80: 88.9, 100: 114.3, 150: 168.3, 200: 219.1, 250: 273.0, 300: 323.9}
Z = np.array([0.0, 0.0, 1.0])


def unit(v):
    v = np.asarray(v, float)
    return v / np.linalg.norm(v)


def cyl(p0, p1, radius, n=28):
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    return pv.Cylinder(center=(p0 + p1) / 2, direction=p1 - p0, radius=radius,
                       height=float(np.linalg.norm(p1 - p0)), resolution=n, capping=True)


def pipe(p0, p1, od, n=28):
    return cyl(p0, p1, od / 2, n)


def elbow(node, u, v, r, od, n=28, steps=12):
    """90-degree bend at `node`; u, v are unit vectors toward the two neighbours."""
    node, u, v = np.asarray(node, float), unit(u), unit(v)
    c = node + (u + v) * r
    th = np.linspace(0, np.pi / 2, steps + 1)
    pts = np.array([c - v * r * np.cos(t) - u * r * np.sin(t) for t in th])
    return pv.lines_from_points(pts).tube(radius=od / 2, n_sides=n, capping=True)


def flange(center, axis, od, n=28):
    center, axis = np.asarray(center, float), unit(axis)
    t = od * 0.28
    return cyl(center - axis * t / 2, center + axis * t / 2, od * 0.92, n)


def merge(parts):
    out = parts[0].triangulate()
    for p in parts[1:]:
        out = out.merge(p.triangulate())
    return out


def valve(center, axis, od):
    """Gate valve on a horizontal pipe. Returns (body, handwheel)."""
    c, d = np.asarray(center, float), unit(axis)
    length = 2.6 * od
    r = od * 0.78
    body = merge([
        pv.Cone(center=c - d * length / 4, direction=d, height=length / 2, radius=r, resolution=28),
        pv.Cone(center=c + d * length / 4, direction=-d, height=length / 2, radius=r, resolution=28),
        flange(c - d * length / 2, d, od),
        flange(c + d * length / 2, d, od),
        cyl(c - d * od * 0.3, c + d * od * 0.3, od * 0.55),          # bonnet body
        cyl(c, c + Z * od * 2.3, od * 0.13, 16),                       # stem
    ])
    wheel = pv.ParametricTorus(ringradius=od * 0.95, crosssectionradius=od * 0.1,
                               u_res=32, v_res=12, w_res=12)
    wheel = wheel.translate(c + Z * od * 2.3, inplace=False)
    return body, wheel


def tank(cx, cy, r, h, n=64):
    shell = pv.Cylinder(center=(cx, cy, h / 2), direction=Z, radius=r, height=h, resolution=n)
    hc = r * 0.16
    roof = pv.Cone(center=(cx, cy, h + hc / 2), direction=Z, height=hc, radius=r, resolution=n)
    return merge([shell, roof])


def box(x0, x1, y0, y1, z0, z1):
    return pv.Box(bounds=(x0, x1, y0, y1, z0, z1))


def pump(p):
    """p: pump dict from the converter. Returns (base, body) meshes.

    Shaft runs along the long side of the footprint at height cl_z. The casing sits at
    casing_center, the motor fills the rest of the skid toward the far end.
    """
    x0, x1, y0, y1 = p["bounds"]
    cl = p["cl_z"]
    horiz = (x1 - x0) >= (y1 - y0)
    k = 0 if horiz else 1                       # index of the shaft axis
    lo, hi = (x0, x1) if horiz else (y0, y1)
    width = (y1 - y0) if horiz else (x1 - x0)
    cc = np.array(p["casing_center"], float)
    ax = np.zeros(3); ax[k] = 1.0
    sgn = 1.0 if p["casing_side"] < 0 else -1.0  # direction from casing toward the motor
    far = cc.copy(); far[k] = hi if sgn > 0 else lo
    rc = width * 0.36

    def slab(c0, c1, half):
        """Box from the skid top up to the shaft, between shaft coordinates c0..c1."""
        a, b = sorted((c0, c1))
        if horiz:
            return box(a, b, cc[1] - half, cc[1] + half, 160, cl)
        return box(cc[0] - half, cc[0] + half, a, b, 160, cl)

    motor_start = cc + ax * sgn * 620
    motor_end = far - ax * sgn * 120
    parts = [
        cyl(cc - ax * 190, cc + ax * 190, rc, 40),                       # casing
        cyl(cc + ax * sgn * 190, motor_start, rc * 0.42, 24),            # coupling
        cyl(motor_start, motor_end, rc * 0.8, 40),                       # motor
        slab(cc[k] - 260, cc[k] + 260, rc * 0.7),                        # casing pedestal
        slab(motor_start[k] + sgn * 80, motor_end[k] - sgn * 80, rc * 0.6),  # motor pedestal
    ]
    for nz in p["nozzles"]:
        parts.append(cyl(cc, np.array(nz["p"], float), nz["od"] / 2, 24))  # nozzle stub
    return box(x0, x1, y0, y1, 0, 160), merge(parts)


def column(x, y, tos, s=300):
    return box(x - s / 2, x + s / 2, y - s / 2, y + s / 2, 0, tos)


def beam(a, b, tos, depth=300, width=200):
    (ax, ay), (bx, by) = a, b
    if abs(ax - bx) < 1e-6:
        return box(ax - width / 2, ax + width / 2, min(ay, by), max(ay, by), tos - depth, tos)
    return box(min(ax, bx), max(ax, bx), ay - width / 2, ay + width / 2, tos - depth, tos)
