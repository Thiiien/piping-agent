"""Sinh data/catalog/*.json từ bảng ASME B36.10M và B16.9 chép tay.

Các số liệu dưới đây được chép từ trí nhớ về bảng tiêu chuẩn, CHƯA được chuyên gia đối chiếu
với bản tiêu chuẩn gốc (việc S2-16). Tệp sinh ra ghi "verified": false cho đến khi được ký xác nhận.
Chạy: .venv/Scripts/python tools/make_catalog.py
"""
import json
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "data" / "catalog"

# DN: (NPS, OD, STD wall, XS wall), mm. ASME B36.10M.
PIPES = {
    15: ("1/2", 21.3, 2.77, 3.73),
    20: ("3/4", 26.7, 2.87, 3.91),
    25: ("1", 33.4, 3.38, 4.55),
    32: ("1-1/4", 42.2, 3.56, 4.85),
    40: ("1-1/2", 48.3, 3.68, 5.08),
    50: ("2", 60.3, 3.91, 5.54),
    65: ("2-1/2", 73.0, 5.16, 7.01),
    80: ("3", 88.9, 5.49, 7.62),
    90: ("3-1/2", 101.6, 5.74, 8.08),
    100: ("4", 114.3, 6.02, 8.56),
    125: ("5", 141.3, 6.55, 9.53),
    150: ("6", 168.3, 7.11, 10.97),
    200: ("8", 219.1, 8.18, 12.70),
    250: ("10", 273.0, 9.27, 12.70),
    300: ("12", 323.8, 9.53, 12.70),
    350: ("14", 355.6, 9.53, 12.70),
    400: ("16", 406.4, 9.53, 12.70),
    450: ("18", 457.0, 9.53, 12.70),
    500: ("20", 508.0, 9.53, 12.70),
    550: ("22", 559.0, 9.53, 12.70),
    600: ("24", 610.0, 9.53, 12.70),
}

# ASME B16.9, mm. A: co 90 LR tâm tới đầu. A_SR: co 90 SR. B: co 45 LR. C/M: tê cùng cỡ. H: côn thu. E: nắp.
# None nghĩa là tiêu chuẩn không có cỡ đó.
FITTINGS = {
    #  DN:  A,   A_SR,  B,   C,   M,   H,    E
    15: (38, None, 16, 25, 25, None, 25),
    20: (29, None, 11, 29, 29, 38, 25),
    25: (38, 25, 16, 38, 38, 51, 38),
    32: (48, 32, 20, 48, 48, 51, 38),
    40: (57, 38, 24, 57, 57, 64, 38),
    50: (76, 51, 32, 64, 64, 76, 38),
    65: (95, 64, 40, 76, 76, 89, 38),
    80: (114, 76, 48, 86, 86, 89, 51),
    90: (133, 89, 56, 95, 95, 102, 64),
    100: (152, 102, 64, 105, 105, 102, 64),
    125: (190, 127, 79, 124, 124, 127, 76),
    150: (229, 152, 95, 143, 143, 140, 89),
    200: (305, 203, 127, 178, 178, 152, 102),
    250: (381, 254, 159, 216, 216, 178, 127),
    300: (457, 305, 190, 254, 254, 203, 152),
    350: (533, 356, 222, 279, 279, 330, 165),
    400: (610, 406, 254, 305, 305, 356, 178),
    450: (686, 457, 286, 343, 343, 381, 203),
    500: (762, 508, 318, 381, 381, 508, 229),
    550: (838, 559, 349, 419, 419, 508, 254),
    600: (914, 610, 381, 432, 432, 508, 267),
}

NOTE = ("Số liệu chép tay từ bảng tiêu chuẩn, chưa đối chiếu với bản gốc. "
        "Chuyên gia đường ống đối chiếu và đổi verified thành true ở việc S2-16.")


def pipes():
    return {
        "schemaVersion": "1.0",
        "standard": "ASME-B36.10",
        "title": "Welded and seamless wrought steel pipe, kích thước DN15 đến DN600",
        "units": "mm",
        "verified": False,
        "note": NOTE,
        "pipes": [
            {"dn": dn, "nps": nps, "od": od, "wall": {"STD": std, "XS": xs}}
            for dn, (nps, od, std, xs) in PIPES.items()
        ],
        "fittings": [],
    }


def fittings():
    rows = []
    for dn, (a, a_sr, b, c, m, h, e) in FITTINGS.items():
        rows.append({"type": "ELL90LR", "dn": dn, "dims": {"A": a}})
        if a_sr is not None:
            rows.append({"type": "ELL90SR", "dn": dn, "dims": {"A": a_sr}})
        rows.append({"type": "ELL45LR", "dn": dn, "dims": {"B": b}})
        rows.append({"type": "TEE", "dn": dn, "dims": {"C": c, "M": m}})
        if h is not None:
            rows.append({"type": "RED", "dn": dn, "dims": {"H": h}})
        rows.append({"type": "CAP", "dn": dn, "dims": {"E": e}})
    return {
        "schemaVersion": "1.0",
        "standard": "ASME-B16.9",
        "title": "Factory-made wrought buttwelding fittings, DN15 đến DN600",
        "units": "mm",
        "verified": False,
        "note": NOTE + " Mục RED không ghi dn2: chiều dài H áp cho mọi cỡ thu từ đầu lớn đó.",
        "pipes": [],
        "fittings": rows,
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, data in (("asme-b36.10.json", pipes()), ("asme-b16.9.json", fittings())):
        path = OUT / name
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("wrote", path)


if __name__ == "__main__":
    main()
