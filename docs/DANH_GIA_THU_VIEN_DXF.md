# Đánh giá thư viện đọc DXF cho .NET

| Mục | Giá trị |
|---|---|
| Việc | S0-13 trong [`KE_HOACH_SPRINT.md`](KE_HOACH_SPRINT.md) |
| Quyết định liên quan | Q11 trong [`TAI_LIEU_DU_AN.md`](TAI_LIEU_DU_AN.md) |
| Ngày đo | 09/11/2026 trên tệp mẫu, 10/11/2026 trên bản vẽ thật |
| Trạng thái | **Đủ dữ liệu để chốt Q11.** Đã đo trên một bản vẽ mặt bằng thật do AutoCAD ghi, cả DXF lẫn DWG. Chờ người sáng lập xác nhận |

## Kết luận

Đề xuất dùng **ACadSharp** làm thư viện chính và giữ **netDxf** làm phương án dự phòng. Loại **IxMilia.Dxf** vì không lộ handle của đối tượng qua API công khai, trong khi yêu cầu FR-ING-07 bắt buộc lưu handle để truy vết.

Trên bản vẽ thật, ACadSharp là thư viện duy nhất đọc đủ: hai thư viện kia bỏ qua toàn bộ 57 đối tượng `MULTILEADER`, là loại đường dẫn nhiều nhánh mà bản vẽ thật dùng để gắn ghi chú vào ống. Thiếu chúng thì bước gắn chú thích theo mục 7.9 mất một nguồn bằng chứng có điểm 1,00. ACadSharp cũng đọc thẳng tệp DWG gốc và cho kết quả giống hệt bản DXF, nên yêu cầu FR-ING-10 không cần bước chuyển đổi qua AutoCAD.

Giá phải trả: ACadSharp chậm hơn netDxf khoảng 1,4 lần trên bản vẽ thật, tức 1,1 giây so với 0,8 giây cho 19 nghìn đối tượng. Mức này nằm dưới ngưỡng NFR về thời gian đọc.

Bộ đọc DXF nằm sau giao diện `IDrawingReader`, nên đổi thư viện về sau chỉ ảnh hưởng dự án `PipingAgent.Dxf`.

## Cách đo

- Số liệu đối chứng lấy từ `ezdxf` 1.4.5, là thư viện prototype Python đang dùng.
- Mỗi thư viện đọc cùng một tệp. So với đối chứng: số đối tượng theo loại, tổng số đỉnh polyline, nội dung chữ, tên block của các lần chèn, số layer, số đối tượng có handle.
- Thời gian là trung vị của 5 lần đọc, sau một lần đọc khởi động. Đo trên máy phát triển, .NET 8, bản dựng Release.

Chạy lại trên một tệp bất kỳ:

```bash
.venv/Scripts/python tools/dxf_ground_truth.py <tệp.dxf> > truth.json
dotnet run -c Release --project tools/DxfLibEval -- <tệp.dxf> truth.json
```

## Kết quả đo

**Tệp `piping_plan_2d.dxf`**: bản vẽ mẫu 2D, 58 KB, 80 đối tượng gồm 18 LINE, 15 LWPOLYLINE, 6 CIRCLE, 26 TEXT, 15 INSERT.

| Thư viện | Khớp đối chứng | Có handle | Thời gian đọc |
|---|---|---|---|
| netDxf.netstandard 3.0.1 | Khớp hoàn toàn | 80 trên 80 | 3,6 ms |
| ACadSharp 3.8.35 | Khớp hoàn toàn | 80 trên 80 | 11,8 ms |
| IxMilia.Dxf 0.8.4 | Khớp về nội dung | 0 trên 80 | 8,3 ms |

**Tệp `model_3d.dxf`**: mô hình 3D của prototype, 8,6 MB, 87 lưới đa diện với 50.457 bản ghi đỉnh và mặt.

| Thư viện | Khớp đối chứng | Có handle | Thời gian đọc |
|---|---|---|---|
| netDxf.netstandard 3.0.1 | Khớp hoàn toàn | 87 trên 87 | 195 ms |
| ACadSharp 3.8.35 | Khớp hoàn toàn | 87 trên 87 | 833 ms |
| IxMilia.Dxf 0.8.4 | Khớp về nội dung | 0 trên 87 | 484 ms |

Ghi chú về phép đo: ACadSharp lưu đỉnh và mặt của lưới đa diện thành hai danh sách riêng, còn ezdxf đếm chung. Lần đo đầu tôi chỉ đếm danh sách đỉnh nên ra thiếu. Sau khi cộng cả hai danh sách thì khớp. Đây là lỗi của phép đo, không phải lỗi của thư viện.

**Tệp `A410B4001.dxf`**: bản vẽ mặt bằng đường ống thật, khổ A0 tỉ lệ 1/60, do AutoCAD 2025 xuất từ DWG định dạng 2007 bằng `accoreconsole`. 10,7 MB, 19.054 đối tượng gồm 9.095 LINE, 4.705 INSERT, 2.095 TEXT, 854 LWPOLYLINE, 702 CIRCLE, 385 ARC, 375 DIMENSION, 339 LEADER, 243 ELLIPSE, 117 SOLID, 74 POINT, 57 MULTILEADER, 7 HATCH, 6 SPLINE. 61 layer, 847 định nghĩa block. Chi tiết bản vẽ ở [`KHAO_SAT_BAN_VE_THAT.md`](KHAO_SAT_BAN_VE_THAT.md).

| Thư viện | Khớp đối chứng | Có handle | Thời gian đọc |
|---|---|---|---|
| netDxf.netstandard 3.0.1 | **Thiếu 57 MULTILEADER**, còn lại khớp | 18.997 trên 19.054 | 794 ms |
| ACadSharp 3.8.35 | Khớp hoàn toàn | 19.054 trên 19.054 | 1.111 ms |
| IxMilia.Dxf 0.8.4 | **Thiếu 57 MULTILEADER**, còn lại khớp | 0 trên 19.054 | 640 ms |

**Tệp `A410B4001.dwg`**: tệp DWG gốc, 1,7 MB, định dạng AutoCAD 2007.

| Thư viện | Kết quả |
|---|---|
| netDxf.netstandard 3.0.1 | Không đọc được, báo phiên bản không hỗ trợ. Thư viện chỉ đọc DXF |
| ACadSharp 3.8.35 | Khớp hoàn toàn với đối chứng lấy từ bản DXF, 1.061 ms |
| IxMilia.Dxf 0.8.4 | Không đọc được. Thư viện chỉ đọc DXF |

Bản vẽ thật này không có MTEXT, không có block thuộc tính ngoài khung tên, không có xref và không có đối tượng proxy. Các loại đó vẫn chưa được thử.

## Thông tin gói

Lấy từ siêu dữ liệu trên nuget.org ngày 09/11/2026.

| Tiêu chí | netDxf | ACadSharp | IxMilia.Dxf |
|---|---|---|---|
| Gói đã thử | `netDxf.netstandard` 3.0.1 | `ACadSharp` 3.8.35 | `IxMilia.Dxf` 0.8.4 |
| Giấy phép | MIT | MIT | MIT |
| Hỗ trợ .NET Standard 2.0 | Có, ở gói `netDxf.netstandard`. Gói `netDxf` chính chỉ có .NET Framework 4.8 và .NET 6 | Có, kèm bản riêng cho .NET Framework 4.8 và .NET 8 trở lên | Có |
| Bản phát hành gần nhất | 20/05/2024. Gói `netDxf` chính phát hành 01/2025 nhưng trỏ cùng một phiên bản mã nguồn | 28/10/2026, có ba bản trong hai tháng gần đây | 13/06/2024 |
| Phụ thuộc trên .NET Standard 2.0 | Không có | `System.Memory`, `System.Text.Encoding.CodePages` | `System.Text.Encoding.CodePages` |
| Đọc DWG | Không | Có | Không |
| Lộ handle của đối tượng | Có | Có | Không |

## Điều chưa kiểm chứng

- **Mới có một bản vẽ thật**, từ một nhà thầu và một kiểu bản vẽ là mặt bằng. Bản vẽ của nhà thầu khác, hoặc bản isometric, có thể dùng loại đối tượng khác.
- **Các loại đối tượng chưa có trong tệp thử:** chữ nhiều dòng, thuộc tính của block ngoài khung tên, bản vẽ tham chiếu ngoài, đối tượng proxy. Yêu cầu FR-ING-01 cần tất cả các loại này.
- **Nội dung bên trong đối tượng.** Mới so số lượng, số đỉnh, nội dung chữ và tên block. Chưa so toạ độ, góc, bán kính từng đối tượng giữa các thư viện.
- **Hành xử khi gặp lỗi.** Chưa thử tệp hỏng. Yêu cầu FR-REC-10 cần thư viện bỏ qua đối tượng lỗi thay vì dừng cả tệp.
- **Tình trạng bảo trì của netDxf.** Suy ra từ ngày phát hành gói. Chưa xem trang kho mã của dự án.
- **DWG định dạng mới hơn 2007.** Tệp DWG thử là định dạng 2007. ACadSharp mô tả có đọc được đến định dạng 2018, nhưng chưa thử.
- **Xung đột phiên bản thư viện trong tiến trình AutoCAD.** `System.Memory` là thư viện hay gây xung đột khi nhiều plug-in cùng nạp trên .NET Framework. Việc này chỉ ảnh hưởng nếu bộ đọc DXF được nạp bên trong AutoCAD. Theo thiết kế, trong AutoCAD dùng bộ đọc trực tiếp, bộ đọc DXF chỉ chạy ngoài.
- **Ghi DXF.** Chưa đánh giá. Cần cho yêu cầu FR-EXP-05, hiện nằm trong danh sách chờ.

## Việc cần làm khi có thêm bản vẽ thật

1. Xuất bản vẽ ra DXF bằng `accoreconsole` với kịch bản `DXFOUT`, chạy hai lệnh ở trên. Công cụ nhận cả tệp `.dwg`, khi đó chỉ ACadSharp đọc.
2. Ghi lại thư viện nào đọc thiếu, đọc sai, hoặc dừng giữa chừng, theo từng loại đối tượng.
3. Ưu tiên tìm bản vẽ có MTEXT, block thuộc tính và xref để bịt các điểm chưa kiểm chứng.
