# Nguồn của kế hoạch sprint

`docs/KE_HOACH_SPRINT.md` và `docs/backlog.csv` được sinh từ bốn tệp `sprint_part*.md` ở đây. Không sửa hai tệp kết quả bằng tay.

Sửa kế hoạch: sửa tệp `sprint_part*.md`, rồi chạy:

```bash
.venv/Scripts/python tools/sprint_plan/build.py tools/sprint_plan docs
```

Lệnh này tính tổng công từng sprint, điền lịch, và kiểm tra: mã việc không trùng, phụ thuộc tồn tại và không trỏ tới sprint sau, mỗi lập trình viên không quá 8 ngày công mỗi sprint. Có lỗi thì lệnh in ra danh sách lỗi.
