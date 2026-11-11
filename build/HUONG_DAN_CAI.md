# Hướng dẫn cài Piping Agent cho AutoCAD

Bản thử nghiệm nội bộ. Chưa có chữ ký số, chưa có bộ cài tự động.

## 1. Yêu cầu

- AutoCAD 2025 trở lên trên Windows 10 hoặc 11, 64 bit. Bản cho AutoCAD 2021 đến 2024 có trong gói nhưng chưa được thử.
- Quyền ghi vào thư mục `%APPDATA%\Autodesk\ApplicationPlugins` của người dùng hiện tại. Không cần quyền quản trị.

## 2. Cài

1. Đóng AutoCAD.
2. Giải nén tệp zip. Trong đó có thư mục `PipingAgent.bundle`.
3. Chép nguyên thư mục `PipingAgent.bundle` vào `%APPDATA%\Autodesk\ApplicationPlugins`. Mở Windows Explorer, gõ đường dẫn đó vào thanh địa chỉ nếu chưa thấy thư mục.
4. Mở AutoCAD. Dòng lệnh phải hiện `Piping Agent <phiên bản> đã nạp`. Nếu AutoCAD hỏi có tin tệp này không, chọn Load.
5. Gõ `PA_HELLO` để kiểm tra. Gõ `PA_PANEL` để mở bảng điều khiển.

Nếu không thấy dòng đã nạp, kiểm tra biến `SECURELOAD`: gõ `SECURELOAD`, giá trị 1 là bình thường vì thư mục `ApplicationPlugins` được tin cậy sẵn. Giá trị 2 chặn mọi plug-in ngoài danh sách, cần quản trị viên thêm thư mục.

## 3. Dùng lần đầu

1. Mở một bản vẽ mặt bằng đường ống đã lưu thành tệp DWG.
2. Gõ `PA_PANEL`, vào thẻ Hồ sơ, chọn hồ sơ quy ước. Gói có sẵn `sample-pump-station` cho bản vẽ mẫu và `nsrp-piping-layout` cho bản vẽ thử của dự án. Bản vẽ khác quy ước thì soạn hồ sơ mới ngay trong thẻ này: gán vai trò cho layer, thử mẫu nhãn, xuất ra tệp.
3. Gõ `PA_CONVERT`. Bảng điều khiển hiện số đối tượng theo vai trò, tuyến ống, vấn đề. Bấm một dòng để phóng tới chỗ đó trên bản vẽ.
4. Sửa chỗ nhận diện sai bằng `PA_FIX`, bỏ qua vấn đề không đáng kể bằng nút Bỏ qua kèm lý do. Mọi chỉnh sửa lưu vào tệp `<tên bản vẽ>.pa.json` cạnh bản vẽ và được áp lại khi mở sau.
5. Khi không còn vấn đề mức chặn, gõ `PA_BUILD`, chọn `Moi` để dựng vào bản vẽ mới `<tên>-3D.dwg` hoặc `Hientai` để dựng vào bản đang mở. Một lần Undo bỏ toàn bộ phần đã dựng.

## 3a. Thẻ ribbon Piping Agent

Sau khi nạp, ribbon có thêm thẻ `Piping Agent` với ba panel theo đúng kiểu panel chuẩn của AutoCAD: nút lớn cho lệnh chính, cột nút nhỏ ba hàng cho lệnh phụ, biểu tượng nét phẳng đổi màu theo theme sáng tối (`COLORTHEME`).

- Isometric sang 3D: nút lớn `Đọc tờ ISO`; nút tách `Dựng 3D` (bấm phần trên dựng vào bản vẽ mới, mũi tên chọn Bản đang mở hoặc Xem trước); cột nhỏ `Cổng nối`, `Nạp IR`, `Xuất JSON`.
- Mặt bằng: nút tách `Nhận diện` (cả bản vẽ hoặc trong cửa sổ); nút lớn `Kiểm tra`; cột nhỏ `Sửa`, `Bỏ qua`, `Chạy lại`.
- Trợ lý: nút lớn `Bảng điều khiển`; cột nhỏ `Hỏi agent`, `Cấu hình agent`, `Kiểm tra plug-in`.

Di chuột lên nút để xem chú thích, giữ thêm một chút để xem phần giải thích dài. Bấm `Alt` rồi `PA` để vào thẻ bằng bàn phím, mỗi nút có phím tắt riêng hiện khi bấm `Alt`. Nút chỉ gửi lệnh vào dòng lệnh, riêng nút tách đặt sẵn câu trả lời cho câu hỏi chế độ nên không hỏi lại. Không thấy thẻ thì gõ `PA_RIBBON`; ribbon đang tắt thì gõ `RIBBON` trước.

## 3b. Tờ isometric sang 3D

Cách nạp nhanh khi đang phát triển, không cần chép vào `ApplicationPlugins`: gõ `NETLOAD`, chọn `dist\PipingAgent.bundle\Contents\net8\PipingAgent.AutoCAD.dll`. AutoCAD có thể hỏi có tin tệp không, chọn Load. Nếu báo `Unable to load`, thư mục chưa nằm trong `TRUSTEDPATHS`; thêm bằng tay trong Options, thẻ Files, hoặc chép bundle vào `ApplicationPlugins` theo mục 2.

1. Mở tờ isometric ISOGEN DWG trong AutoCAD, hoặc mở bản vẽ trống bất kỳ.
2. Gõ `PA_ISO`. Nhấn Enter để lấy chính tờ đang mở, hoặc dán đường dẫn tới tệp DWG, DXF, PDF.
3. Đọc dòng kiểm kê. `đủ để dựng` là bước 1 xong; bản kiểm kê ghi ở `<tên tờ>.kiem-ke.md` cạnh tờ, IR ở `<tên tờ>.ir.json`. Còn `khoảng trống` thì lệnh hỏi `Dungvan/Thoi`, mặc định Thoi.
4. Gõ `PA_BUILD`, chọn `Moi` để ghi `<tên tờ>-3D.dwg` cạnh tờ và mở nó, hoặc `Hientai` để dựng vào bản đang mở. `Xemtruoc` chỉ in kế hoạch.
5. Bản 3D tự chuyển sang góc nhìn SW Isometric, kiểu hiển thị Shaded và thu phóng vừa mô hình; nếu vẫn thấy ống là bốn nét thì gõ `VSCURRENT` rồi `Shaded`. Layer đặt theo mẫu `PA-<tuyến>-<loại>`, ví dụ `PA-3E204-1-250A-SUS-PIPE`, `-ELBOW`, `-FLANGE`, `-VALVE`, `-FITTING`, tắt bật riêng được.

6. Tại mỗi mặt nối có một vòng tròn trên layer `PA-<tuyến>-PORT`: xanh lá là hai khối đã khớp, đỏ là cổng hở. Gõ `PA_PORT` rồi chọn một khối để xem nó nối với khối nào.

Gộp nhiều tờ: chạy `PipingAgent.Cli iso-merge a.ir.json b.ir.json@dE,dN,dZ --out gop.ir.json` ngoài AutoCAD, rồi trong AutoCAD gõ `PA_BUILDIR`, dán đường dẫn `gop.ir.json`, rồi `PA_BUILD`.

## 4. Gỡ

Xoá thư mục `%APPDATA%\Autodesk\ApplicationPlugins\PipingAgent.bundle` khi AutoCAD đã đóng. Tệp `.pa.json` cạnh bản vẽ và thư mục `%LOCALAPPDATA%\PipingAgent` có thể giữ hoặc xoá.

## 5. Khi gặp lỗi

Nhật ký ở `%LOCALAPPDATA%\PipingAgent\logs`, mỗi ngày một tệp. Khi báo lỗi, gửi kèm tệp nhật ký của ngày đó và tệp `.pa.json` của bản vẽ. Nhật ký không chứa nội dung bản vẽ.
