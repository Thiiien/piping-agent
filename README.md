# Prototype: bản vẽ đường ống 2D sang mô hình 3D

Bộ tài liệu demo cho nhà đầu tư. Mọi hình ảnh trong video và slide được dựng từ dữ liệu do prototype tạo ra.

Tài liệu dự án đầy đủ, gồm đặc tả yêu cầu, kiến trúc và lộ trình: [`docs/TAI_LIEU_DU_AN.md`](docs/TAI_LIEU_DU_AN.md).

Kế hoạch sprint và danh sách việc: [`docs/KE_HOACH_SPRINT.md`](docs/KE_HOACH_SPRINT.md), bản bảng tính ở [`docs/backlog.csv`](docs/backlog.csv).

## Chuyển hướng: isometric sang 3D

Từ 10/11/2026 sản phẩm thu hẹp thành isometric sang 3D, nhận PDF vector và DWG. Lý do, khảo sát và kế hoạch ở [`docs/CHUYEN_HUONG_ISOMETRIC.md`](docs/CHUYEN_HUONG_ISOMETRIC.md). Phần mặt bằng 2D giữ làm chức năng phụ.

```powershell
dotnet run -c Release --project src\PipingAgent.Cli -- iso <isometric.pdf> --out <ir.json>
```

In chuỗi đoạn ống theo trục, kích thước đã gán, kiểm tra khép kín tại các neo toạ độ, rồi ghi IR 3D. Trong AutoCAD gõ `PA_BUILDIR` chọn tệp IR rồi `PA_BUILD` để dựng solid đúng toạ độ E, N, EL.

## Mã nguồn sản phẩm

Khung dự án C# theo mục 6.10 của tài liệu dự án. Hiện có kết quả Sprint 1 và Sprint 2: lõi dữ liệu, hai bộ đọc bản vẽ cho cùng kết quả, hồ sơ quy ước, phân loại đối tượng, đọc nhãn, làm sạch hình học, dựng đoạn chạy và gắn nhãn kèm điểm tin cậy, danh mục ống ASME, bảng điều khiển năm thẻ trong AutoCAD. Sprint 3 thêm đồ thị kết nối 3D với ống đứng, co, tê và kích thước phụ kiện, cùng lệnh `PA_BUILD` dựng solid trong AutoCAD. Sprint 4 thêm van từ block, thư viện thiết bị ba lớp với biểu thức an toàn, đầu chờ, giá đỡ, khung bộ luật, và quy trình `PA_CONVERT`, `PA_CHECK`, `PA_BUILD` có khoá khi còn vấn đề chặn. Sprint 5 thêm cơ chế quyết định lưu trong tệp phiên `<bản vẽ>.pa.json`, các lệnh `PA_FIX`, `PA_WAIVE`, `PA_RERUN`, thẻ Tuyến ống và trình soạn hồ sơ trong bảng. Sprint 6 có xuất khối lượng CSV, báo cáo Markdown, lệnh `batch` xử lý nhiều bản vẽ với bộ chấm điểm hồi quy theo `*.expected.json`, và gói zip cài thủ công `build\pack_bundle.ps1 -Zip` kèm `build\HUONG_DAN_CAI.md`. Sprint 7 mở đầu giai đoạn 2: lớp công cụ `PipingAgent.Tools` với 12 công cụ chỉ đọc, tiến trình `PipingAgent.AgentHost` nói JSON-RPC qua named pipe, bộ kết nối cho mọi điểm cuối kiểu OpenAI, lệnh `PA_ASK` hỏi agent về bản vẽ và `PA_AGENTCONFIG` cài đặt mô hình. Trên máy phát triển dùng Hermes Agent của Nous Research làm cổng: `hermes proxy start` mở `http://127.0.0.1:8645/v1`.

Lệnh trong AutoCAD: `PA_CONVERT` hoặc `PA_SCAN` quét bản vẽ hoặc tập đã chọn, chạy nhận diện theo hồ sơ quy ước đang chọn và in báo cáo, `PA_CHECK` chạy lại bộ luật và in vấn đề theo luật, `PA_FIX` sửa kết quả nhận diện với từ khoá Nhan, Dn, Caodo, Nut, Caukien, Noi, Tach, Boqua rồi chạy lại, `PA_WAIVE` bỏ qua một vấn đề có lý do, `PA_RERUN` chạy lại với các quyết định hiện có, `PA_SCANWINDOW` làm vậy trong một cửa sổ, `PA_BUILD` dựng ống, co, tê dạng solid từ kết quả quét gần nhất với từ khoá `Xemtruoc` chỉ đếm, `Moi` dựng vào bản vẽ mới `<tên>-3D.dwg`, `Hientai` dựng vào bản đang mở trên layer `PA-<tuyến>-<loại>` và một lần Undo bỏ hết, `PA_EXPORT` ghi đối tượng ra JSON, `PA_PANEL` mở bảng điều khiển với các thẻ Tổng quan, Vấn đề, Tuyến ống, Hồ sơ, Nhật ký, `PA_HELLO` kiểm tra nhanh. Bấm một dòng ở thẻ Tuyến ống hoặc Vấn đề thì bản vẽ phóng tới và tô sáng bằng đồ hoạ tạm thời. Hồ sơ quy ước chọn ở thẻ Hồ sơ, nhớ cho lần sau ở `%LOCALAPPDATA%\PipingAgent\settings.json`. Nhật ký ở `%LOCALAPPDATA%\PipingAgent\logs`.

Công cụ dòng lệnh, chạy ngoài AutoCAD với tệp DXF hoặc DWG:

```powershell
dotnet run -c Release --project src\PipingAgent.Cli -- summary <bản vẽ>
dotnet run -c Release --project src\PipingAgent.Cli -- dump <bản vẽ> <ra.json> [--explode]
dotnet run -c Release --project src\PipingAgent.Cli -- compare <a.json> <b.json>
dotnet run -c Release --project src\PipingAgent.Cli -- recognize <bản vẽ> data\profiles\sample-pump-station.json [--out <ir.json>] [--bom <ra.csv>] [--report <ra.md>] [--expected <expected.json>]
dotnet run -c Release --project src\PipingAgent.Cli -- batch <thư mục bản vẽ> --profile <hồ sơ.json> --out <thư mục ra>
```

Lệnh `recognize` chạy chuỗi nhận diện ngoài AutoCAD và in báo cáo: tỷ lệ phân loại, layer còn thiếu quy tắc, số nhãn khớp mẫu, số đoạn chạy, vấn đề theo luật. Mã thoát 1 nếu có vấn đề mức chặn.

Đóng gói plug-in để AutoCAD tự nạp: `build\pack_bundle.ps1` tạo `dist\PipingAgent.bundle`. Chép thư mục đó vào `%APPDATA%\Autodesk\ApplicationPlugins`, hoặc chạy `build\pack_bundle.ps1 -Install`, rồi khởi động lại AutoCAD.

| Thư mục | Nội dung |
|---|---|
| `src/PipingAgent.Core` | Lõi xử lý, .NET Standard 2.0. Không tham chiếu CAD, giao diện, mạng. Có hình học, Piping IR, tuần tự hoá JSON, đối tượng nguồn trung lập |
| `schemas` | Lược đồ JSON của các hợp đồng công khai, hiện có `piping-ir-1.0.json` |
| `src/PipingAgent.Hosting` | Lớp chủ dùng chung: đọc hồ sơ quy ước và danh mục từ đĩa, việc mà lõi không được làm |
| `data/profiles` | Hồ sơ quy ước: `base-default.json` làm nền, `sample-pump-station.json` cho bản vẽ mẫu, `nsrp-piping-layout.json` thử cho bản vẽ thật. Chép cạnh tệp DLL của plug-in khi dựng |
| `data/catalog` | Danh mục ống ASME B36.10, phụ kiện ASME B16.9, chiều dài van ASME B16.10, DN15 đến DN600, sinh từ `tools/make_catalog.py`. Số liệu chép tay, chưa được chuyên gia ký, tệp ghi `verified: false` |
| `data/equipment` | Thư viện thiết bị lớp mặc định: `tank_vertical`, `pump`, `generic_box`, `exchanger_horizontal`. Lớp công ty ở `%PROGRAMDATA%\PipingAgent\equipment`, lớp dự án ở `%LOCALAPPDATA%\PipingAgent\equipment`, lớp sau ghi đè theo `id` |
| `src/PipingAgent.Tools` | Hợp đồng công cụ cho agent: định nghĩa, lược đồ chặt, sổ đăng ký, 12 công cụ chỉ đọc trên bản vẽ, IR và danh mục |
| `src/PipingAgent.Dxf` | Bộ đọc DXF và DWG bằng ACadSharp, chạy ngoài AutoCAD |
| `src/PipingAgent.Iso` | Isometric: đọc PDF vector bằng PdfPig, đồ thị trên giấy, gán kích thước theo hướng chữ hoặc đường chỉ, toạ độ thật từ neo E, N, EL, kiểm tra khép kín |
| `src/PipingAgent.UI` | Bảng điều khiển WPF dùng chung |
| `src/PipingAgent.AutoCAD` | Plug-in AutoCAD, dựng ra hai bản cho .NET Framework 4.8 và .NET 8 |
| `src/PipingAgent.AgentHost` | Tiến trình agent chạy riêng, JSON-RPC qua named pipe, vòng lặp gọi công cụ, bộ kết nối kiểu OpenAI. Cấu hình `agent.json` ba cấp, khoá mã hoá DPAPI. Chạy thử không cần AutoCAD: `PipingAgent.AgentHost --selftest "<câu hỏi>" --drawing <dxf>` |
| `src/PipingAgent.Cli` | Công cụ dòng lệnh |
| `tests/Core.Tests`, `tests/Dxf.Tests`, `tests/Architecture.Tests` | Kiểm thử đơn vị của lõi và bộ đọc DXF, kiểm tra kiến trúc. Kiểm thử bộ đọc trên bản vẽ thật tự bỏ qua khi máy không có tệp |
| `tools/DxfLibEval` | Công cụ đánh giá thư viện DXF, không thuộc sản phẩm. Nhận cả tệp `.dwg`. Kết quả ở `docs/DANH_GIA_THU_VIEN_DXF.md` |
| `tools/dxf_survey.py` | Khảo sát một tệp DXF: layer, loại đối tượng, block, chữ. Kết quả đợt 1 ở `docs/KHAO_SAT_BAN_VE_THAT.md` |
| `tools/analyze_tags.py` | Liệt kê dạng chữ chưa khớp mẫu nhãn trên một bản dump, đã che số, để mở rộng hồ sơ |
| `data/private`, `docs/dataTemplate` | Bản vẽ của khách hàng chưa ẩn danh. Cả hai thư mục đã bị loại khỏi kho mã. Không bao giờ commit |

Dựng và kiểm thử, cần .NET SDK 9 trở lên:

```powershell
build\build.ps1
```

Trên máy có AutoCAD 2025, kịch bản này nạp luôn plug-in vào `accoreconsole` và chạy `PA_HELLO` trên bản vẽ thật nếu có, hoặc bản vẽ mẫu. Thêm `-SkipAutoCad` để bỏ qua bước đó. Máy chủ dựng tự động không có AutoCAD nên tự bỏ qua.

Thử plug-in trong AutoCAD: gõ `NETLOAD`, chọn `src\PipingAgent.AutoCAD\bin\Release\net48\PipingAgent.AutoCAD.dll` với AutoCAD đến bản 2024, hoặc thư mục `net8.0-windows` với AutoCAD từ bản 2025. AutoCAD sẽ hỏi có tin tệp DLL không vì nó nằm ngoài thư mục tin cậy, chọn tải. Sau đó gõ `PA_HELLO`. Đã chạy được trên AutoCAD 2025 với bản .NET 8. Bản .NET Framework 4.8 chưa thử vì chưa có AutoCAD bản cũ.

Kiểm thử không cần giao diện:

```powershell
build\test_plugin.ps1 -Dwg data\private\A410B4001.dwg [-Command PA_SCAN]
build\test_build.ps1 -Dwg data\private\sample.dxf -Profile data\profiles\sample-pump-station.json
```

Kịch bản thứ hai quét, dựng vào bản vẽ hiện tại, đếm solid, Undo một lần rồi dựng lại có tiêm lỗi. Kết quả mong đợi trên bản vẽ mẫu: `SOLIDS=44`, `AFTERUNDO=none`, `AFTERFAIL=none`.

Kịch bản chép bản dựng vào `src\PipingAgent.AutoCAD\bin` rồi nạp bằng `accoreconsole`. Thư mục đó phải có trong biến `TRUSTEDPATHS` của AutoCAD, vì console không hiện được hộp thoại hỏi tin cậy và chỉ báo không nạp được. `TRUSTEDPATHS` không bao gồm thư mục con, trừ khi đường dẫn kết thúc bằng `\...`.

Xuất DWG sang DXF không cần mở AutoCAD, dùng cho công cụ đánh giá và khảo sát:

```powershell
& "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe" /i <tệp.dwg> /s <kịch bản.scr> /l en-US
```

với kịch bản gồm hai dòng `(setvar "FILEDIA" 0)` và `_.DXFOUT "<đường dẫn ra.dxf>" 16`. Tiến trình có thể không tự thoát sau khi ghi xong, tệp vẫn trọn vẹn.

## Kết quả

| File | Nội dung |
|---|---|
| `output/pitch_deck.pptx` | Bộ slide 13 trang, video demo nhúng ở trang 4 |
| `output/demo_2d_to_3d.mp4` | Video demo 56 giây, 1920x1080 |
| `prototype/data/piping_plan_2d.dxf` | Bản vẽ mặt bằng đường ống 2D mẫu (đầu vào) |
| `prototype/data/model_3d.dxf` | Mô hình 3D do prototype xuất ra, mở bằng AutoCAD |
| `prototype/data/model.json` | Mô hình dạng tham số: ống, co, tê, van, thiết bị, số liệu thống kê |

## Chạy lại

```bash
.venv/Scripts/python prototype/make_sample_drawing.py   # tạo bản vẽ 2D mẫu
.venv/Scripts/python prototype/convert_2d_to_3d.py      # đọc DXF 2D, dựng mô hình, xuất DXF 3D
.venv/Scripts/python prototype/render_video.py          # dựng video
.venv/Scripts/python prototype/render_video.py --deck-stills
.venv/Scripts/python prototype/render_hero.py           # ảnh cho slide
node deck/build_deck.js                                 # tạo slide
```

Số liệu thời gian trong video và slide lấy từ lần chạy `convert_2d_to_3d.py` gần nhất, nên cần dựng lại video và slide sau khi chạy lại bước này.

## Prototype làm được gì

`convert_2d_to_3d.py` chỉ đọc file DXF 2D và áp dụng các quy tắc cố định:

- Polyline trên layer `PIPE` là đường tâm ống, mỗi polyline một cao độ.
- Nhãn dạng `P-1011 DN150 EL+4100` trên layer `PIPE-TAG` cho số hiệu tuyến, đường kính và cao độ tâm ống. Nhãn gắn với ống mà đường dẫn chạm vào, nếu không có đường dẫn thì gắn với ống gần nhất.
- Hai đoạn khác cao độ gặp nhau trên mặt bằng được nối bằng ống đứng.
- Chỗ đổi hướng thành co 90 độ bán kính dài, chỗ ba nhánh thành tê.
- Block `VALVE_GATE` thành van trên ống mà nó nằm lên.
- Hình tròn trên layer `EQUIP` là bồn đứng (chiều cao từ chữ `H=...`), hình chữ nhật là bơm.
- Block `COLUMN` và đường trên layer `STRUCT` là giá đỡ ống, cao độ từ chữ `TOS EL+...`.

## Giới hạn hiện tại

- Bản vẽ đầu vào là bản vẽ mẫu tự tạo, theo đúng quy ước layer ở trên. Bản vẽ thật sẽ không sạch như vậy.
- Chưa có AI agent. Phần nhận diện hoàn toàn bằng quy tắc.
- Chưa chạy bên trong AutoCAD hay Smart 3D. Kết quả 3D xuất ra file DXF dạng lưới đa diện (polyface mesh), chưa phải khối solid hay đối tượng ống thông minh.
- File `model_3d.dxf` đã qua kiểm tra cấu trúc bằng thư viện ezdxf nhưng chưa được mở thử bằng AutoCAD trên máy này.
- Video là hình dựng từ dữ liệu của prototype, không phải quay màn hình AutoCAD.
