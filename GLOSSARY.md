# Piping Domain Glossary

Ngôn ngữ chuẩn (ubiquitous language) cho hệ thống nhận diện mô hình đường ống (Piping Model Reconstruction), thống nhất thuật ngữ giữa bình đồ 2D (Piping Layout) và bản vẽ đẳng cự (Isometric/ISOGEN).

## Core Domain

**Piping Model**:
Mô hình dữ liệu trung gian biểu diễn toàn bộ mạng lưới đường ống, phụ kiện, kết cấu và thiết bị được trích xuất từ bản vẽ.
_Avoid_: 3D Model, CAD Entity, Piping IR

**Line**:
Tập hợp các đoạn ống và phụ kiện cùng mang một số hiệu đường ống (Line Number) và quy cách kỹ thuật (Piping Spec).
_Avoid_: Pipeline, Tuyến ống

**Run**:
Đoạn liên tục trên một Line chạy giữa hai điểm nối nhánh hoặc hai điểm kết nối thiết bị.
_Avoid_: Pipe branch, Nhánh ống

**Segment**:
Một đoạn ống thẳng đơn lẻ nối giữa hai điểm mút hoặc hai phụ kiện liên tiếp.
_Avoid_: Pipe, Ống con, Vector

**Node**:
Điểm kết nối không gian giữa các đoạn ống, phụ kiện hoặc điểm rẽ nhánh.
_Avoid_: Point, Vertex, Vertex3D

**Component**:
Phụ kiện đường ống lắp trên tuyến như cút, tê, van, côn thu hoặc mặt bích.
_Avoid_: Fitting, Accessory, Phụ tùng

**Equipment**:
Thiết bị công nghệ (bồn bể, máy bơm, tháp) kết nối với hệ thống đường ống thông qua các đầu vòi (nozzle).
_Avoid_: Vessel, Pump, Khối thiết bị

**Structure**:
Kết cấu xây dựng hỗ trợ đường ống như giá đỡ (pipe rack), dầm thép hoặc cột bê tông.
_Avoid_: Civil, Steel frame, Giàn đỡ

**Port**:
Cổng kết nối vật lý trên một phụ kiện hoặc vòi thiết bị xác định vị trí, hướng và đường kính danh định nối với ống.
_Avoid_: Connection point, Joint, Điểm đầu

**Provenance**:
Thông tin nguồn gốc liên kết một đối tượng trong mô hình với thực thể đồ họa gốc trên bản vẽ nguồn.
_Avoid_: Source trace, Audit log

## Isometric Domain

**Isometric Drawing**:
Bản vẽ đơn tuyến chiếu trục đo biểu diễn hình học 3D của một tuyến ống độc lập dùng cho chế tạo và lắp đặt.
_Avoid_: 3D Drawing, Iso sheet, Bản vẽ chế tạo

**ISOGEN**:
Định dạng xuất bản vẽ isometric tự động chuẩn công nghiệp (tạo ra các lớp PIPE, DIMLINES, MATLIST, ISOTEXT).
_Avoid_: AutoIso, Iso generator

**Anchor**:
Điểm mốc tọa độ 3D xác định rõ ràng trên bản vẽ (như vòi thiết bị hoặc mặt bích) dùng để cố định hệ tọa độ tuyệt đối.
_Avoid_: Neo, Fixed point, Datum point

**Span**:
Khoảng cách đo gióng giữa hai điểm mốc hoặc phụ kiện trên trục đo kèm theo con số kích thước tương ứng.
_Avoid_: Đoạn gióng, Dimension span

**Bridged Segment**:
Đoạn ống đồ họa bị ngắt quãng trên bản vẽ (như bị chèn phụ kiện hoặc nét khuất) nhưng thuộc về cùng một đoạn ống liên tục.
_Avoid_: Broken line, Nối khe

**Callout**:
Ký hiệu quả bóng hoặc ô số thứ tự trên bản vẽ chỉ đến một phụ kiện để tra cứu bảng vật tư.
_Avoid_: Ô số, Balloon, Part tag

**MATLIST**:
Bảng liệt kê danh mục vật tư thiết bị (Bill of Materials) trên bản vẽ isometric.
_Avoid_: BOM, Bảng vật tư, Part list

**Cut Piece List**:
Bảng kê chiều dài thực tế cần cắt cho từng đoạn ống thẳng sau khi đã trừ độ chiếm chỗ của phụ kiện.
_Avoid_: Cut pipe, Bảng cắt ống

**Continuation**:
Ký hiệu mũi tên hoặc ghi chú tham chiếu chuyển tiếp giữa các tờ bản vẽ khác nhau của cùng một tuyến ống.
_Avoid_: See iso, Điểm nối tờ, Next sheet

## 2D Layout Domain

**Piping Layout**:
Bản vẽ bình đồ mặt bằng biểu diễn hướng tuyến, cao độ và vị trí lắp đặt đường ống trong không gian nhà xưởng.
_Avoid_: 2D Plant, Mặt bằng đường ống

**Centerline**:
Đường tim ống đơn hoặc đôi biểu diễn tim hình học của tuyến ống trên bình đồ.
_Avoid_: Trục ống, Tim tuyến

**BOP (Bottom of Pipe)**:
Cao độ đo tới đáy ngoài của ống, dùng để tính toán điểm kê lên giá đỡ.
_Avoid_: Đáy ống, Elevation bottom

**TOP (Top of Pipe)**:
Cao độ đo tới đỉnh ngoài của ống.
_Avoid_: Đỉnh ống, Elevation top

**TOS (Top of Steel)**:
Cao độ mặt trên của dầm thép đỡ ống.
_Avoid_: Mặt dầm, Đỉnh thép

**Elevation Datum**:
Mốc cao độ chuẩn của dự án để quy đổi các giá trị cao độ tương đối (+EL, EL).
_Avoid_: Mốc 0, Datum plane

## Agent & Configuration Domain

**Convention Profile**:
Bộ quy ước khai báo ánh xạ các lớp, block, kiểu chữ và tiêu chuẩn đường ống tương ứng với phong cách vẽ của dự án.
_Avoid_: Config, Setup file, Hồ sơ nhận diện

**Draft Profile**:
Hồ sơ quy ước tạm thời do trợ lý AI khảo sát và đề xuất trong bộ nhớ phiên làm việc trước khi được kỹ sư phê duyệt.
_Avoid_: Temp profile, Đề xuất nháp

**Agent**:
Trợ lý AI vận hành độc lập với CAD qua mô hình ngôn ngữ lớn để khảo sát bản vẽ, đề xuất hồ sơ và gợi ý giải quyết các điểm mơ hồ.
_Avoid_: Bot, Copilot, Plugin AI

**Hermes Proxy**:
Dịch vụ cổng chuyển tiếp cục bộ tương thích giao thức OpenAI chạy ngầm trên máy trạm, tự động chèn xác thực và chuyển tiếp yêu cầu đến các nhà cung cấp mô hình.
_Avoid_: Local Server, AI Gateway, Cổng API

**Hermes Agent**:
Trợ lý AI dòng lệnh hoặc giao diện người dùng độc lập chạy trên máy trạm, có khả năng kết nối tới các máy chủ MCP để gọi công cụ ngoại vi.
_Avoid_: Bot ngoài, Hermes Client

**MCP Server (Máy chủ MCP)**:
Tiến trình dịch vụ cục bộ cung cấp các công cụ trích xuất và phân tích bản vẽ đường ống theo chuẩn Model Context Protocol cho các ứng dụng AI bên ngoài qua kênh stdio.
_Avoid_: AI Plugin, External API, Agent Bridge

**Live CAD Bridge (Cầu nối CAD Trực tiếp)**:
Kênh giao tiếp liên tiến trình (Named Pipe) giữa AutoCAD plug-in và MCP Server, cho phép AI bên ngoài truy vấn trực tiếp bản vẽ đang mở theo thời gian thực mà không cần ghi tệp ra đĩa.
_Avoid_: Direct CAD Link, Memory Hook, CAD IPC

**In-CAD MCP Client (Bộ máy khách MCP trong CAD)**:
Tính năng của cửa sổ PA_ASK cho phép giao diện hội thoại nổi trong AutoCAD trực tiếp nạp, điều phối và thực thi các công cụ theo chuẩn Model Context Protocol kết nối với mô hình ngôn ngữ lớn.
_Avoid_: CAD MCP, Chat Tooling

**Model Provider**:
Cơ chế bộ kết nối trích xuất và điều hướng yêu cầu tới mô hình ngôn ngữ lớn (OpenAI, Hermes proxy hoặc mô hình nội bộ).
_Avoid_: LLM Driver, Đầu mối kết nối

**Transient Capacity Error (Lỗi Nghẽn Tải Thượng Nguồn)**:
Trạng thái máy chủ thượng nguồn (upstream LLM provider) tạm thời quá tải lưu lượng tại giờ cao điểm và trả về mã HTTP 429, không phải lỗi hết hạn ngạch tài khoản hay sai khóa API.
_Avoid_: Quota Exceeded, Account Expired, Dead Model

**Exponential Backoff Retry (Thử Lại Lùi Thời Gian Tăng Dần)**:
Cơ chế tự động gửi lại yêu cầu API sau các khoảng thời gian chờ tăng dần (2s, 4s, 8s) khi gặp lỗi nghẽn tải tạm thời, giúp cuộc trò chuyện không bị ngắt quãng đột ngột.
_Avoid_: Instant Retry, Loop Retry, Blind Retry

**Action Tool (Công cụ Hành động)**:
Công cụ ở cấp độ quyền `ToolTier.Action` cho phép Agent thực hiện các can thiệp thay đổi hình học hoặc dữ liệu trực tiếp lên bản vẽ AutoCAD sau khi được kỹ sư phê duyệt.
_Avoid_: Direct Editor, Write Tool, Mutation Script

**Mutation Proposal (Đề xuất Can thiệp)**:
Bản mô tả có cấu trúc gồm danh sách các thao tác thêm mới hoặc sửa đổi CAD dự kiến do Agent tạo ra và lưu trong bộ nhớ tạm (Staging) để kỹ sư xem trước và phê duyệt trước khi commit.
_Avoid_: Change Request, Pending Edit, Draft Patch

**Atomic Undo Transaction (Giao dịch Hoàn tác Đơn lẻ)**:
Khối giao dịch duy nhất trong AutoCAD bọc toàn bộ các thao tác can thiệp của một đề xuất đã duyệt, cho phép kỹ sư hoàn nguyên 100% bản vẽ chỉ bằng một lệnh `UNDO`.
_Avoid_: Batch Transaction, Single Rollback, Group Undo

**Soft Delete (Xóa Mềm / Cách ly Đối tượng)**:
Kỹ thuật không xóa vật lý (Hard Delete) thực thể nguồn khỏi cơ sở dữ liệu CAD mà chỉ chuyển sang layer ẩn hoặc gán trạng thái miễn trừ, bảo toàn tính toàn vẹn nguồn gốc (Provenance).
_Avoid_: Erase Entity, Hard Delete, Xóa vĩnh viễn

**Smart Defaults (Giá trị Mặc định Thông minh)**:
Nguyên tắc ứng xử của Agent: khi người dùng yêu cầu vẽ một cấu kiện nhưng thiếu thông số phụ (tọa độ, DN, tiêu chuẩn), Agent không hỏi dồn dập mà tự áp dụng các thông số chuẩn kỹ thuật (DN100, Sch 40, tâm khung nhìn) và lập đề xuất để kỹ sư duyệt nhanh.
_Avoid_: Interrogation, Hỏi dồn, Blocking Questions

**Pipe Geometry Mutation (Thao tác Sinh Ống Mẫu)**:
Thao tác can thiệp bản vẽ ở cấp độ Action tạo đường ống 2D (đường tim Line kèm nhãn) hoặc khối 3D ACIS (theo danh mục ASME B36.10) tại vị trí chỉ định.
_Avoid_: Freehand Drawing, Vẽ tùy tiện, Raw CAD Line

**Verification Hierarchy (Hệ thống Kiểm định 4 Cấp độ)**:
Khung đánh giá độ tin cậy của can thiệp CAD: L1 (API Success - transaction không ném ngoại lệ), L2 (Entity Count Audit - số lượng thực thể tăng/giảm chính xác), L3 (Property & Geometry Audit - đối tượng đúng layer, kích thước chiều dài đạt dung sai < 0.1mm, text khớp nội dung), L4 (Visual QA - phản hồi trực quan trên màn hình).
_Avoid_: Trust-all, Blind Commit, Không kiểm định

**Post-Mutation Audit (Kiểm toán Hậu can thiệp)**:
Cơ chế tự động rà soát lại toàn bộ thực thể mới tạo hoặc sửa đổi ngay sau khi transaction hoàn tất, xuất báo cáo chỉ số định lượng (L2/L3) xác nhận bản vẽ đã được cập nhật chính xác.
_Avoid_: No-op check, Bỏ qua hậu kiểm

**Environment Guardrail (Vệ sĩ Môi trường CAD)**:
Phòng tuyến kỹ thuật bảo vệ giao dịch CAD đảm bảo mọi tính toán hình học được thực thi độc lập với trạng thái tạm thời của người dùng như chế độ bắt điểm chuột (OSNAP) hay biến hệ thống hiển thị.
_Avoid_: Mouse click simulator, Snap dependency

**WCS Enforcement (Ép buộc Tọa độ Thế giới)**:
Nguyên tắc chuẩn hóa bắt buộc mọi tọa độ điểm chèn và vector định hướng phải được tính theo Hệ tọa độ thế giới (World Coordinate System), loại bỏ nguy cơ lệch tọa độ khi bản vẽ đang xoay trục người dùng (UCS).
_Avoid_: UCS assumption, Local coordinate leak

**Document Target Binding (Khóa liên kết Tài liệu)**:
Cơ chế gắn chặt mỗi đề xuất can thiệp (Mutation Proposal) với đường dẫn hoặc định danh của bản vẽ đích, ngăn chặn việc commit nhầm thay đổi vào tab bản vẽ khác khi kỹ sư chuyển đổi tài liệu trong AutoCAD.
_Avoid_: Unbound Mutation, Loose Target, Tráo tab bản vẽ

**Mutation Evidence (Bằng chứng Thực thi Can thiệp)**:
Bản ghi bằng chứng vật lý sinh ra sau khi commit thành công gồm danh sách Handle, tọa độ hộp bao (BoundingBox), số liệu đo đạc thực tế và tem thời gian, hiện thực hóa triết lý "Tool success is not task success".
_Avoid_: Empty Success, Unverified Result, Không bằng chứng

**CAD Health Doctor (Bác sĩ Chẩn đoán Hệ thống CAD)**:
Công cụ tự chẩn đoán toàn diện (qua lệnh PA_DOCTOR hoặc tool cad_doctor) kiểm tra tính sẵn sàng của bản vẽ, proxy AI, thư viện ASME Catalog và quyền can thiệp mà không làm gián đoạn hay sửa đổi bản vẽ.
_Avoid_: Blind Debug, Phỏng đoán lỗi

**Delivery Freshness Gate (Cổng kiểm soát Độ mới Bàn giao)**:
Nguyên tắc kiểm tra đảm bảo mọi dữ liệu bàn giao (bản vẽ, báo cáo, hồ sơ quy ước) phải được sinh mới hoặc cập nhật từ chính phiên làm việc hiện tại, loại bỏ nguy cơ đọc nhầm tệp rác cũ còn sót lại.
_Avoid_: Stale Delivery, Tệp cũ còn sót

**Direct CAD Execution (Thực thi Trực tiếp Can thiệp CAD)**:
Phương thức cho phép Agent tự động commit ngay lập tức thay đổi vào AutoCAD (qua cờ auto_apply) trong một bước duy nhất khi kỹ sư ra lệnh vẽ hoặc sửa đổi trực tiếp, loại bỏ hoàn toàn ma sát phải copy-paste mã đề xuất qua lại.
_Avoid_: Multi-step friction, Bắt buộc duyệt thủ công

**Native Undo Safety (An toàn Hoàn tác Gốc)**:
Cơ chế bảo vệ dựa trên giao dịch nguyên tử của AutoCAD, cho phép kỹ sư hoàn tác 100% mọi thao tác vẽ/chỉnh sửa của Agent tức thì bằng phím tắt Ctrl+Z hoặc lệnh U/UNDO mà không làm hỏng dữ liệu gốc.
_Avoid_: Destructive Write, Không thể hoàn tác

**Resilient Proposal Resolution (Phân giải Đề xuất Dung sai)**:
Cơ chế tìm kiếm và suy diễn thông minh cho phép hệ thống tự động nhận diện đúng đề xuất can thiệp ngay cả khi người dùng gõ sai tiền tố (ví dụ pro- thay vì prop-) hoặc tự động chọn đề xuất duy nhất đang chờ khi người dùng không truyền proposal_id.
_Avoid_: Strict Hash Match, Lỗi từ chối do chính tả

## UI & Interaction Domain

**Modeless Chat Window (Cửa sổ Nổi Trò chuyện)**:
Cửa sổ giao diện hội thoại AI độc lập, hiển thị nổi trên AutoCAD (modeless), không khóa chuột hay bàn phím, cho phép kỹ sư vừa pan, zoom, kiểm tra bản vẽ vừa tương tác trực tiếp với Agent.
_Avoid_: Modal Dialog, Cửa sổ chặn, Popup cứng

**PaletteSet (Bảng điều khiển neo)**:
Bảng giao diện mặc định có thể neo vào mép màn hình (dockable panel) của AutoCAD, chuyên dùng để hiển thị và tra cứu các bảng dữ liệu kỹ thuật nhiều thẻ (Tổng quan, Vấn đề, Tuyến ống, Đoạn chạy, Hồ sơ, Nhật ký).
_Avoid_: Sidebar, Menu phụ, Khung nổi
