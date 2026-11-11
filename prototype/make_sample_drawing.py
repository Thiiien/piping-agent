"""Create the SAMPLE 2D piping plan (DXF) used as demo input.

This script only draws 2D entities, exactly as a drafter would in AutoCAD:
centerlines, text tags, valve symbols, equipment outlines. No 3D data is
written. The converter (convert_2d_to_3d.py) reads the DXF file only.
Units: millimetres.
"""
from pathlib import Path
import ezdxf
from ezdxf.enums import TextEntityAlignment

OUT = Path(__file__).parent / "data" / "piping_plan_2d.dxf"
TEXT_H = 250

doc = ezdxf.new("R2010", setup=True)
doc.units = ezdxf.units.MM
msp = doc.modelspace()

LAYERS = {            # name: AutoCAD color index
    "PIPE": 4,        # cyan
    "PIPE-TAG": 3,    # green
    "PIPE-RISER": 4,
    "VALVE": 6,       # magenta
    "EQUIP": 2,       # yellow
    "EQUIP-TAG": 2,
    "STRUCT": 8,      # grey
    "STRUCT-TAG": 8,
    "BORDER": 7,      # white
}
for name, color in LAYERS.items():
    doc.layers.add(name, color=color)
doc.styles.add("ARIAL", font="arial.ttf")

# ---- blocks -----------------------------------------------------------
valve = doc.blocks.new("VALVE_GATE")
bow = [(-250, -150), (-250, 150), (250, -150), (250, 150), (-250, -150)]
valve.add_lwpolyline(bow, dxfattribs={"layer": "0"})

col = doc.blocks.new("COLUMN")
col.add_lwpolyline([(-150, -150), (150, -150), (150, 150), (-150, 150)],
                   close=True, dxfattribs={"layer": "0"})
col.add_line((-150, -150), (150, 150), dxfattribs={"layer": "0"})
col.add_line((-150, 150), (150, -150), dxfattribs={"layer": "0"})


def text(s, pos, layer, h=TEXT_H):
    t = msp.add_text(s, height=h, dxfattribs={"layer": layer, "style": "ARIAL"})
    t.set_placement(pos, align=TextEntityAlignment.LEFT)
    return t


# ---- equipment --------------------------------------------------------
for tag, (cx, cy) in {"T-101": (6000, 14000), "T-102": (6000, 6000)}.items():
    msp.add_circle((cx, cy), 2500, dxfattribs={"layer": "EQUIP"})
    text(tag, (cx - 500, cy + 150), "EQUIP-TAG", 320)
    text("H=6000", (cx - 620, cy - 450), "EQUIP-TAG")

for tag, (cx, cy) in {"P-101A": (16000, 12000), "P-101B": (16000, 8000)}.items():
    msp.add_lwpolyline(
        [(cx - 1200, cy - 550), (cx + 1200, cy - 550), (cx + 1200, cy + 550), (cx - 1200, cy + 550)],
        close=True, dxfattribs={"layer": "EQUIP"})
    text(tag, (cx - 250, cy - 120), "EQUIP-TAG")

# ---- pipe rack --------------------------------------------------------
RACK_X = (21500, 24900)
RACK_Y = (3000, 8000, 13000, 18000)
for y in RACK_Y:
    for x in RACK_X:
        msp.add_blockref("COLUMN", (x, y), dxfattribs={"layer": "STRUCT"})
    msp.add_line((RACK_X[0], y), (RACK_X[1], y), dxfattribs={"layer": "STRUCT"})
for x in RACK_X:
    for y0, y1 in zip(RACK_Y, RACK_Y[1:]):
        msp.add_line((x, y0), (x, y1), dxfattribs={"layer": "STRUCT"})
text("PIPE RACK  TOS EL+4000", (16000, 2200), "STRUCT-TAG")

# ---- pipes: one polyline per constant-elevation run -------------------
def pipe(points):
    msp.add_lwpolyline(points, dxfattribs={"layer": "PIPE"})

def tag(s, pos):
    text(s, pos, "PIPE-TAG")

def leader_tag(s, on_pipe, y):
    """Tag placed east of the rack with a leader line touching the pipe."""
    msp.add_line(on_pipe, (25800, y), dxfattribs={"layer": "PIPE-TAG"})
    text(s, (25900, y - 100), "PIPE-TAG")

def valve_at(pos, rotation=0):
    msp.add_blockref("VALVE_GATE", pos, dxfattribs={"layer": "VALVE", "rotation": rotation})

def riser(pos, r=180):
    msp.add_circle(pos, r, dxfattribs={"layer": "PIPE-RISER"})

# suction lines, tanks -> pumps
pipe([(8500, 14000), (12500, 14000), (12500, 12000), (14800, 12000)])
tag("P-1001 DN200 EL+600", (11050, 14300)); valve_at((10300, 14000))
pipe([(8500, 6000), (12500, 6000), (12500, 8000), (14800, 8000)])
tag("P-1002 DN200 EL+600", (11050, 5450)); valve_at((10300, 6000))
pipe([(12500, 12000), (12500, 8000)])                       # cross-tie
tag("P-1003 DN200 EL+600", (13200, 9900)); valve_at((12500, 10000), 90)

# pump A discharge: low run, riser, run on rack to north battery limit
pipe([(15300, 12550), (15300, 13300), (19500, 13300)])
tag("P-1011 DN150 EL+600", (15500, 13900)); valve_at((17500, 13300))
pipe([(19500, 13300), (23000, 13300), (23000, 19500)])
leader_tag("P-1011 DN150 EL+4100", (23000, 15500), 15500); riser((19500, 13300))

# pump B discharge
pipe([(15300, 7450), (15300, 6700), (19500, 6700)])
tag("P-1012 DN150 EL+600", (15500, 5850)); valve_at((17500, 6700))
pipe([(19500, 6700), (23700, 6700), (23700, 19500)])
leader_tag("P-1012 DN150 EL+4100", (23700, 10500), 10500); riser((19500, 6700))

# tank fill line from battery limit, rises off the rack, branches to both tanks
pipe([(22300, 19500), (22300, 17000)])
leader_tag("P-1021 DN100 EL+4100", (22300, 18700), 18700); riser((22300, 17000), 140)
pipe([(22300, 17000), (6000, 17000), (6000, 16500)])
tag("P-1021 DN100 EL+5500", (13500, 17300)); valve_at((8200, 17000))
pipe([(10500, 17000), (10500, 10000), (6000, 10000), (6000, 8500)])
tag("P-1022 DN100 EL+5500", (6300, 9500)); valve_at((10500, 15500), 90)

# utility line straight along the rack
pipe([(24400, 1500), (24400, 19500)])
leader_tag("U-2001 DN80 EL+4100", (24400, 5000), 5000)

# ---- border, title block, north arrow (ignored by the converter) ------
X0, Y0, X1, Y1 = -1500, -3500, 31500, 22000
B = {"layer": "BORDER"}
msp.add_lwpolyline([(X0, Y0), (X1, Y0), (X1, Y1), (X0, Y1)], close=True, dxfattribs=B)
msp.add_line((X0, -1300), (X1, -1300), dxfattribs=B)
for x in (14000, 22000, 27000):
    msp.add_line((x, Y0), (x, -1300), dxfattribs=B)
text("PIPING PLAN - PUMP STATION AREA 100", (-900, -2650), "BORDER", 500)
text("DWG NO.  A100-PP-001", (14400, -2200), "BORDER", 300)
text("SAMPLE DRAWING FOR DEMO", (14400, -2900), "BORDER", 240)
text("SCALE 1:100", (22400, -2550), "BORDER", 300)
text("REV 0", (27500, -2550), "BORDER", 300)
text("UNITS: MM", (27500, -3100), "BORDER", 200)
# north arrow
nx, ny = 29500, 1300
msp.add_circle((nx, ny), 900, dxfattribs=B)
msp.add_lwpolyline([(nx, ny + 800), (nx - 350, ny - 500), (nx, ny - 200), (nx + 350, ny - 500)],
                   close=True, dxfattribs=B)
text("N", (nx - 130, ny + 1050), "BORDER", 400)
text("B.L.", (22900, 19800), "BORDER", 250)

doc.saveas(OUT)
print("wrote", OUT, "entities:", len(msp))
