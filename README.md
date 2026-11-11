# Piping Agent: tờ isometric sang mô hình 3D trong AutoCAD

Plug-in AutoCAD 2025 đọc tờ isometric do ISOGEN xuất (DWG, DXF hoặc PDF vector), kiểm kê cấu kiện theo bảng vật tư, rồi dựng ống, co, tê, mặt bích, van thành khối 3D đúng toạ độ E, N, EL, có cổng nối giữa các khối. Sản phẩm thu hẹp về hướng này từ 10/11/2026; lý do, khảo sát và kế hoạch ở [`docs/CHUYEN_HUONG_ISOMETRIC.md`](docs/CHUYEN_HUONG_ISOMETRIC.md). Phần nhận diện mặt bằng 2D giữ làm chức năng phụ, không phát triển thêm.

Tài liệu dự án gốc, viết cho hướng mặt bằng 2D trước pivot: [`docs/TAI_LIEU_DU_AN.md`](docs/TAI_LIEU_DU_AN.md) và [`docs/KE_HOACH_SPRINT.md`](docs/KE_HOACH_SPRINT.md). Mã nguồn vẫn trích dẫn số mục của hai tài liệu này.

## Dùng trong AutoCAD

Cài theo [`build/HUONG_DAN_CAI.md`](build/HUONG_DAN_CAI.md): `build\pack_bundle.ps1` tạo `dist\PipingAgent.bundle`, chép vào `%APPDATA%\Autodesk\ApplicationPlugins` hoặc chạy `build\pack_bundle.ps1 -Install`, hoặc `NETLOAD` tệp `dist\PipingAgent.bundle\Contents\net8\PipingAgent.AutoCAD.dll`. Thẻ ribbon `Piping Agent` có đủ các nút.

Luồng chính, mỗi bước một lệnh:

1. `PA_ISO`: mở tờ iso, Enter để lấy tờ đang mở. Lệnh đọc, dựng đồ thị, gán kích thước, kiểm kê theo bảng vật tư và ghi `<tờ>.kiem-ke.md` cùng `<tờ>.ir.json` cạnh tờ. Còn thiếu thì dừng ở đây.
2. `PA_BUILD`: dựng 3D. `Moi` vào tệp `<tên>-3D.dwg`, `Hientai` vào bản đang mở trên layer `PA-<tuyến>-<loại>`, một lần Undo bỏ hết.
3. `PA_PORT`: chọn một khối để xem cổng nối và khối đối diện. Vòng xanh là cổng đã nối, vòng đỏ là cổng hở.

Nhiều tờ cùng tuyến: `dotnet run -c Release --project src\PipingAgent.Cli -- iso-merge <a.ir.json> <b.ir.json> --out <gộp.ir.json>` rồi `PA_BUILDIR` nạp tệp gộp.

Mặt bằng 2D, chức năng phụ: `PA_CONVERT` nhận diện theo hồ sơ quy ước, `PA_CHECK` chạy luật, `PA_FIX` và `PA_WAIVE` sửa hoặc bỏ qua, `PA_RERUN` chạy lại với quyết định đã lưu trong `<bản vẽ>.pa.json`, `PA_PANEL` mở bảng điều khiển. Trợ lý: `PA_ASK` hỏi agent về bản vẽ, `PA_AGENTCONFIG` chọn mô hình, `PA_HELLO` kiểm tra plug-in. Hồ sơ và nhật ký ở `%LOCALAPPDATA%\PipingAgent`.

## Dòng lệnh ngoài AutoCAD

```powershell
dotnet run -c Release --project src\PipingAgent.Cli -- iso <tờ.pdf|dwg|dxf> [--out <ir.json>] [--force]
dotnet run -c Release --project src\PipingAgent.Cli -- iso-merge <a.ir.json> <b.ir.json> ... --out <gộp.ir.json>
dotnet run -c Release --project src\PipingAgent.Cli -- recognize <bản vẽ 2D> data\profiles\sample-pump-station.json [--out <ir.json>] [--bom <ra.csv>] [--report <ra.md>] [--expected <expected.json>]
dotnet run -c Release --project src\PipingAgent.Cli -- batch <thư mục bản vẽ> --profile <hồ sơ.json> --out <thư mục ra>
```

`iso` in chuỗi đoạn ống theo trục, kích thước đã gán, kiểm tra khép kín tại các neo toạ độ và bản kiểm kê; mã thoát 1 khi kiểm kê còn thiếu, trừ khi có `--force`.

## Mã nguồn

| Thư mục | Nội dung |
|---|---|
| `src/PipingAgent.Core` | Lõi, .NET Standard 2.0, không tham chiếu CAD, giao diện, mạng: hình học, Piping IR, kế hoạch dựng và cổng nối, hồ sơ quy ước, luật, danh mục |
| `src/PipingAgent.Iso` | Isometric: đọc PDF vector bằng PdfPig và DWG/DXF bằng ACadSharp, đồ thị trên giấy, gán kích thước, toạ độ thật từ neo E, N, EL, kiểm kê theo bảng vật tư, `IsoPipeline` |
| `src/PipingAgent.Dxf` | Bộ đọc DXF và DWG cho mặt bằng, chạy ngoài AutoCAD |
| `src/PipingAgent.Hosting` | Đọc hồ sơ quy ước, danh mục, thư viện thiết bị từ đĩa |
| `src/PipingAgent.Tools`, `src/PipingAgent.AgentHost` | Công cụ chỉ đọc cho agent và tiến trình agent riêng, JSON-RPC qua named pipe, kết nối điểm cuối kiểu OpenAI |
| `src/PipingAgent.UI` | Bảng điều khiển WPF |
| `src/PipingAgent.AutoCAD` | Plug-in, hai bản net48 và net8.0-windows; bản net48 chưa thử trên AutoCAD cũ |
| `src/PipingAgent.Cli` | Dòng lệnh |
| `schemas` | `piping-ir-1.0.json`, hợp đồng IR |
| `data/catalog` | ASME B36.10, B16.9, B16.10, DN15 đến DN600. Số liệu chép tay, chưa được chuyên gia ký, tệp ghi `verified: false`. Bảng JIS 10K trong mã chưa kiểm chứng |
| `data/profiles`, `data/equipment` | Hồ sơ quy ước và thư viện thiết bị cho mặt bằng 2D, chép cạnh DLL khi dựng |
| `tests/*` | xUnit cho lõi, bộ đọc DXF và kiến trúc. Chưa có test cho Iso, plug-in, CLI |
| `tools` | `dxf_survey.py` khảo sát layer, loại đối tượng, block, chữ của một DXF mới |
| `data/private`, `docs/dataTemplate` | Bản vẽ khách hàng chưa ẩn danh. Đã loại khỏi kho mã. Không bao giờ commit |

Prototype Python, bộ slide và video gọi vốn thời kỳ mặt bằng 2D đã chuyển ra ngoài kho mã.

## Dựng và kiểm thử

Cần .NET SDK 9. `build\build.ps1` restore, build, test; trên máy có AutoCAD 2025 còn nạp plug-in vào `accoreconsole` và chạy `PA_HELLO`, thêm `-SkipAutoCad` để bỏ qua. Máy chủ GitHub Actions chạy đúng kịch bản này.

Thử lệnh không cần giao diện:

```powershell
build\test_plugin.ps1 -Dwg <tờ.dwg> -Command "PA_ISO`n`nPA_BUILD`nMoi`n"
```

Kịch bản chép bản dựng vào `src\PipingAgent.AutoCAD\bin` rồi nạp bằng `accoreconsole`; thư mục đó phải nằm trong `TRUSTEDPATHS` của AutoCAD vì console không hiện được hộp thoại hỏi tin cậy. `accoreconsole` không có visual style nên không kiểm được phần hiển thị Shaded.
