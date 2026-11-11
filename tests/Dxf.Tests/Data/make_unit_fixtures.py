"""Sinh bốn tệp DXF nhỏ, mỗi tệp một đường dài 1000 đơn vị, khai báo đơn vị khác nhau.
Chạy một lần: .venv/Scripts/python tests/Dxf.Tests/Data/make_unit_fixtures.py
"""
from pathlib import Path

import ezdxf

HERE = Path(__file__).parent
UNITS = {"units_mm.dxf": 4, "units_m.dxf": 6, "units_inch.dxf": 1, "units_none.dxf": 0}

for name, insunits in UNITS.items():
    doc = ezdxf.new("R2018")
    doc.header["$INSUNITS"] = insunits
    msp = doc.modelspace()
    doc.layers.add("PIPE", color=4)
    msp.add_line((0, 0), (1000, 0), dxfattribs={"layer": "PIPE"})
    msp.add_text("P-1 %%C100 %%UEL+2500", height=50, dxfattribs={"layer": "PIPE", "insert": (0, 100)})
    msp.add_mtext("Dòng một\\PDòng hai {\\fArial;chữ} \\U+00D8100", dxfattribs={"char_height": 50, "insert": (0, 300)})
    doc.saveas(HERE / name)
    print("wrote", name)
