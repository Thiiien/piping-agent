# Post-Mutation Verification & Environment Guardrails (Hậu kiểm Định L1-L4 và Vệ sĩ Môi trường CAD)

## Bối cảnh và Quyết định

Sau khi triển khai cơ chế can thiệp bản vẽ 2 pha Staging theo ADR-12, việc đảm bảo độ tin cậy tuyệt đối cho các thực thể CAD do Agent tạo ra hoặc chỉnh sửa là yêu cầu tối quan trọng trong hồ sơ thiết kế công nghiệp. Dựa trên phân tích các bài học thực chiến từ bộ kỹ năng CAD tự động hóa (`cad-skills`), chúng tôi quyết định bổ sung hệ thống **Hậu kiểm định L1-L4 (Post-Mutation Verification)** và **Vệ sĩ Môi trường (Environment Guardrails)** cho `PipingAgent`:

1. **Giữ vững kiến trúc C# .NET In-Process**:
   - Khác với các giải pháp chạy ngoài tiến trình dùng Python COM (`win32com`, `pyautocad`) hay nạp tệp AutoLISP (`.lsp`) dễ bị treo do bẫy dòng nhắc phụ của `SendCommand` hoặc nghẽn cuộc gọi COM khi AutoCAD bận (`RPC_RETRYLATER`), `PipingAgent` tiếp tục vận hành dưới dạng In-Process AutoCAD .NET Plugin qua `TransactionManager` thuần túy, đảm bảo an toàn bộ nhớ và tốc độ thực thi tức thì.

2. **Hệ thống Kiểm định 4 Cấp độ (Verification Hierarchy L1-L4)**:
   - **Cấp L1 (API Success)**: Transaction của AutoCAD commit thành công không ném ngoại lệ.
   - **Cấp L2 (Entity Count Audit)**: Tự động đếm và xác minh số lượng đối tượng sinh mới hoặc bị sửa đổi khớp chính xác với số lượng thao tác đã phê duyệt (ví dụ: `add_pipe` 2D sinh đúng 2 thực thể: `Line` và `DBText`).
   - **Cấp L3 (Geometric & Layer Audit)**: Kiểm toán từng đối tượng mới:
     - Xác nhận đối tượng nằm đúng layer mục tiêu (ví dụ `PA_PIPE`).
     - Xác nhận đối tượng hình học đạt độ chính xác kích thước (chiều dài đoạn ống đo được khớp với `LengthMm` trong dung sai < 0.1mm).
     - Xác nhận nội dung chuỗi văn bản của `DBText`/`MText` khớp với nội dung chỉ định.
   - **Cấp L4 (Visual Feedback & Screen Regeneration)**: Gọi `Editor.UpdateScreen()`, tái sinh đồ họa và duy trì highlight tạm thời trên CAD để kỹ sư đối soát trực quan trước mắt.

3. **Vệ sĩ Môi trường (Environment Guardrails)**:
   - **WCS Enforcement**: Mọi tọa độ điểm chèn (`startPt`, `endPt`, `Position`) và vector định hướng đều được tính toán và áp dụng theo Hệ tọa độ thế giới (WCS), loại bỏ hoàn toàn rủi ro lệch hình khi kỹ sư đang xoay trục tọa độ người dùng (UCS).
   - **Snap Independence**: Đảm bảo toàn bộ quá trình chèn thực thể được định nghĩa bằng cấu trúc dữ liệu hình học tuyệt đối, không mô phỏng thao tác chuột và hoàn toàn miễn nhiễm với chế độ bắt điểm đối tượng (OSNAP/OSMODE).

## Các lựa chọn đã bị bác bỏ

- **Bác bỏ niềm tin ngầm định cấp L1 (Implicit Trust)**: Chỉ dựa vào việc hàm không ném lỗi là cực kỳ nguy hiểm trong CAD, vì đối tượng có thể bị vẽ sai layer, sai tỷ lệ hoặc rỗng mà không làm gián đoạn transaction.
- **Bác bỏ hạ cấp sang Python COM / SendCommand**: Việc dùng SendCommand để gọi các lệnh biên tập như `_ERASE` hay `_AUDIT` thường xuyên dẫn đến tình trạng treo tiến trình vô tận khi AutoCAD kích hoạt các câu hỏi phụ ngoài kịch bản.
