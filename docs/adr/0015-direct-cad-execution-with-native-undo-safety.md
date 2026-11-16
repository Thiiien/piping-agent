# Direct CAD Execution with Native Undo Safety (Thực thi Trực tiếp Can thiệp CAD với Cơ chế Hoàn tác Gốc)

## Bối cảnh và Quyết định

Trong quá trình trải nghiệm thực tế trên AutoCAD, mô hình 2-Pha Staging cứng nhắc (Agent phát hành `cad_propose_mutation` -> Dừng lại chờ người dùng gõ lệnh `cad_apply_mutation` kèm mã hex proposal_id) gây ra các trở ngại lớn về trải nghiệm kỹ sư:
1. Kỹ sư ra lệnh trực tiếp (ví dụ: *"vẽ cho tôi một đoạn ống tiêu chuẩn mẫu"* hay *"vẽ đoạn ống 2m"*), nhưng Agent lại dừng lại và yêu cầu người dùng phải tự sao chép chuỗi mã định danh `prop-xxxxxxxx` để phê duyệt.
2. Việc người dùng phải gõ thủ công dẫn đến lỗi chính tả (như gõ nhầm `pro-` thay vì `prop-`), làm đứt gãy luồng tương tác và khiến lệnh vẽ thất bại.
3. Trong môi trường AutoCAD, mọi can thiệp của Piping Agent đều được bọc trong một `Transaction` duy nhất của Document Session và có sẵn cơ chế hoàn tác gốc (`UNDO` / `Ctrl+Z`), do đó kỹ sư hoàn toàn có khả năng hoàn nguyên bản vẽ tức thì và an toàn 100%.

Để giải quyết triệt để vấn đề này, chúng tôi quyết định mở rộng kiến trúc can thiệp với các nguyên tắc sau:

1. **Thực thi Trực tiếp (Direct CAD Execution via `auto_apply: true`)**:
   - Công cụ `cad_propose_mutation` được bổ sung cờ `auto_apply` (mặc định `false`).
   - Khi kỹ sư ra lệnh vẽ/sửa đổi trực tiếp, Agent tự động áp dụng Smart Defaults và kích hoạt `auto_apply=true`.
   - Hệ thống tạo đề xuất, gắn Document Target Binding, và **thực thi commit trực tiếp vào bản vẽ CAD trong cùng một bước duy nhất**, không yêu cầu người dùng phải gõ mã xác nhận.
   - Toàn bộ quy trình kiểm toán L2/L3 và thu thập bằng chứng vật lý (`MutationEvidence`) vẫn diễn ra đầy đủ trước khi hoàn tất giao dịch.

2. **Bảo toàn An toàn qua Hoàn tác Gốc (Native Undo Safety)**:
   - Dựa trên cơ chế AutoCAD Database Transaction (`db.TransactionManager.StartTransaction()`) với `Document.LockDocument()`.
   - Khi commit thành công, giao dịch được ghi thành một bản ghi hoàn tác nguyên tử trên bản vẽ. Kỹ sư có thể nhấn **Ctrl+Z** hoặc gõ lệnh **U / UNDO** trong AutoCAD bất kỳ lúc nào để hoàn tác toàn bộ các thực thể vừa vẽ mà không làm hỏng dữ liệu gốc.
   - Agent luôn phản hồi kèm thông báo rõ ràng về tính năng hoàn tác này.

3. **Phân giải Đề xuất Dung sai (Resilient Proposal Resolution)**:
   - Trong trường hợp người dùng vẫn sử dụng lệnh `cad_apply_mutation`, hệ thống không còn đối chiếu cứng nhắc chuỗi mã.
   - Hỗ trợ đối chiếu không phân biệt hoa thường, tự động loại bỏ tiền tố (`pro-` vs `prop-`), và tìm kiếm mờ (fuzzy match).
   - Nếu hàng đợi chỉ có duy nhất 1 đề xuất đang chờ (`pending`), hệ thống tự động suy diễn và áp dụng đề xuất đó ngay cả khi người dùng không truyền `proposal_id`.

## Các lựa chọn đã bị bác bỏ

- **Bác bỏ việc bắt buộc kỹ sư phải copy-paste proposal ID**: Tạo ma sát người dùng không cần thiết và dễ xảy ra lỗi gõ nhầm trên giao diện chat nhỏ.
- **Bác bỏ việc xóa bỏ hoàn toàn Staging Protocol**: Cơ chế Staging vẫn được giữ nguyên cho các trường hợp kỹ sư yêu cầu xem trước (khảo sát đề xuất với `auto_apply=false`) hoặc can thiệp hàng loạt phức tạp.
