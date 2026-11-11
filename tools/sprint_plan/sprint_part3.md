
## 6. Giai đoạn 2: agent, Sprint 7 đến 12

**Mục tiêu giai đoạn.** Agent lập được hồ sơ quy ước cho bộ bản vẽ chưa từng gặp và đề xuất cách xử lý các chỗ mơ hồ. Có bộ đánh giá đo được chất lượng. Chế độ không có agent vẫn chạy.

**Nguyên tắc xuyên suốt.** Bộ đánh giá được làm song song với agent từ Sprint 8, không để cuối. Không có bộ đánh giá thì không biết một lần sửa lời nhắc là tốt lên hay xấu đi.

**Lưu ý.** Danh sách dưới đây phải được rà lại ở việc S6-14, sau khi có kết quả thử nghiệm của giai đoạn 1.

### Sprint 7: lớp công cụ và kết nối mô hình ngôn ngữ

**Mục tiêu.** Agent Host chạy thành tiến trình riêng, gọi được mô hình ngôn ngữ, và mô hình gọi được công cụ chỉ đọc trên bản vẽ đang mở.

**Trình diễn cuối sprint.** Trong AutoCAD, gửi câu hỏi "bản vẽ này có những layer nào chứa ống". Mô hình gọi công cụ tóm tắt bản vẽ và trả lời đúng. Nhật ký ghi đủ từng lệnh gọi.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S7-01 | Khung hợp đồng công cụ: tên, mô tả, lược đồ vào và ra ở chế độ chặt, lớp quyền chỉ đọc, đề xuất, hành động. Kiểm tra tham số trước khi chạy | Một công cụ mẫu đăng ký được. Tham số sai lược đồ bị từ chối kèm thông báo mô hình hiểu được | LÕI | 2 | S6-14 | Hạng mục 2.1, mục 8.3, ADR-09 |
| S7-02 | Các công cụ chỉ đọc trên bản vẽ: tóm tắt, danh sách layer, danh sách block, lấy mẫu đối tượng, lấy mẫu văn bản đã gom nhóm, truy vấn lân cận, ghi chú chung | Bảy công cụ ở mục 8.2 có kiểm thử. Kết quả có giới hạn kích thước | LÕI | 2 | S7-01 | Hạng mục 2.2, mục 8.2 |
| S7-03 | Các công cụ chỉ đọc trên IR và danh mục: tổng quan IR, chi tiết phần tử, danh sách vấn đề, ngữ cảnh một vấn đề, tra danh mục | Năm công cụ có kiểm thử | LÕI | 1 | S7-01 | Hạng mục 2.2 |
| S7-04 | Tiến trình Agent Host: ứng dụng chạy nền, vòng đời khởi động và tự tắt, giao thức JSON-RPC 2.0 qua kênh đặt tên giới hạn cho người dùng hiện tại | Hai tiến trình gọi nhau được. Tiến trình khác người dùng không kết nối được | LÕI | 1,5 | S7-01 | Hạng mục 2.3, ADR-04, mục 11.2 |
| S7-05 | Lớp trừu tượng nhà cung cấp và bộ kết nối đầu tiên dùng thư viện chính thức của Anthropic cho C#. Mô hình mặc định theo mục 8.6 | Một lượt hỏi đáp có gọi công cụ chạy trọn. Đổi mô hình chỉ bằng cấu hình | LÕI | 1,5 | S7-04 | Hạng mục 2.4, mục 8.7, FR-AGT-10 |
| S7-06 | Đầu plug-in của giao thức nội bộ: khởi chạy và dừng Agent Host, nhận lệnh gọi công cụ, đưa về luồng chính của AutoCAD, có giới hạn thời gian | Công cụ đọc bản vẽ chạy đúng luồng. AutoCAD không treo khi Agent Host chết | CAD | 2 | S7-04 | Mục 6.5, CON-02 |
| S7-07 | Hộp thoại cài đặt: nhà cung cấp, mô hình, điểm cuối. Khóa truy cập lưu bằng cơ chế bảo vệ thông tin xác thực của Windows | Khóa không xuất hiện trong tệp cấu hình và nhật ký | CAD | 1 | S7-05 | NFR-SEC-04, NFR-SEC-08 |
| S7-08 | Đọc đối tượng trong block lồng nhau và bản vẽ tham chiếu ngoài, ghi đường dẫn nguồn | Bản vẽ thật có ống nằm trong block được đọc đủ | CAD | 1,5 | S1-12 | FR-ING-03 |
| S7-09 | Cấu hình ba cấp máy, người dùng, dự án. Cấp sau ghi đè cấp trước | Kiểm thử ba cấp với cùng một khóa cấu hình | CAD | 1 | S5-06 | FR-CFG-02 |
| S7-10 | Hồ sơ kế thừa: hồ sơ dự án kế thừa hồ sơ công ty, chỉ ghi phần khác | Hồ sơ con rỗng cho kết quả giống hồ sơ cha | CAD | 1 | S2-01 | FR-PRF-05 |
| S7-11 | Sửa các lỗi còn tồn từ buổi thử giai đoạn 1 | Danh sách lỗi tồn về không hoặc được xếp ưu tiên lại | CAD | 1,5 | S6-13 | Hạng mục 1.19 |
| S7-12 | Lập tệp các chỗ mơ hồ và cách giải quyết đúng, kèm lý do, cho năm bản vẽ chuẩn đầu tiên | Năm tệp `ambiguities.json`. Có cả ca mà đáp án là "không đủ căn cứ, phải hỏi người" | ỐNG | 2,5 | S6-15 | Mục 13.2, 13.5 |
| S7-13 | Chốt với khách hàng thử nghiệm kiểu kết nối mô hình ngôn ngữ được phép. Kiểm tra điều kiện lưu dữ liệu của đúng mô hình và nền tảng | Câu trả lời bằng văn bản cho Q6. Phụ lục thỏa thuận về dữ liệu gửi tới mô hình | KH | 1 | S1-19 | Q6, R7, mục 8.6 |
| S7-14 | Tìm khách hàng thử nghiệm thứ hai có quy ước bản vẽ khác | Có nguồn bản vẽ thứ hai đồng ý cung cấp | KH | 1,5 | S6-17 | Hạng mục 2.13, mục 13.2 |

{{TONG:S7}}

**Tiến độ, cập nhật ngày 10/11/2026.** Người sáng lập chốt dùng Hermes Agent của Nous Research trên máy làm cổng mô hình: `hermes proxy start` mở điểm cuối kiểu OpenAI tại `http://127.0.0.1:8645/v1`, suy luận chạy trên Nous Portal. Tài khoản chưa có tín dụng cho mô hình trả phí nên dùng mô hình miễn phí `meituan/longcat-2.5:free`; `anthropic/claude-sonnet-5.5` qua cùng cổng khi có tín dụng, chỉ đổi cấu hình.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S7-01 | `PipingAgent.Tools`: `ToolDefinition`, `ToolRegistry`, `ToolResult`, `ToolSchema` kiểm tra lược đồ chặt tự viết, không thư viện ngoài. Lược đồ không có `additionalProperties: false` hoặc trường tuỳ chọn không nằm trong `required` bị từ chối khi đăng ký. Tham số sai trả kết quả lỗi kèm gợi ý cách gọi đúng. Kết quả quá 24.000 ký tự bị cắt kèm ghi chú |
| Xong | Việc S7-02 | Bảy công cụ chỉ đọc trên bản vẽ: tóm tắt, layer, block, mẫu đối tượng có phân trang, mẫu chữ gom theo dạng, lân cận theo handle, ghi chú chung đánh dấu là dữ liệu. Chưa có `drawing_render_region` và `drawing_get_legend`, thuộc Sprint 10 và 11 |
| Xong | Việc S7-03 | `catalog_lookup`, `ir_get_overview`, `ir_get_element`, `ir_list_issues`, `ir_get_issue_context`. 12 công cụ có kiểm thử |
| Xong | Việc S7-04 | `PipingAgent.AgentHost` tiến trình riêng, JSON-RPC 2.0 mỗi dòng một thông điệp trên named pipe `PipingAgent.<guid>` với ACL chỉ cho tài khoản Windows hiện tại; tự thoát khi plug-in đóng kênh, tiến trình cha chết, hoặc rảnh 10 phút. `JsonRpcPeer` dùng chung trong `PipingAgent.Hosting`. Chưa thử kết nối từ tài khoản khác |
| Xong, đổi hướng | Việc S7-05 | `ILlmProvider` với bộ kết nối `OpenAiCompatibleProvider` thay vì thư viện Anthropic, theo quyết định ngày 10/11/2026 dùng Hermes proxy; cùng mã chạy với Ollama, LM Studio hay cổng nội bộ. Đổi mô hình chỉ bằng `agent.json`. Vòng lặp agent: lịch sử chỉ nối thêm, kiểm tra lý do dừng, phát hiện gọi lặp, giới hạn bước, nhật ký từng lượt. Chưa truyền theo luồng |
| Xong | Việc S7-06 | `AgentClient` trong plug-in khởi chạy Agent Host, nhận `tool.call` và chạy công cụ ngay trên luồng chính trong lúc chờ, có hạn giờ. `PA_ASK` trong accoreconsole với câu hỏi của kế hoạch: mô hình gọi 9 công cụ, trả lời đúng layer `PIPE` kèm căn cứ, 57,6 giây, nhật ký `agent-<ngày>.jsonl` ghi đủ từng lệnh gọi và không chứa khoá |
| Xong | Việc S7-07 | `PA_AGENTCONFIG` ghi `%LOCALAPPDATA%\PipingAgent\agent.json`; khoá mã hoá DPAPI theo tài khoản, hoặc tên biến môi trường. Kiểm thử xác nhận khoá không xuất hiện trong tệp lẫn chuỗi mô tả. Chưa có hộp thoại, mới hỏi ở dòng lệnh |
| Xong một phần | Việc S7-08 | Block lồng nhau đã đọc được từ Sprint 1 với đường dẫn block. Xref chưa thử, bản vẽ thật không có xref |
| Xong | Việc S7-09 | `AgentConfig.Load`: máy `%PROGRAMDATA%`, người dùng `%LOCALAPPDATA%`, dự án `pipingagent.agent.json` cạnh bản vẽ; cấp sau ghi đè từng trường. Có kiểm thử |
| Xong | Việc S7-10 | Hồ sơ kế thừa `extends` đã làm ở Sprint 2 với kiểm thử hợp nhất |
| Chờ người ngoài | Việc S7-11 đến S7-14 | |

**Số đo tham khảo.** Cùng câu hỏi trên bản vẽ mẫu: `meituan/longcat-2.5:free` đúng, 9 lệnh gọi, 43 đến 58 giây, 17.000 đến 22.000 token vào; `upstage/solar-mini4:free` trả lời sau 1 lệnh gọi, 7 giây, xếp nhầm VALVE, EQUIP, STRUCT vào nhóm chứa ống; `stepfun/step-3.7-flash:free` bị giới hạn tần suất sau lệnh gọi đầu; `reflection/beam-1.0-501b-a23b:free` trả HTTP 400 với yêu cầu có công cụ.

**Điểm cần chú ý.**
- Trước khi viết mã gọi mô hình, đối chiếu tài liệu chính thức của nhà cung cấp. Tên mô hình, tham số và giá thay đổi nhanh.
- Nếu khách hàng trả lời S7-13 là cấm dịch vụ AI bên ngoài thì giai đoạn 2 vẫn làm, nhưng thử nghiệm agent diễn ra trên bản vẽ đã ẩn danh hoặc với khách hàng khác.

### Sprint 8: vòng lặp agent và chế độ lập hồ sơ

**Mục tiêu.** Agent tự khảo sát một bản vẽ và đề xuất hồ sơ quy ước. Người dùng theo dõi từng bước và duyệt.

**Trình diễn cuối sprint.** Mở bản vẽ mẫu, không có hồ sơ. Bấm "Đề xuất hồ sơ". Thẻ Trò chuyện hiện từng bước agent làm. Kết thúc, người dùng xem hồ sơ đề xuất kèm lý do từng ánh xạ, duyệt, rồi chạy chuyển đổi.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S8-01 | Vòng lặp agent: gọi mô hình, thực thi các lệnh gọi công cụ, gửi mọi kết quả của một lượt trong cùng một tin nhắn, xử lý đủ các lý do dừng gồm cả từ chối | Một phiên nhiều bước chạy trọn. Mỗi lý do dừng có nhánh xử lý và kiểm thử | LÕI | 2 | S7-05 | Hạng mục 2.3, mục 8.4 |
| S8-02 | Giới hạn phiên: số bước, số token, thời gian. Vượt thì dừng và báo cáo trạng thái | Phiên cố ý chạy dài bị dừng đúng ngưỡng, có bản tóm tắt | LÕI | 0,5 | S8-01 | FR-AGT-07 |
| S8-03 | Ghi vết: mỗi lệnh gọi công cụ và mỗi yêu cầu tới mô hình thành một bản ghi nhật ký, gồm số token vào, ra, đọc từ bộ nhớ đệm, thời gian, lý do dừng | Tái hiện được trình tự một phiên chỉ từ tệp nhật ký | LÕI | 1 | S8-01 | FR-AGT-08, mục 11.5 |
| S8-04 | Sắp xếp ngữ cảnh để dùng được bộ nhớ đệm lời nhắc theo mục 8.5. Đo số token đọc từ bộ nhớ đệm | Từ yêu cầu thứ hai trong phiên, số token đọc từ bộ nhớ đệm lớn hơn không | LÕI | 0,5 | S8-03 | Mục 8.5, R8 |
| S8-05 | Hai công cụ của chế độ lập hồ sơ: ghi hồ sơ nháp, và chạy thử hồ sơ nháp trả về chỉ số kèm mẫu các ca không khớp | Hồ sơ nháp không có hiệu lực cho đến khi người dùng duyệt | LÕI | 1,5 | S7-02, S6-01 | Hạng mục 2.5, FR-PRF-07 |
| S8-06 | Lời nhắc hệ thống và quy trình của chế độ lập hồ sơ, bản đầu. Chạy trên bản vẽ mẫu và hai bản vẽ thật | Trên bản vẽ mẫu, hồ sơ agent đề xuất cho kết quả nhận diện bằng hồ sơ viết tay | LÕI | 1 | S8-05 | FR-AGT-02, UC-03 |
| S8-07 | Khung bộ đánh giá: chạy một ca nhiều lần, ghi số bước, token, thời gian, chi phí, lưu toàn bộ chuỗi lệnh gọi | Một lệnh chạy một ca năm lần và in bảng kết quả có trung bình và độ dao động | LÕI | 1,5 | S8-03 | Hạng mục 2.9, mục 13.5 |
| S8-08 | Thẻ Trò chuyện: dòng tin nhắn, hiển thị agent đang làm gì theo từng bước, nút dừng | Bấm dừng thì phiên dừng trong vài giây và trạng thái được giữ | CAD | 2,5 | S7-06 | Hạng mục 2.7, FR-AGT-13 |
| S8-09 | Màn hình duyệt hồ sơ đề xuất: từng ánh xạ kèm lý do, cho chấp nhận, sửa, từ chối | Hồ sơ chỉ được lưu sau khi người dùng bấm duyệt | CAD | 2 | S8-05, S5-12 | FR-PRF-07, FR-AGT-05 |
| S8-10 | Chế độ tắt agent và mất kết nối: giao diện chuyển trạng thái rõ ràng, mọi chức năng không phải agent vẫn chạy | Rút mạng giữa phiên: có thông báo, không mất dữ liệu, chuyển đổi bằng hồ sơ vẫn chạy | CAD | 1 | S8-08 | FR-AGT-09, NFR-SEC-03 |
| S8-11 | Thử lại có giới hạn khi gọi mô hình lỗi. Hết lượt thì báo rõ và cho tiếp tục không có agent | Kiểm thử với lỗi giả lập: quá tải, hết thời gian, sai khóa | CAD | 1 | S8-01 | NFR-REL-06 |
| S8-12 | Kiểm tra giao diện AutoCAD không treo quá một giây trong lúc agent chờ mô hình | Thao tác phóng, xoay bản vẽ vẫn mượt khi phiên agent đang chạy | CAD | 1 | S8-08 | NFR-PRF-04 |
| S8-13 | Xác định nguồn môi trường Smart 3D để phát triển: của khách hàng, của đối tác hay tự mua | Có đường truy cập cụ thể và ngày bắt đầu dùng được | CAD | 0,5 | | Q9, mục 10.3.3 |
| S8-14 | Hoàn tất tệp chỗ mơ hồ cho các bản vẽ chuẩn còn lại. Chấm các hồ sơ agent đề xuất | Đủ tệp cho toàn bộ bộ bản vẽ chuẩn. Danh sách ánh xạ agent làm sai | ỐNG | 2,5 | S7-12 | Mục 13.5 |
| S8-15 | Nhận bản vẽ từ nguồn quy ước thứ hai, ký thỏa thuận | Từ năm bản vẽ trở lên của nguồn thứ hai | KH | 1,5 | S7-14 | Hạng mục 2.13 |

{{TONG:S8}}

**Điểm cần chú ý.**
- Mỗi lần chạy bộ đánh giá tốn tiền thật. Ước tính chi phí trước mỗi đợt chạy lớn và ghi vào nhật ký sprint.
- S8-13 phải xong trong sprint này. Thiếu môi trường Smart 3D thì khảo sát ở Sprint 9 không làm được và cả giai đoạn 3 trễ theo.

### Sprint 9: chế độ giải quyết mơ hồ

**Mục tiêu.** Agent xem các vấn đề mà bộ luật không tự quyết được, đề xuất quyết định kèm lý do và điểm tin cậy, hoặc hỏi người dùng khi thiếu căn cứ.

**Trình diễn cuối sprint.** Trên một bản vẽ thật có nhãn mơ hồ: agent đề xuất gắn nhãn, người dùng xem lý do, xem ảnh vùng bản vẽ agent đã xem, duyệt từng đề xuất. Mở màn hình "Dữ liệu đã gửi" để thấy chính xác nội dung đã gửi tới mô hình.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S9-01 | Các công cụ đề xuất: đề xuất quyết định, xem trước tác động của quyết định, đánh dấu cần người xem, hỏi người dùng có lựa chọn, kết thúc phiên có tóm tắt | Năm công cụ có kiểm thử. Đề xuất thiếu lý do hoặc điểm tin cậy bị từ chối | LÕI | 2 | S7-03, S5-05 | Hạng mục 2.6, mục 8.2 |
| S9-02 | Lời nhắc và quy trình của chế độ giải quyết mơ hồ. Mọi đề xuất ở trạng thái chờ duyệt | Agent xử lý các vấn đề của một bản vẽ thật từ đầu đến cuối. Không quyết định nào tự có hiệu lực | LÕI | 1,5 | S9-01 | FR-AGT-03, 05, 06, UC-04 |
| S9-03 | Hàng rào an toàn: văn bản lấy từ bản vẽ luôn được bọc và đánh dấu là dữ liệu. Kiểm tra quyền ở lớp công cụ. Tạo bản vẽ kiểm thử có cài cắm chỉ dẫn | Trên bản vẽ cài cắm, agent không làm theo chỉ dẫn và không có đề xuất nào xuất phát từ nó | LÕI | 1,5 | S9-02 | Mục 8.8, NFR-SEC-05, 06 |
| S9-04 | Bộ lọc dữ liệu gửi tới mô hình theo bảng ở mục 8.9: chỉ phần trích có cấu trúc, tối thiểu cho nhiệm vụ | Kiểm thử chứng minh tệp bản vẽ và các trường bị cấm không bao giờ nằm trong yêu cầu | LÕI | 1 | S8-03 | Mục 8.9, NFR-SEC-01 |
| S9-05 | Bộ ca đánh giá cho chế độ lập hồ sơ trên phần phát triển của bộ bản vẽ chuẩn. Chấm theo mục 13.5 | Bảng điểm: chỉ số nhận diện với hồ sơ của agent so với hồ sơ đúng, số ánh xạ phải sửa | LÕI | 1,5 | S8-07, S8-14 | Mục 13.5 |
| S9-06 | Chạy đo lần đầu để lấy mốc so sánh. Ghi chi phí và thời gian | Bảng mốc được lưu trong kho, mọi thay đổi sau này so với nó | LÕI | 0,5 | S9-05 | Mục 13.5 |
| S9-07 | Xuất ảnh một vùng bản vẽ cho agent, có đánh số các đối tượng đáng chú ý | Ảnh rõ ở vùng 20 m. Số trên ảnh khớp mã đối tượng trả về kèm | CAD | 2 | S7-06 | Hạng mục 2.8, FR-AGT-11 |
| S9-08 | Màn hình duyệt quyết định đề xuất: lý do, điểm tin cậy, nguồn quyết định, chấp nhận hoặc từ chối. Phân biệt rõ phần tử do quy tắc, do agent, do người | Mỗi phần tử trên giao diện cho biết ai quyết định | CAD | 2 | S9-01, S5-08 | FR-REV-05, NFR-USE-04 |
| S9-09 | Duyệt hàng loạt các đề xuất trên ngưỡng tin cậy. Không cho duyệt hàng loạt dưới ngưỡng | Ngưỡng cấu hình được. Đề xuất dưới ngưỡng phải duyệt từng cái | CAD | 0,5 | S9-08 | FR-REV-06, R4 |
| S9-10 | Giao diện cho câu hỏi của agent: hiện câu hỏi có lựa chọn, phóng tới chỗ liên quan, gửi trả lời | Agent nhận được trả lời và tiếp tục phiên | CAD | 1 | S8-08, S9-01 | FR-AGT-06 |
| S9-11 | Màn hình "Dữ liệu đã gửi": toàn bộ nội dung gửi tới mô hình trong phiên, gồm cả ảnh | Nội dung khớp từng byte với nhật ký yêu cầu | CAD | 1 | S8-03 | NFR-SEC-07 |
| S9-12 | Khảo sát Smart 3D: viết mã thử tạo một tuyến và định tuyến một đoạn chạy hoàn toàn bằng mã | Đoạn mã chạy được trong môi trường Smart 3D, hoặc danh sách cụ thể những gì API không cho làm | CAD | 1,5 | S8-13 | Hạng mục 3.1, R6 |
| S9-13 | Lập đáp án và tệp chỗ mơ hồ cho bản vẽ của nguồn quy ước thứ hai | Từ ba thư mục mới trong bộ bản vẽ chuẩn | ỐNG | 2,5 | S8-15 | Hạng mục 2.13 |
| S9-14 | Cho khách hàng thử nghiệm xem agent chạy. Ghi phản ứng về mức tin tưởng và về dữ liệu gửi đi | Ghi chú buổi làm việc. Danh sách lo ngại của khách hàng | KH | 1 | S9-02 | R4, R7 |

{{TONG:S9}}

**Điểm cần chú ý.**
- Cuối sprint là mốc M5. Kết quả S9-12 quyết định phạm vi giai đoạn 3. Nếu API không đủ, dùng phương án dự phòng qua tệp trung gian ở mục 10.3.3.
- Theo dõi tỷ lệ đề xuất sai có điểm tin cậy cao. Đây là dấu hiệu sớm của rủi ro R4.

### Sprint 10: đánh giá, tinh chỉnh và mở rộng nhận diện

**Mục tiêu.** Có bộ đánh giá đủ ba nhóm ca. Hoàn thành một vòng tinh chỉnh dựa trên số liệu. Nhận diện thêm co 45 độ và côn thu. Agent đọc được bảng chú giải ký hiệu.

**Trình diễn cuối sprint.** Trình bày bảng điểm trước và sau vòng tinh chỉnh. Trình diễn dựng lại từng phần: sửa cao độ một tuyến, chỉ tuyến đó được dựng lại. Chọn một ống 3D, đối tượng 2D nguồn được tô sáng.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S10-01 | Bộ ca đánh giá cho chế độ giải quyết mơ hồ và nhóm ca an toàn | Bảng điểm: tỷ lệ đề xuất đúng, tỷ lệ sai với tin cậy cao, tỷ lệ hỏi đúng lúc, kết quả nhóm ca an toàn | LÕI | 1,5 | S9-05, S9-13 | Mục 13.5 |
| S10-02 | Phân tích lỗi từ chuỗi lệnh gọi đã lưu. Sửa lời nhắc và mô tả công cụ, vòng một. Chạy lại bộ đánh giá | Bảng so sánh trước và sau. Mỗi thay đổi được giữ đều có số liệu chứng minh | LÕI | 2 | S10-01 | Mục 13.5 |
| S10-03 | So sánh các mô hình và mức nỗ lực suy luận trên bộ đánh giá: chất lượng, thời gian, chi phí theo việc hoàn thành | Bảng so sánh. Đề xuất mô hình và mức nỗ lực mặc định cho từng chế độ | LÕI | 1 | S10-01 | Mục 8.6, R8 |
| S10-04 | Báo cáo chi phí và thời gian cho mỗi bản vẽ, tính từ nhật ký | Con số đo được thay cho mục để trống ở mục 14.3 của tài liệu dự án | LÕI | 0,5 | S8-03 | Mục 14.3, R8 |
| S10-05 | Mở rộng phân loại nút: co 45 độ, côn thu, tê thu | Ca kiểm thử cho từng loại. Luật R-CON-04 không báo nhầm tại côn thu | LÕI | 2 | S3-05 | Hạng mục 2.11, FR-REC-08 |
| S10-15 | Công cụ đọc bảng chú giải ký hiệu: phát hiện vùng chú giải, trả về từng ô gồm tên block, chữ mô tả và ảnh. Đối tượng trong vùng chú giải không được coi là cấu kiện thật. Cập nhật lời nhắc chế độ lập hồ sơ để đọc chú giải trước | Trên bản vẽ có bảng chú giải, hồ sơ agent đề xuất ghi rõ ánh xạ nào lấy từ chú giải. Không có van lơ lửng sinh ra từ ký hiệu mẫu trong bảng | LÕI | 1 | S7-02, S8-06 | Hạng mục 2.11b, FR-PRF-09, mục 7.9.4 |
| S10-07 | Dựng co 45 độ, côn thu, tê thu trong AutoCAD | Hộp bao khớp tính toán trong 0,1 mm | CAD | 1,5 | S10-05 | FR-BLD-01 |
| S10-08 | Dựng lại từng phần: khi IR đổi thì chỉ xóa và dựng lại đối tượng 3D bị ảnh hưởng | Sửa một tuyến trên mô hình 500 đoạn ống: chỉ tuyến đó được dựng lại, không tạo đối tượng trùng | CAD | 2 | S3-15 | Hạng mục 2.12, FR-BLD-07 |
| S10-09 | Liên kết hai chiều giữa đối tượng 3D và đối tượng 2D nguồn | Chọn bên này thì bên kia được tô sáng, cả hai chiều | CAD | 1,5 | S3-15, S2-13 | FR-BLD-08 |
| S10-10 | Phát hiện đối tượng 3D đã bị sửa tay sau khi dựng. Hỏi trước khi ghi đè | Di chuyển một ống bằng tay rồi dựng lại: có cảnh báo liệt kê đối tượng bị ảnh hưởng | CAD | 1,5 | S10-08 | FR-BLD-12 |
| S11-08 | Thêm định nghĩa thiết bị: bồn nằm, thiết bị trao đổi nhiệt. Kiểm thử nhận diện | Hai tệp định nghĩa mới, thêm mà không sửa mã | CAD | 1 | S4-04 | FR-REC-11, mục 7.8 |
| S10-12 | Viết báo cáo khảo sát Smart 3D theo sáu mục ở mục 10.3.3 | Báo cáo kèm khuyến nghị về phạm vi giai đoạn 3 | CAD | 0,5 | S9-12 | Hạng mục 3.1, Q3 |
| S10-13 | Mở rộng bộ bản vẽ chuẩn lên khoảng 15 bản, trong đó có bản vẽ có bảng chú giải. Bổ sung số hiệu và thuộc tính cấu kiện vào đáp án. Phân loại nguyên nhân các đề xuất sai của agent | Đáp án có thuộc tính cấu kiện để chấm việc gắn chú thích. Bảng nguyên nhân: thiếu thông tin, hiểu sai quy ước, sai kiến thức ngành | ỐNG | 2,5 | S10-01 | Hạng mục 2.13 |
| S10-14 | Chuẩn bị buổi thử nghiệm giai đoạn 2: khách hàng nào, bộ bản vẽ chưa từng gặp nào, đo gì | Lịch và bộ bản vẽ được xác nhận | KH | 1 | S9-14 | Mục 5.11 |

{{TONG:S10}}

**Điểm cần chú ý.**
- Chỉ sửa lời nhắc theo phần phát triển của bộ bản vẽ chuẩn. Phần giữ lại chỉ dùng để chấm ở Sprint 12.
- Chỉ đổi sang mô hình rẻ hơn khi bảng ở S10-03 cho thấy chất lượng giữ được.

### Sprint 11: lệnh sửa bằng lời, ống hai nét và kiểm tra va chạm

**Mục tiêu.** Hoàn tất các chức năng còn lại của giai đoạn 2, gồm cả gắn chú thích với cấu kiện, để Sprint 12 chỉ còn đo và sửa.

**Trình diễn cuối sprint.** Gõ "nâng tuyến P-1011 lên cao độ 4500" trong thẻ Trò chuyện. Agent đề xuất thay đổi, người dùng xem trước rồi duyệt, mô hình 3D cập nhật. Chạy kiểm tra va chạm trên một bản vẽ có ống giao nhau.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S11-01 | Chế độ lệnh sửa: lời nhắc, quy trình, dùng lại các công cụ đề xuất quyết định trên IR | Năm câu lệnh mẫu cho ra đúng quyết định. Yêu cầu ngoài khả năng được từ chối rõ ràng | LÕI | 2 | S9-01 | Hạng mục 2.10, FR-AGT-04, UC-06 |
| S11-02 | Bộ ca đánh giá cho chế độ lệnh sửa: câu lệnh kèm trạng thái IR mong đợi | Bảng điểm cho chế độ thứ ba | LÕI | 1 | S11-01 | Mục 13.5 |
| S11-03 | Ống vẽ bằng hai nét song song: suy ra đường tâm và đường kính ngoài theo tỷ lệ | Ca kiểm thử trên bản vẽ thật có ống hai nét. Trường hợp không chắc thì tạo vấn đề | LÕI | 2,5 | S2-06 | Hạng mục 2.11, FR-CLS-04 |
| S11-04 | Ghi chú chung: agent đọc và đề xuất thay đổi tương ứng trong hồ sơ, ví dụ ngữ nghĩa cao độ mặc định | Trên bản vẽ có ghi chú "cao độ là đáy ống", agent đề xuất đúng thiết lập | LÕI | 1 | S8-06 | FR-REC-14 |
| S11-14 | Gắn chú thích chữ rời với cấu kiện: vai trò và mẫu chú thích trong hồ sơ, ba mức ưu tiên đường dẫn, nằm sát, gần nhất, điểm tin cậy và ngưỡng dùng chung với nhãn ống | Ca dựng sẵn: chú thích có đường dẫn gắn đúng. Hai van cách đều một chú thích thì ra vấn đề, không ra liên kết. Nhãn ống không bị lấy nhầm làm chú thích van | LÕI | 1,5 | S4-18, S2-09 | Hạng mục 2.11c, FR-REC-19, mục 7.9.5 |
| S11-06 | Kiểm tra va chạm cứng ống với ống, ống với thiết bị, ống với kết cấu. Ba luật R-CLH-01 đến 03 | Phát hiện đúng trên ca dựng sẵn. Không báo va chạm tại đầu chờ và tại nút nối | CAD | 2,5 | S4-10, S4-11 | Hạng mục 2.12, FR-CHK-05 |
| S11-07 | Bốn luật còn lại của giai đoạn 2: R-CON-06, R-GEO-03, R-ELV-04, R-EQP-03 | Mỗi luật có ca phát hiện đúng và ca không báo nhầm | CAD | 1 | S5-03 | Mục 9.3 |
| S11-15 | Ba luật chú thích cấu kiện: R-CMP-01 đến 03 | Mỗi luật có ca phát hiện đúng và ca không báo nhầm | CAD | 0,5 | S11-14 | Mục 9.3 |
| S11-16 | Hiển thị thuộc tính và nguồn gốc từng thuộc tính của cấu kiện trên bảng điều khiển. Cho gắn lại chú thích sang cấu kiện khác. Ghi số hiệu cấu kiện vào dữ liệu gắn trên đối tượng 3D và bảng khối lượng | Người dùng thấy số hiệu van lấy từ thuộc tính block hay từ chữ rời, và sửa được liên kết sai | CAD | 0,5 | S11-14, S5-08 | FR-REC-19, NFR-USE-04 |
| S11-09 | Giao diện lệnh sửa trong thẻ Trò chuyện: xem trước thay đổi trước khi duyệt | Người dùng thấy phần tử nào đổi và vấn đề nào sinh ra hay mất đi | CAD | 1,5 | S11-01, S9-08 | FR-AGT-04 |
| S11-10 | Công cụ ẩn danh bản vẽ của khách hàng: bỏ khung tên, đổi số hiệu theo bảng ánh xạ | Bản vẽ sau ẩn danh không còn tên khách hàng và tên dự án. Hình học không đổi | CAD | 1 | S1-07 | Mục 13.2 |
| S11-11 | Mở rộng kiểm thử tích hợp AutoCAD cho các chức năng giai đoạn 2 | Thêm ca cho dựng lại từng phần, liên kết hai chiều, phát hiện sửa tay | CAD | 1 | S10-10 | Mục 13.6 |
| S11-12 | Mở rộng bộ bản vẽ chuẩn lên từ 20 bản, từ ít nhất hai nguồn quy ước | Đủ 20 thư mục có đáp án và tệp chỗ mơ hồ | ỐNG | 2,5 | S10-13 | Hạng mục 2.13, mục 5.11 |
| S11-13 | Hỏi khách hàng thử nghiệm về mức giá chấp nhận được và cách tính tiền họ muốn | Ghi chú từ ít nhất hai khách hàng | KH | 1 | S9-14 | Q12 |

{{TONG:S11}}

**Điểm cần chú ý.**
- S11-03 chỉ làm khi bản vẽ thật có ống hai nét. Nếu không gặp, dời xuống danh sách chờ và dùng thời gian cho vòng tinh chỉnh agent.
- S11-06, S11-07 và S11-15 là việc của lõi, giao cho `CAD` để cân sức làm giữa hai người.
- Các điểm tin cậy ở S11-14 là điểm khởi đầu. Chỉnh lại theo đáp án đã bổ sung ở S10-13.

### Sprint 12: đo, sửa và nghiệm thu giai đoạn 2

**Mục tiêu.** Đo toàn bộ chỉ số trên bộ bản vẽ chuẩn từ 20 bản. Thử nghiệm agent với bộ bản vẽ chưa từng gặp. Nghiệm thu giai đoạn 2.

**Trình diễn cuối sprint.** Trình bày bảng chỉ số ở mục 5.2 trên phần giữ lại, bảng điểm agent, chi phí mỗi bản vẽ. Chạy trực tiếp: agent lập hồ sơ cho một bộ bản vẽ mà đội chưa từng xem.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S12-01 | Đo toàn bộ chỉ số nhận diện trên bộ bản vẽ chuẩn, tách phần phát triển và phần giữ lại | Bảng chỉ số so với mục tiêu ở mục 5.2, gồm tỷ lệ sai im lặng | LÕI | 1 | S11-12 | Mục 5.2, 13.4 |
| S12-02 | Vòng tinh chỉnh agent thứ hai, tập trung vào nhóm lỗi lớn nhất từ S10-13 | Bảng so sánh trước và sau trên phần phát triển | LÕI | 2 | S10-02, S11-02 | Mục 13.5 |
| S12-03 | Chỉnh ngưỡng tin cậy để cân bằng tỷ lệ sai im lặng và tỷ lệ cờ thừa, dựa trên thời gian duyệt đo được | Ngưỡng mặc định mới kèm số liệu giải thích | LÕI | 1 | S12-01 | Mục 13.4 |
| S12-04 | Sửa lỗi phát hiện trong buổi thử nghiệm, phần lõi và agent | Không còn lỗi chặn quy trình | LÕI | 2 | S12-08 | Mục 13.7 |
| S12-05 | Đo lại chi phí, thời gian, tỷ lệ dùng bộ nhớ đệm. Chốt mô hình mặc định | Cấu hình mặc định của bản phát hành | LÕI | 0,5 | S12-02 | Mục 8.6 |
| S12-06 | Cập nhật chương 8 và 13 của tài liệu dự án bằng số liệu thật | Các mục đánh dấu đề xuất được thay bằng số đo | LÕI | 0,5 | S12-01 | Mục 14.3 |
| S12-07 | Soạn bản nháp khai báo năng lực của ứng dụng chủ, chuẩn bị cho bộ thích ứng thứ hai | Lớp khai báo năng lực có trong lõi. Bộ thích ứng AutoCAD khai báo đúng năng lực của mình | LÕI | 1 | S10-12 | Mục 6.12.3 quy tắc 8 |
| S12-08 | Thử nghiệm với khách hàng: agent lập hồ sơ cho bộ bản vẽ chưa từng gặp. Đo thời gian thiết lập so với soạn hồ sơ bằng tay | Số phút thiết lập của hai cách. Số ánh xạ người dùng phải sửa | CAD | 2 | S10-14 | Mục 5.11, mốc quyết định giai đoạn 2 |
| S12-09 | Sửa lỗi phát hiện trong buổi thử nghiệm, phần plug-in và giao diện | Không còn lỗi chặn quy trình | CAD | 2 | S12-08 | Mục 13.7 |
| S12-10 | Chạy đủ sáu cổng chất lượng ở mục 13.7. Đóng gói bản 0.2 | Biên bản từng cổng. Gói cài bản 0.2 | CAD | 1 | S12-04, S12-09 | Mục 13.7 |
| S12-11 | Kiểm tra chế độ không có agent vẫn chạy trọn quy trình | Toàn bộ kiểm thử giai đoạn 1 đạt khi tắt agent | CAD | 0,5 | S12-10 | FR-AGT-09, mục 5.11 |
| S12-12 | Plug-in tối thiểu trong Smart 3D: nạp được lõi và bảng điều khiển dùng chung | Một lệnh trong Smart 3D mở bảng điều khiển và nạp một tệp IR | CAD | 1,5 | S10-12 | Hạng mục 3.2 |
| S12-13 | Rà nghiệm thu giai đoạn 2. Chốt Q3. Lập lại kế hoạch chi tiết Sprint 13 đến 16 | Biên bản nghiệm thu. Kế hoạch giai đoạn 3 đã chia nhỏ | CAD | 1 | S12-10 | Mục 5.11, Q3, mốc M6 |
| S12-14 | Ký xác nhận đáp án của toàn bộ bộ bản vẽ chuẩn. Đánh giá chất lượng đề xuất của agent trong buổi thử | Danh sách ký. Nhận xét bằng văn bản | ỐNG | 2 | S12-08 | Mục 13.2 |
| S12-15 | Tổng hợp kết quả thử nghiệm giai đoạn 2. Đề nghị khách hàng thử nghiệm chuyển sang hợp đồng trả tiền | Một trang kết quả. Phản hồi của khách hàng về đề nghị | KH | 2 | S12-08 | Q12, mốc quyết định giai đoạn 4 |

{{TONG:S12}}

### Tổng kết giai đoạn 2

| # | Điều kiện nghiệm thu | Việc chứng minh |
|---|---|---|
| 1 | Agent lập được hồ sơ cho bộ bản vẽ chưa từng gặp, người dùng chỉ sửa nhỏ | S12-08 |
| 2 | Các chỉ số ở mục 5.2 được đo trên bộ bản vẽ chuẩn từ 20 bản | S12-01 |
| 3 | Chế độ không có agent vẫn hoạt động | S12-11 |
| 4 | Bộ đánh giá agent đủ bốn nhóm ca và chạy được bằng một lệnh | S10-01, S11-02 |
| 5 | Có số đo chi phí mô hình ngôn ngữ cho mỗi bản vẽ | S10-04, S12-05 |

---
