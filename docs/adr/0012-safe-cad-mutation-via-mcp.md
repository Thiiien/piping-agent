# Safe CAD Mutation via MCP (Cơ chế Can thiệp Bản vẽ An toàn cho Agent MCP)

## Bối cảnh và Quyết định

Agent kết nối qua MCP và Live CAD Bridge trước đây hoàn toàn chỉ đọc (ReadOnly) nhằm bảo vệ bản vẽ nguồn. Để hỗ trợ Agent tự động can thiệp sửa lỗi, gắn nhãn và đánh dấu nghi vấn trên bản vẽ AutoCAD, chúng tôi quyết định bổ sung nhóm công cụ `ToolTier.Action` nhưng tuân thủ nghiêm ngặt mô hình **2-Pha Staging & Human-in-the-Loop**:

1. **Luồng 2 pha**: Agent chỉ có thể phát hành đề xuất `cad_propose_mutation` (lưu vào bộ nhớ tạm Staging và highlight trên CAD). Thao tác ghi xuống bản vẽ chỉ xảy ra khi kỹ sư phê duyệt thông qua `cad_apply_mutation`.
2. **Phạm vi an toàn Giai đoạn 1**: Hỗ trợ 6 thao tác ngữ nghĩa và hình học có kiểm soát:
   - `add_text`: Thêm chữ/nhãn chú thích.
   - `modify_text`: Sửa nội dung nhãn/text.
   - `change_layer`: Chuyển đối tượng sang đúng layer phân loại.
   - `add_revision_cloud`: Vẽ mây sửa đổi khoanh vùng nghi vấn.
   - `waive_issue`: Miễn trừ cảnh báo quy tắc kỹ thuật.
   - `add_pipe`: Vẽ đoạn ống tiêu chuẩn (đường tim 2D kèm nhãn hoặc khối 3D ACIS) theo danh mục ASME B36.10 với tọa độ xác định hoặc Smart Defaults (tâm khung nhìn).
   - Tuyệt đối cấm xóa cứng (Hard Delete) thực thể nguồn và không di dời tùy tiện tọa độ các tuyến ống đã tồn tại.
3. **Giao dịch đơn lẻ & Khả năng hoàn tác (Atomic Undo Transaction)**: Toàn bộ thay đổi của một đề xuất được bọc trong một `Transaction` duy nhất và đánh dấu nguồn gốc XData `PIPING_AGENT`. Kỹ sư có thể hoàn nguyên 100% chỉ với một lệnh `UNDO`.
4. **Khóa bảo mật (Permission Guard)**: Mặc định tắt (Opt-in). Chỉ kích hoạt khi bật tùy chọn trong Cấu hình AI hoặc truyền cờ `--allow-action`.

## Các lựa chọn đã bị bác bỏ
- **Bác bỏ Direct Autonomous Mutation (Agent tự ý sửa trực tiếp không qua duyệt)**: Quá rủi ro đối với hồ sơ thiết kế kỹ thuật công nghiệp khi mô hình LLM có thể gặp hiện tượng hallucination.
- **Bác bỏ Hard Delete Entity**: Xóa vật lý thực thể CAD nguồn làm phá vỡ tính truy vết nguồn gốc (Provenance) và khiến việc tái nhận diện tuyến ống gặp lỗi.
