import csv
import re
import sys
from collections import defaultdict
from datetime import date, timedelta
from pathlib import Path

scratch = Path(sys.argv[1])
out_dir = Path(sys.argv[2])

text = "".join((scratch / f"sprint_part{i}.md").read_text(encoding="utf-8") for i in range(1, 5))

ROLES = ["CAD", "LÕI", "ỐNG", "KH"]
row_re = re.compile(r"^\| (S(\d+)-(\d+)) \| (.*) \|$", re.M)


def num(s):
    return float(s.replace(",", "."))


def fmt(x):
    s = f"{x:.1f}".rstrip("0").rstrip(".")
    return s.replace(".", ",")


def phase(n):
    if n == 0:
        return "Chuẩn bị"
    if n <= 6:
        return "GĐ1"
    if n <= 12:
        return "GĐ2"
    if n <= 16:
        return "GĐ3"
    return "GĐ4"


POOL = 99
tasks = []
errors = []
current = None
for line in text.split("\n"):
    h = re.match(r"^### Sprint (\d+):", line)
    if h:
        current = int(h.group(1))
        continue
    if line.startswith("## 4. Sprint 0"):
        current = 0
        continue
    if line.startswith("## 13."):
        current = POOL
        continue
    if re.match(r"^## \d+\.", line) and not re.match(r"^## [5678]\.", line):
        current = None
        continue
    m = row_re.match(line)
    if not m:
        continue
    tid, sprint = m.group(1), current
    if sprint is None:
        errors.append(f"{tid}: row outside a sprint section")
        continue
    cells = [c.strip() for c in m.group(4).split("|")]
    if len(cells) != 6:
        errors.append(f"{tid}: {len(cells)} cells")
        continue
    viec, ketqua, role, cong, dep, trace = cells
    if role not in ROLES:
        errors.append(f"{tid}: role {role!r}")
    tasks.append(dict(id=tid, sprint=sprint, viec=viec, ketqua=ketqua, role=role,
                      cong=num(cong), dep=dep, trace=trace))

ids = [t["id"] for t in tasks]
dups = {i for i in ids if ids.count(i) > 1}
if dups:
    errors.append(f"duplicate ids: {sorted(dups)}")
by_id = {t["id"]: t for t in tasks}
for t in tasks:
    for d in re.findall(r"S\d+-\d+", t["dep"]):
        if d not in by_id:
            errors.append(f"{t['id']}: unknown dependency {d}")
        elif by_id[d]["sprint"] > t["sprint"]:
            errors.append(f"{t['id']}: depends on later sprint task {d}")
# references to task ids anywhere in the prose must exist
for ref in set(re.findall(r"S\d+-\d+", text)):
    if ref not in by_id:
        errors.append(f"prose references unknown task {ref}")

totals = defaultdict(lambda: defaultdict(float))
for t in tasks:
    if t["sprint"] != POOL:
        totals[t["sprint"]][t["role"]] += t["cong"]
for s, r in sorted(totals.items()):
    for role in ("CAD", "LÕI"):
        if r[role] > 8.0001:
            errors.append(f"Sprint {s}: {role} = {r[role]} > 8")

for s in sorted(totals):
    r = totals[s]
    parts = [f"`{role}` {fmt(r[role])}" for role in ROLES if r[role] > 0]
    line = f"**Tổng công.** {', '.join(parts)}. Cộng {fmt(sum(r.values()))} ngày công, {sum(1 for t in tasks if t['sprint'] == s)} việc."
    key = "{{TONG:S%d}}" % s
    if key not in text:
        errors.append(f"missing placeholder {key}")
    text = text.replace(key, line)

# calendar
GOALS = {
    0: "Bản vẽ thật, chuyên gia, khung dự án",
    1: "Khung dữ liệu, đọc bản vẽ",
    2: "Phân loại, đọc và gắn nhãn",
    3: "Đồ thị 3D, ống đầu tiên trong AutoCAD",
    4: "Cấu kiện, thiết bị, khung bộ luật",
    5: "Bộ luật, quyết định, chỉnh sửa",
    6: "Xuất kết quả, thử tại khách hàng",
    7: "Lớp công cụ, kết nối mô hình ngôn ngữ",
    8: "Vòng lặp agent, chế độ lập hồ sơ",
    9: "Chế độ giải quyết mơ hồ",
    10: "Đánh giá, tinh chỉnh, mở rộng nhận diện",
    11: "Lệnh sửa bằng lời, ống hai nét, va chạm",
    12: "Đo, sửa, nghiệm thu giai đoạn 2",
    13: "Khung plug-in Smart 3D, ánh xạ cấp ống",
    14: "Dựng tuyến và đoạn chạy trong Smart 3D",
    15: "Cấu kiện, đầu chờ, gỡ bỏ an toàn",
    16: "Thử nghiệm Smart 3D, nghiệm thu giai đoạn 3",
    17: "Bộ cài, quy trình phát hành",
    18: "Cấp phép",
    19: "Triển khai trong mạng của khách hàng",
    20: "Tài liệu, đào tạo",
    21: "Máy chủ MCP, chế độ dịch vụ",
    22: "Theo yêu cầu của khách hàng",
    23: "Hoàn thiện trước phát hành",
    24: "Phát hành bản 1.0",
}
MILESTONES = {0: "M0", 2: "M1", 3: "M2", 5: "M3", 6: "M4", 9: "M5", 12: "M6", 16: "M7", 24: "M8"}
start = date(2026, 11, 16)
assert start.weekday() == 0
tet_from = date(2027, 2, 8)
rows = ["| Sprint | Giai đoạn | Từ thứ Hai | Đến thứ Sáu | Trọng tâm | Mốc |", "|---|---|---|---|---|---|"]
cur = start
for s in range(25):
    if cur == tet_from:
        rows.append(f"| Nghỉ Tết | | {cur:%d/%m/%Y} | {cur + timedelta(days=4):%d/%m/%Y} | Giả định nghỉ một tuần | |")
        cur += timedelta(days=7)
    end = cur + timedelta(days=11)
    rows.append(f"| {s} | {phase(s)} | {cur:%d/%m/%Y} | {end:%d/%m/%Y} | {GOALS[s]} | {MILESTONES.get(s, '')} |")
    cur += timedelta(days=14)
text = text.replace("{{LICH}}", "\n".join(rows))
last_end = cur - timedelta(days=3)

# one-developer numbers
dev = lambda sprints: sum(t["cong"] for t in tasks if t["sprint"] in sprints and t["role"] in ("CAD", "LÕI"))
kh = lambda sprints: sum(t["cong"] for t in tasks if t["sprint"] in sprints and t["role"] == "KH")
g1 = range(1, 7)
MIN_B = (
    [t["id"] for t in tasks if t["sprint"] in (1, 2, 3, 4) and t["role"] == "LÕI"]
    + ["S1-09", "S1-10", "S1-11", "S1-12", "S1-16", "S1-17", "S2-10"]
    + ["S3-10", "S3-11", "S3-12", "S3-13", "S3-14", "S3-15", "S3-16"]
    + ["S4-09", "S4-10", "S4-11", "S4-15"]
    + ["S5-01", "S5-02", "S5-03", "S5-04", "S5-05", "S5-06", "S5-07"]
    + ["S6-04", "S6-05", "S6-08", "S6-11", "S6-12"]
)
b_total = sum(by_id[i]["cong"] for i in MIN_B)
g1_dev = dev(g1)
one = [
    "| Khoản | Ngày công |",
    "|---|---|",
    f"| Việc lập trình của Sprint 0 | {fmt(dev([0]))} |",
    f"| Việc lập trình của giai đoạn 1, Sprint 1 đến 6 | {fmt(g1_dev)} |",
    f"| Việc của `KH` trong Sprint 0 | {fmt(kh([0]))} |",
    f"| Việc của `KH` trong giai đoạn 1 | {fmt(kh(g1))} |",
    f"| Riêng chuỗi tối thiểu của lựa chọn B | {fmt(b_total)} |",
    "",
    f"Một người xếp được 8 ngày công lập trình mỗi sprint, hoặc khoảng 7 nếu kiêm `KH`. "
    f"Toàn bộ giai đoạn 1 khi đó cần từ {g1_dev / 8:.0f} đến {g1_dev / 7:.0f} sprint. "
    f"Chuỗi tối thiểu cần từ {b_total / 8:.0f} đến {b_total / 7:.0f} sprint.",
]
text = text.replace("{{MOTNGUOI}}", "\n".join(one))

if "{{" in text:
    errors.append("unfilled placeholder remains")
if "—" in text or "–" in text:
    errors.append("dash characters present")

(out_dir / "KE_HOACH_SPRINT.md").write_text(text, encoding="utf-8", newline="\n")
with open(out_dir / "backlog.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f)
    w.writerow(["Mã", "Sprint", "Giai đoạn", "Việc", "Kết quả phải có", "Vai trò", "Công (ngày)", "Phụ thuộc", "Truy vết"])
    for t in tasks:
        pooled = t["sprint"] == POOL
        w.writerow([t["id"], "Chờ" if pooled else t["sprint"], "Danh sách chờ" if pooled else phase(t["sprint"]),
                    t["viec"], t["ketqua"], t["role"], fmt(t["cong"]), t["dep"], t["trace"]])

print("tasks:", len(tasks), "| pooled:", [t["id"] for t in tasks if t["sprint"] == POOL], "| last sprint ends:", last_end)
print("total days by role:", {r: sum(t["cong"] for t in tasks if t["role"] == r) for r in ROLES})
print("phase dev days:", {p: sum(t["cong"] for t in tasks if phase(t["sprint"]) == p and t["role"] in ("CAD", "LÕI")) for p in ["Chuẩn bị", "GĐ1", "GĐ2", "GĐ3", "GĐ4"]})
print("G1 dev:", g1_dev, "| B minimal:", b_total, "| lines:", text.count("\n"))
for s in sorted(totals):
    print(s, {k: v for k, v in totals[s].items()})
print("ERRORS:" if errors else "no errors")
for e in errors:
    print("  ", e)
