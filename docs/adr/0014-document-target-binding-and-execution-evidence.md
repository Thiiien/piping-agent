# Document Target Binding, Execution Evidence & CAD Health Doctor

## Bối cảnh và Quyết định

Tiếp thu các kinh nghiệm thực chiến từ dự án tự động hóa CAD 3D chuyên sâu `solidworks-automation-skill` (v2.1.1), `PipingAgent` bổ sung 3 cơ chế bảo vệ và kiểm chứng trọng yếu:

1. **Khóa liên kết tài liệu (Document Target Binding)**:
   - Trong môi trường làm việc đa tài liệu (MDI) của AutoCAD, kỹ sư có thể mở đồng thời nhiều bản vẽ (`Unit10.dwg`, `Unit20.dwg`).
   - Mỗi đề xuất can thiệp (`MutationProposal`) khi phát hành qua `cad_propose_mutation` được gắn chặt với danh tính của bản vẽ nguồn (`DrawingPath` và `DrawingFingerprint`).
   - Khi kỹ sư duyệt và gọi `cad_apply_mutation`, hệ thống bắt buộc kiểm tra xem tài liệu đang kích hoạt (`Document.Name`) có khớp với bản vẽ của đề xuất hay không. Nếu phát hiện tráo tab bản vẽ, giao dịch bị **chặn đứng lập tức** nhằm tránh thảm họa ghi đè nhầm hình học sang bản vẽ khác.

2. **Cấu trúc Bằng chứng Thực thi (Execution Evidence & Artifacts)**:
   - Thực hiện triết lý *"Tool success is not treated as task success"*: Một thao tác ghi vào CAD không được coi là hoàn tất nếu thiếu bằng chứng nghiệm thu vật lý.
   - Khi commit thành công, hệ thống sản sinh đối tượng `MutationEvidence` chứa:
     - Danh sách mã thực thể cố định (`Handle`) vừa sinh ra hoặc sửa đổi.
     - Tọa độ hộp bao hình học thực tế (`BoundingBox`: `MinPoint`, `MaxPoint`) đo được từ `GeometricExtents`.
     - Số liệu đo đạc thực tế (`MeasuredMetrics`: chiều dài ống, đường kính danh định, cao độ).
     - Tem thời gian (`AppliedAt`) và kết quả kiểm toán L2/L3.
   - Bằng chứng này được lưu vào lịch sử phiên và trả về trực tiếp trong payload của `cad_apply_mutation` để Agent và kỹ sư nghiệm thu độc lập.

3. **Bộ chẩn đoán sức khỏe hệ thống (CAD Health Doctor)**:
   - Cung cấp công cụ MCP `cad_doctor` và lệnh AutoCAD `PA_DOCTOR` cho phép rà soát nhanh 4 trụ cột hệ thống:
     - Trạng thái bản vẽ đang mở và số lượng thực thể.
     - Tính sẵn sàng của thư viện danh mục tiêu chuẩn ASME B36.10 / B16.9 / B16.10.
     - Cấu hình AI, mô hình chỉ định và quyền can thiệp (`AllowAction`).
     - Tình trạng máy chủ proxy (Hermes) hoặc điểm cuối OpenAI.

## Các lựa chọn đã bị bác bỏ

- **Bác bỏ việc áp dụng thao tác tự do không kiểm tra tên tài liệu (Free-floating Mutation)**: Nguy cơ kỹ sư vô tình chuyển tab AutoCAD trong lúc Agent đang sinh đề xuất dẫn đến việc vẽ sai vị trí vào bản vẽ không mong muốn.
- **Bác bỏ việc chỉ trả về thông báo chuỗi văn bản đơn thuần**: Không có danh sách Handle và tọa độ BoundingBox khiến Agent không thể tự kiểm tra lại sản phẩm của chính mình và phá vỡ chuỗi truy vết nguồn gốc (Provenance).
