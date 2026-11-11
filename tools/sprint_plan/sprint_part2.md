
### Sprint 4: cấu kiện, thiết bị và khung bộ luật

**Mục tiêu.** IR có đủ van, thiết bị, đầu chờ, giá đỡ. Thư viện thiết bị chạy được. Bảng điều khiển hiện danh sách vấn đề và phóng tới vị trí.

**Trình diễn cuối sprint.** Gõ `PA_CONVERT` trên bản vẽ mẫu. Mô hình 3D có đủ ống, van, bồn, bơm, giá đỡ. Thêm một loại thiết bị mới bằng một tệp JSON và một dòng trong hồ sơ, chạy lại, thiết bị mới xuất hiện mà không biên dịch lại.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S4-01 | Nhận diện van và cấu kiện nối tiếp từ block: tìm đoạn ống chứa nó, vị trí, hướng trục. Chia đoạn ống tại đó | 7 van trên bản vẽ mẫu. Block không nằm trên ống nào thì tạo vấn đề | LÕI | 1,5 | S3-03 | Hạng mục 1.8, FR-REC-09, R-GEO-06 |
| S4-02 | Thư viện thiết bị: lược đồ tệp định nghĩa, bộ nạp ba lớp mặc định, công ty, dự án, lớp sau ghi đè lớp trước | Nạp được ví dụ ở mục 7.8.3. Tệp sai lược đồ báo lỗi rõ | LÕI | 1 | S2-01 | Hạng mục 1.3b, ADR-10 |
| S4-03 | Bộ tính biểu thức cho thư viện thiết bị: bốn phép tính, dấu ngoặc, tên tham số. Không chạy mã bên ngoài | Kiểm thử gồm biểu thức sai cú pháp, tham số không tồn tại, chia cho không | LÕI | 1 | S4-02 | Mục 7.8.3 |
| S4-04 | Nhận diện thiết bị theo `equipmentRules`: khớp theo hình, tên block, tiền tố số hiệu. Lấy tham số từ hình bao và nhãn. Viết định nghĩa cho bồn đứng, bơm và hộp bao dự phòng | 2 bồn và 2 bơm trên bản vẽ mẫu. Loại chưa có định nghĩa thành hộp bao kèm cảnh báo | LÕI | 1,5 | S4-03, S2-03 | FR-REC-11, R-EQP-04, NFR-MNT-07 |
| S4-05 | Nhận diện đầu chờ: đầu ống kết thúc trên biên thiết bị. Ghi thiết bị được nối | 8 đầu chờ và 5 đầu hở trên bản vẽ mẫu. Đầu ống gần thiết bị nhưng không chạm thì tạo vấn đề | LÕI | 1 | S4-04 | FR-REC-12, R-CON-05 |
| S4-06 | Nhận diện giá đỡ: cột, dầm, cao độ mặt trên kết cấu | 8 cột và 10 dầm trên bản vẽ mẫu | LÕI | 0,5 | S2-03 | FR-REC-13 |
| S4-07 | Khung bộ luật: giao diện một luật, bộ chạy, cấu trúc vấn đề, ba mức độ, bật tắt và đổi mức độ từ hồ sơ | Một luật mẫu chạy được. Đổi mức độ trong hồ sơ thì kết quả đổi theo | LÕI | 1 | S3-07 | Hạng mục 1.9, FR-CHK-01 đến 03, mục 9.2 |
| S4-18 | Đọc thuộc tính của block thành thuộc tính cấu kiện theo ánh xạ trong hồ sơ: số hiệu, cấp áp và các trường khác. Ghi nguồn gốc cho từng thuộc tính | Van có thuộc tính số hiệu trong block thì số hiệu xuất hiện trong IR. Thuộc tính không có trong ánh xạ được bỏ qua, không gây lỗi | LÕI | 0,5 | S4-01 | Hạng mục 1.8b, FR-REC-18, mục 7.9.5 |
| S4-09 | Dựng van và mặt bích: tổ hợp solid đơn giản theo kích thước danh mục, hoặc chèn block 3D nếu có | Van đúng vị trí, đúng hướng trục. Đoạn ống hai bên được cắt đúng chiều dài van | CAD | 1,5 | S4-01, S3-11 | FR-BLD-01, mục 10.1.4 |
| S4-10 | Dựng thiết bị từ định nghĩa: năm khối cơ bản hình trụ, hộp, nón, đáy cong, cầu. Tùy chọn chèn block 3D do người dùng cung cấp | Bồn, bơm dựng đúng kích thước. Thứ tự chọn cách dựng đúng mục 7.8.5 | CAD | 2 | S4-04, S3-10 | Mục 7.8.5, FR-BLD-01 |
| S4-11 | Dựng kết cấu: cột và dầm dạng hộp theo tiết diện | Cột và dầm đúng cao độ mặt trên | CAD | 0,5 | S4-06 | FR-BLD-01 |
| S4-12 | Thẻ Vấn đề: danh sách có mức độ, mã luật, mô tả, gợi ý sửa. Bấm vào thì phóng tới và tô sáng đối tượng liên quan | Bấm từng vấn đề trên bản vẽ mẫu đều tới đúng chỗ | CAD | 1,5 | S4-07, S2-13 | FR-REV-02 |
| S4-13 | Lọc và sắp xếp danh sách vấn đề theo mức độ, mã luật, tuyến, trạng thái | Bốn tiêu chí lọc hoạt động và kết hợp được | CAD | 0,5 | S4-12 | FR-REV-04 |
| S4-14 | Thẻ Tổng quan: số liệu nhận diện, tóm tắt vấn đề, xem trước số đối tượng sẽ dựng | Số liệu khớp IR | CAD | 1 | S3-10, S2-12 | FR-BLD-06 |
| S4-15 | Nối `PA_CONVERT`, `PA_CHECK`, `PA_BUILD` thành quy trình. Còn vấn đề mức chặn thì không cho dựng | Bản vẽ có vấn đề chặn: nút dựng bị khóa kèm lý do | CAD | 1 | S4-07, S3-17 | UC-01, FR-CHK-02 |
| S4-16 | Lập đáp án cho hai bản vẽ thật tiếp theo. Rà hình dạng các thiết bị trong thư viện | Hai thư mục mới. Góp ý về tham số và đầu chờ của từng loại thiết bị | ỐNG | 2,5 | S0-04 | Hạng mục 1.18, mục 7.8 |
| S4-17 | Thống nhất với khách hàng lịch cài thử ở Sprint 6: máy nào, ai dùng, bản vẽ nào, đo gì | Lịch và danh sách bản vẽ thử được hai bên xác nhận | KH | 1 | S1-19 | Hạng mục 1.19 |

{{TONG:S4}}

**Tiến độ, cập nhật ngày 10/11/2026.** Phần lập trình làm trước ngày bắt đầu sprint.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S4-01 | `ComponentRecognizer`: block tra bảng `blocks` của hồ sơ theo mẫu, tìm đoạn ống trong `componentTolerance`, vị trí chiếu lên trục, hướng trục, chiều dài tra `ASME-B16.10` (tệp danh mục mới, chưa ký). 7 van trên bản vẽ mẫu. Không nằm trên ống thì R-GEO-06, chưa ánh xạ thì R-CAT-02. Không chia đoạn ống trong IR; bộ dựng cắt ống hai bên |
| Xong | Việc S4-02 | `EquipmentLibrary` với `EquipmentDefinition` theo mục 7.8.3, `EquipmentFiles.LoadLayers` ba lớp mặc định, công ty `%PROGRAMDATA%\PipingAgent\equipment`, dự án `%LOCALAPPDATA%\PipingAgent\equipment`, lớp sau ghi đè. Ví dụ mục 7.8.3 nạp được nguyên văn. Sai lược đồ báo đường dẫn trường, định nghĩa sai không được thêm |
| Xong | Việc S4-03 | `Expression`: bốn phép tính, ngoặc, dấu âm, tên tham số. Kiểm thử sai cú pháp, tham số không tồn tại, chia cho không, ký tự lạ |
| Xong | Việc S4-04 | `EquipmentRecognizer`: hình bao từ tròn, chữ nhật khép kín, polyline kín, block; khớp `equipmentRules` theo hình, layer, block, tiền tố số hiệu; tham số từ hình bao, chữ gần theo mẫu, thuộc tính block, mặc định. 2 bồn `D=5000 H=6000`, 2 bơm `L=2400 W=1100` trên bản vẽ mẫu. Thiếu định nghĩa thì `generic_box` kèm R-EQP-04, thiếu số hiệu R-EQP-01, thiếu tham số R-EQP-02. Thư viện có `tank_vertical`, `pump`, `generic_box`, `exchanger_horizontal` |
| Xong | Việc S4-05 | `NozzleRecognizer`: đầu hở nằm trên biên thiết bị trong `nozzleTolerance` thành nút đầu chờ có `equipmentId`, `nozzleId`, thêm đầu chờ vào thiết bị. 8 đầu chờ và 5 đầu hở trên bản vẽ mẫu. Gần trong `nearEquipment` mà không chạm thì R-CON-05 |
| Xong | Việc S4-06 | `StructureRecognizer`: block ánh xạ `structure` thành cột, nét thẳng trên layer kết cấu thành dầm, cao độ mặt trên từ chữ khớp `tosPattern` gần nhất. 8 cột và 10 dầm, TOS 4000 |
| Xong | Việc S4-07 | `IRule`, `RuleContext`, `RuleRunner`: mức từ hồ sơ, `off` thì bỏ, luật lỗi không dừng luật khác. Luật mẫu R-TAG-01, R-CON-01, R-ELV-03 theo `elevationRange`, R-CAT-03 mới báo danh mục chưa ký. Kiểm thử đổi mức trong hồ sơ thì kết quả đổi theo |
| Xong | Việc S4-18 | Thuộc tính block theo ánh xạ `attributes` của bảng `blocks`: `tag` thành số hiệu kèm `tagRef`, `attributes.x` thành thuộc tính, thuộc tính không có trong ánh xạ giữ nguyên tên viết thường. Mỗi thuộc tính có nguồn `block_attribute` và handle. Bản vẽ mẫu không có block thuộc tính nên mới kiểm thử bằng mã |
| Xong | Việc S4-09 | `BuildPlanExtras`: cắt ống hai bên cấu kiện đúng chiều dài, `AcadBuilder.Component` thân trụ cộng trục tay vặn. Mặt bích chưa dựng vì bản vẽ mẫu không có |
| Xong | Việc S4-10 | Khối cơ bản `cylinder`, `box`, `cone`, `sphere`, `dished_head` xấp xỉ nón cụt, tính số ở lõi, plug-in chỉ tạo solid và hợp lại. Hệ trục cục bộ theo mục 7.8.3. Cách 3 chèn block 3D mới đọc được đường dẫn từ `hosts.autocad.block3d`, chưa chèn. Cách 1 và 2 thuộc Smart 3D |
| Xong | Việc S4-11 | Cột hộp từ chân tới mặt trên, dầm hộp có mặt trên tại TOS, tiết diện từ `section` hoặc `defaultSectionSize` 300 |
| Xong | Việc S4-12 | Thẻ Vấn đề có mức, luật, tuyến, trạng thái, nội dung, gợi ý. Bấm thì phóng và tô sáng. Chưa bấm thử từng vấn đề trong giao diện |
| Xong | Việc S4-13 | Lọc theo mức, mã luật, tuyến, trạng thái, kết hợp được, có đếm tổng |
| Xong | Việc S4-14 | Thẻ Tổng quan thêm dòng "Sẽ dựng" từ kế hoạch dựng và trạng thái khoá |
| Xong | Việc S4-15 | `PA_CONVERT` đọc và nhận diện, `PA_CHECK` chạy lại bộ luật và in theo luật, `PA_BUILD` từ chối khi còn vấn đề chặn và in lý do. Thử trong accoreconsole: bản vẽ mẫu không chặn nên dựng 80 solid gồm 33 ống, 15 co, 3 tê, 7 van, 4 thiết bị, 18 kết cấu; bản vẽ thật còn 2.724 vấn đề chặn nên bị khoá |
| Chờ người ngoài | Việc S4-16, S4-17 | |

**Điểm cần chú ý.**
- S4-18 chỉ đọc thuộc tính có sẵn trong block. Việc gắn chữ rời với cấu kiện và đọc bảng chú giải nằm ở Sprint 10 và 11.
- S4-02 đến S4-04 hiện thực quyết định ADR-10. Ca kiểm thử quan trọng nhất là thêm thiết bị mà không sửa mã.
- Bộ dựng thiết bị ở S4-10 là phần đầu tiên chứng minh định nghĩa thiết bị dùng lại được cho phần mềm CAD khác. Không để logic hình khối lọt vào dự án plug-in. Phần tính hình nằm ở lõi, plug-in chỉ tạo solid từ danh sách khối cơ bản.

### Sprint 5: bộ luật, quyết định và chỉnh sửa

**Mục tiêu.** Đủ 27 luật của giai đoạn 1. Người dùng sửa được kết quả nhận diện trên giao diện, và các chỉnh sửa còn nguyên khi chạy lại.

**Trình diễn cuối sprint.** Trên bản vẽ mẫu đã cố ý làm hỏng vài chỗ: hệ thống báo vấn đề, người dùng sửa từng vấn đề trên bảng điều khiển, dựng 3D. Đóng AutoCAD, mở lại, chạy lại, các chỉnh sửa vẫn được áp. Kiểm thử đối chứng với prototype đạt.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S5-01 | Nhóm luật nhãn: R-TAG-01 đến 06 | Mỗi luật có ít nhất một ca phát hiện đúng và một ca không báo nhầm | LÕI | 1 | S4-07 | Mục 9.3 |
| S5-02 | Nhóm luật kết nối và hình học: R-CON-01 đến 05, R-GEO-01, 02, 04, 05, 06 | Như trên | LÕI | 1,5 | S4-07 | Mục 9.3 |
| S5-03 | Các nhóm còn lại của giai đoạn 1: R-ELV-01, 02, 03, 05, R-EQP-01, 02, 04, R-CAT-01, 02, R-GEN-01, 02 | Như trên. Bản vẽ mẫu sạch cho ra không vấn đề chặn | LÕI | 1 | S4-07 | Mục 9.3 |
| S5-04 | Kiểm thử đối chứng tự động với prototype trên bản vẽ mẫu bản đầu | Mọi chỉ số ở bảng mục 13.3 khớp. Chênh lệch chiều dài do khác bảng phụ kiện được giải thích bằng văn bản | LÕI | 1 | S4-05, S4-06 | Hạng mục 1.11, mục 13.3 |
| S5-05 | Cơ chế quyết định: mô hình dữ liệu, lưu tách khỏi kết quả nhận diện, áp lên kết quả, khớp lại theo handle nguồn và dấu vân tay hình học. Quyết định không còn khớp được đánh dấu mồ côi | Sửa một nhãn, chạy lại nhận diện, chỉnh sửa vẫn được áp. Xóa đối tượng nguồn thì quyết định thành mồ côi và người dùng được báo | LÕI | 2 | S3-07 | Hạng mục 1.10, FR-REV-07, ADR-06 |
| S5-06 | Lưu phiên: tệp đi kèm cạnh bản vẽ chứa IR, quyết định, vấn đề đã bỏ qua, hồ sơ đã dùng. Tự lưu sau mỗi thay đổi | Tắt tiến trình đột ngột rồi mở lại không mất chỉnh sửa nào | LÕI | 1 | S5-05 | FR-CFG-01, NFR-REL-05, Q4 |
| S4-08 | Chạy toàn bộ chuỗi trên bản vẽ thật. Thêm tệp định nghĩa cho các thiết bị gặp trong bản vẽ thật | Bảng lỗi theo loại. Ít nhất một thiết bị mới được thêm chỉ bằng dữ liệu | LÕI | 0,5 | S4-04, S0-04 | R2, mục 7.8.6 |
| S5-08 | Thao tác sửa trên giao diện, phần một: gắn lại nhãn, nhập DN, nhập cao độ | Mỗi thao tác tạo một quyết định, chạy lại nhận diện và cập nhật danh sách vấn đề | CAD | 1,5 | S5-05, S4-12 | Hạng mục 1.14, FR-REV-03 |
| S5-09 | Thao tác sửa, phần hai: đổi loại nút, nối hai đầu ống, tách tuyến, bỏ qua đối tượng | Như trên | CAD | 1,5 | S5-08 | FR-REV-03 |
| S5-10 | Bỏ qua một vấn đề, bắt buộc nhập lý do. Ghi vào nhật ký | Không nhập lý do thì không bỏ qua được. Vấn đề đã bỏ qua vẫn xem lại được | CAD | 0,5 | S4-12 | FR-CHK-06 |
| S5-11 | Thẻ Tuyến ống: danh sách tuyến kèm DN, chiều dài, số vấn đề. Bấm thì tô sáng cả tuyến | Số liệu khớp IR | CAD | 1 | S2-13 | FR-REV-01 |
| S5-12 | Trình soạn hồ sơ: liệt kê layer và block của bản vẽ đang mở, cho gán vai trò | Tạo được hồ sơ của bản vẽ mẫu từ đầu chỉ bằng giao diện | CAD | 2 | S2-11, S2-01 | Hạng mục 1.15, FR-PRF-03 |
| S5-13 | Trình soạn hồ sơ: nhập mẫu nhãn và thử ngay trên các nhãn của bản vẽ. Nhập, xuất hồ sơ ra tệp | Gõ mẫu nhãn thấy ngay bao nhiêu nhãn khớp và nhãn nào không | CAD | 1 | S5-12 | FR-PRF-03, FR-PRF-06 |
| S5-14 | Chạy lại nhận diện và bộ luật sau mỗi quyết định, cập nhật giao diện | Sửa một vấn đề thì danh sách vấn đề cập nhật trong vài giây | CAD | 0,5 | S5-08 | UC-02 |
| S5-15 | Lập đáp án cho hai bản vẽ thật tiếp theo. Chấm tỷ lệ cờ thừa của bộ luật trên các bản đã có đáp án | Bộ bản vẽ chuẩn đạt khoảng mười bản. Danh sách cờ thừa theo luật | ỐNG | 2,5 | S5-03 | Hạng mục 1.18, mục 13.4 |
| S5-16 | Soạn kịch bản thử nghiệm cho khách hàng: các bước, phiếu ghi thời gian, câu hỏi phỏng vấn sau khi dùng | Kịch bản và phiếu ghi được khách hàng xem trước | KH | 1 | S4-17 | Hạng mục 1.19, NFR-USE-01 |

{{TONG:S5}}

**Tiến độ, cập nhật ngày 10/11/2026.** Phần lập trình làm trước ngày bắt đầu sprint.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S5-01, S5-02, S5-03 | Đủ 27 luật giai đoạn 1 cộng R-CAT-03 và R-DEC-01 mới. Kiểm thử `RuleMatrixTests` xác nhận mọi luật giai đoạn 1 có định nghĩa trong danh mục và có mã sinh trong lõi; thêm R-GEN-02 từ cờ đơn vị của bộ đọc và R-GEO-04 cấu kiện chồng nhau. Ca phát hiện đúng và không báo nhầm có cho từng nhóm trong các tệp kiểm thử hiện có, chưa đủ một cặp cho từng luật riêng lẻ. Bản vẽ mẫu sạch: 0 vấn đề chặn, 5 cảnh báo R-CON-01 tại ranh giới, 1 thông tin R-CAT-03 |
| Xong | Việc S5-04 | `PrototypeComparisonTests`: mọi chỉ số của `prototype/data/model.json` khớp, kể cả chiều dài theo DN. Chiều dài ống thẳng sau khi trừ co và tê theo ASME B16.9 là 106,2 m so với 115,9 m chiều dài tâm, vì prototype không có bảng phụ kiện |
| Xong | Việc S5-05 | `DecisionApplier`: khớp theo handle nguồn trong `prov.sources`, áp ở đúng bước của chuỗi nhận diện. Chín thao tác: gắn nhãn, DN, cao độ, loại nút, loại cấu kiện, nối, tách, bỏ đối tượng, miễn trừ. Quyết định không khớp được đánh dấu `orphan` và tạo R-DEC-01. Kiểm thử gắn nhãn rồi chạy lại vẫn áp, xoá đối tượng nguồn thì mồ côi |
| Xong | Việc S5-06 | `SessionFile` ghi cạnh bản vẽ `<tên>.pa.json` gồm IR, quyết định, hồ sơ, dấu vân tay; ghi qua tệp tạm rồi đổi tên. Thử trong accoreconsole: bỏ qua một vấn đề, mở phiên mới, quyết định được áp lại |
| Xong, chưa thử tay | Việc S5-08, S5-09 | `PA_FIX` với từ khoá Nhan, Dn, Caodo, Nut, Caukien, Noi, Tach, Boqua: chọn đối tượng nguồn trên bản vẽ, nhập giá trị, ghi quyết định, chạy lại. Nút Sửa trên thẻ Vấn đề gọi lệnh. Chưa thử chọn đối tượng trong giao diện |
| Xong | Việc S5-10 | `PA_WAIVE` và nút Bỏ qua trên bảng: không có lý do thì từ chối. Ghi quyết định `waive_issue` vào tệp phiên và nhật ký. Vấn đề đã bỏ qua vẫn trong danh sách với trạng thái Miễn trừ, lọc được |
| Xong | Việc S5-11 | Thẻ Tuyến ống: tuyến, DN, chiều dài, số đoạn chạy, số vấn đề mở; bấm tô sáng mọi đối tượng nguồn của tuyến. Thẻ cũ đổi tên Đoạn chạy |
| Xong một phần | Việc S5-12, S5-13 | Trình soạn trong thẻ Hồ sơ: liệt kê layer và block của bản vẽ với số lượng và loại, gán vai trò bằng ô chọn, thử mẫu nhãn thấy ngay số khớp và ví dụ không khớp, xuất hồ sơ kế thừa `base-default`. Chưa có nhập hồ sơ vào trình soạn, chưa soạn bảng `blocks` và `equipmentRules`, chưa thử trong giao diện |
| Xong | Việc S5-14 | `PA_RERUN` và `Rerun` dùng chung: chạy lại trên đối tượng đã đọc, không đọc lại bản vẽ; bản vẽ mẫu dưới 0,1 giây |
| Chờ người ngoài | Việc S5-15, S5-16 | |

**Điểm cần chú ý.**
- Cuối sprint là mốc M3. Chưa đạt kiểm thử đối chứng S5-04 thì không mang đi cài cho khách hàng.
- S5-05 quyết định trải nghiệm duyệt và sửa. Nếu thiếu thời gian, cắt S5-11 và S5-13 trước, không cắt S5-05.

### Sprint 6: xuất kết quả và thử nghiệm với khách hàng

**Mục tiêu.** Nghiệm thu giai đoạn 1: phần mềm chạy trên máy khách hàng với bản vẽ của họ, có số đo giờ công.

**Trình diễn cuối sprint.** Buổi trình diễn diễn ra tại khách hàng thử nghiệm hoặc bằng bản ghi của buổi thử. Trình bày bảng chỉ số chất lượng trên bộ bản vẽ chuẩn và số giờ tiết kiệm so với đường cơ sở.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S6-01 | Chạy thử hồ sơ và trả về chỉ số: tỷ lệ phân loại, tỷ lệ nhãn khớp mẫu, số đoạn chạy không nhãn, số vấn đề theo loại | Trình soạn hồ sơ hiện bốn chỉ số sau mỗi lần chạy thử | LÕI | 1 | S5-03 | FR-PRF-04 |
| S6-02 | Xuất bảng khối lượng ra CSV và Excel: chiều dài ống theo tuyến và DN, số co, tê, van, mặt bích | Số liệu trên bản vẽ mẫu khớp IR | LÕI | 1 | S4-01 | Hạng mục 1.16, FR-EXP-01 |
| S6-03 | Xuất báo cáo chuyển đổi: thống kê, danh sách vấn đề, vấn đề đã bỏ qua kèm lý do | Một tệp báo cáo đọc được mà không cần phần mềm | LÕI | 1 | S5-06 | FR-EXP-02 |
| S6-04 | Công cụ dòng lệnh: xử lý nhiều bản vẽ DXF với một hồ sơ, xuất IR và báo cáo tổng hợp | Chạy trên cả bộ bản vẽ chuẩn bằng một lệnh | LÕI | 1 | S6-03 | Hạng mục 1.17, FR-CFG-04, mức tích hợp C |
| S6-05 | Bộ chấm điểm hồi quy: so IR với `expected.json`, tính các chỉ số ở mục 13.4, gồm tỷ lệ sai im lặng | Một lệnh in ra bảng chỉ số cho toàn bộ bộ bản vẽ chuẩn. Chạy trong dựng tự động | LÕI | 1,5 | S6-04, S5-15 | Mục 13.4, NFR-MNT-05 |
| S5-07 | Chạy bộ luật trên bản vẽ thật. Chỉnh ngưỡng theo góp ý của chuyên gia. Đo tỷ lệ cờ thừa | Bảng số vấn đề theo luật cho từng bản vẽ. Danh sách ngưỡng đã chỉnh | LÕI | 0,5 | S5-03, S3-18 | Mục 13.4, R2 |
| S6-07 | Sửa lỗi phát hiện trong buổi thử với khách hàng, phần lõi | Các lỗi chặn quy trình đều được sửa hoặc có cách vòng | LÕI | 1,5 | S6-12 | Hạng mục 1.19 |
| S6-08 | Kiểm thử tích hợp AutoCAD theo bảng ở mục 13.6: đọc, dựng, dữ liệu thuộc tính, hoàn tác, lỗi giữa chừng, dấu vân tay, dựng lại, hai nền .NET | Tám ca chạy được bằng kịch bản trên hai phiên bản AutoCAD | CAD | 2 | S5-14 | Mục 13.6 |
| S6-09 | Nối `PA_EXPORT` và `PA_BATCH`. Thêm thẻ lệnh trên ribbon | Tám lệnh ở mục 5.8.1 đều gọi được từ ribbon | CAD | 1 | S6-02, S6-04 | Mục 5.8.1 |
| S6-10 | Tách chuỗi giao diện ra tệp tài nguyên, có tiếng Việt và tiếng Anh | Đổi ngôn ngữ trong cấu hình thì toàn bộ giao diện đổi | CAD | 0,5 | S5-13 | FR-CFG-05 |
| S6-11 | Gói cài thủ công: tệp nén chứa gói `.bundle`, hồ sơ mẫu, hướng dẫn cài hai trang | Người không viết mã cài được theo hướng dẫn | CAD | 0,5 | S6-09 | FR-LIC-03 |
| S6-12 | Cài tại khách hàng, cùng họ chạy ít nhất ba bản vẽ thật theo kịch bản, ghi thời gian duyệt và mọi chỗ vướng | Phiếu ghi cho từng bản vẽ. Danh sách lỗi và góp ý | CAD | 2 | S6-11, S5-16 | Hạng mục 1.19, mục 5.11 |
| S6-13 | Sửa lỗi phát hiện trong buổi thử, phần plug-in và giao diện | Các lỗi chặn quy trình đều được sửa | CAD | 1,5 | S6-12 | Hạng mục 1.19 |
| S6-14 | Rà nghiệm thu giai đoạn 1 theo mục 5.11. Lập lại kế hoạch chi tiết cho Sprint 7 đến 12 | Biên bản nghiệm thu ghi rõ đạt, chưa đạt. Kế hoạch giai đoạn 2 đã cập nhật | CAD | 0,5 | S6-12 | Mục 5.11, mốc M4 |
| S6-15 | Chia bộ bản vẽ chuẩn thành phần phát triển và phần giữ lại. Ký xác nhận đáp án | Danh sách hai phần. Phần giữ lại không được dùng để chỉnh thuật toán | ỐNG | 1 | S5-15 | Mục 13.2 |
| S6-16 | Tham gia buổi thử, đánh giá mô hình 3D kết quả theo con mắt người trong nghề | Nhận xét bằng văn bản: dùng được ở đâu, chưa dùng được ở đâu | ỐNG | 1 | S6-12 | Mục 13.7 mục 6 |
| S6-17 | Tổng hợp số đo: giờ công thủ công so với giờ công có công cụ. Phỏng vấn người dùng. Hỏi điều kiện để họ trả tiền | Một trang kết quả thử nghiệm. Đề xuất con số mục tiêu cho Q7 | KH | 2 | S6-12, S0-06 | Q7, Q12 |

{{TONG:S6}}

**Tiến độ, cập nhật ngày 10/11/2026.** Chỉ phần lập trình được làm trước; phần nghiệm thu với khách hàng chưa thể làm.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong một phần | Việc S6-01 | Bốn chỉ số có trong `RecognitionReport` và bảng điều khiển sau mỗi lần quét; trình soạn hồ sơ mới hiện số nhãn khớp mẫu khi thử mẫu, chưa chạy thử cả hồ sơ đang soạn |
| Xong một phần | Việc S6-02 | `QuantityExport`: chiều dài ống đã trừ phụ kiện theo tuyến và DN, số co, tê, van, mặt bích. Xuất CSV có BOM UTF-8 để Excel mở đúng; chưa xuất `.xlsx` thật. Số liệu bản vẽ mẫu khớp IR |
| Xong | Việc S6-03 | `ConversionReport` ra Markdown: thống kê, khối lượng, vấn đề theo luật, vấn đề đã bỏ qua kèm lý do, quyết định và quyết định mồ côi |
| Xong | Việc S6-04 | `PipingAgent.Cli batch <thư mục> --profile --out`: IR, khối lượng, báo cáo cho từng bản và `tong-hop.csv`. Đọc tệp phiên `.pa.json` nếu có |
| Xong | Việc S6-05 | `RegressionScorer` so IR với `<tên>.expected.json`: số đoạn chạy, đoạn ống, co, tê, ống đứng, đầu chờ, van, thiết bị, tổng chiều dài, chiều dài từng tuyến, tuyến thiếu và thừa làm tỷ lệ sai im lặng. Đáp án mẫu ở `data/golden/piping_plan_2d.expected.json`, bản vẽ mẫu 10/10. `batch` trả mã thoát 1 khi có bản không đạt để dùng trong dựng tự động |
| Xong một phần | Việc S6-08 | `build\test_build.ps1` chạy bằng kịch bản các ca đọc, dựng, xdata, hoàn tác, lỗi giữa chừng trên .NET 8. Chưa có ca dấu vân tay sau khi dựng, dựng lại, và nền .NET Framework 4.8 vì không có AutoCAD cũ |
| Chưa làm | Việc S6-09, S6-10 | Ribbon cần kiểu của AcMgd, phải cô lập như PaletteSet; chuỗi giao diện chưa tách ra tài nguyên |
| Xong, chưa thử tay | Việc S6-11 | `build\pack_bundle.ps1 -Zip` tạo `dist\PipingAgent-<phiên bản>.zip` gồm gói `.bundle`, thư mục hồ sơ mẫu và `build\HUONG_DAN_CAI.md`. Chưa có người không viết mã cài thử |
| Chờ người ngoài | Việc S6-07, S6-12 đến S6-17 | Cần khách hàng thử nghiệm và chuyên gia đường ống |

**Điểm cần chú ý.**
- S6-12 phụ thuộc lịch của khách hàng. Chốt lịch từ Sprint 4 qua việc S4-17.
- Nếu buổi thử lộ ra lỗi lớn hơn sức sửa của S6-07 và S6-13 thì kéo dài giai đoạn 1 thêm một sprint thay vì mang lỗi sang giai đoạn 2.

### Tổng kết giai đoạn 1

**Điều kiện nghiệm thu**, theo mục 5.11 của tài liệu dự án:

| # | Điều kiện | Việc chứng minh |
|---|---|---|
| 1 | UC-01, UC-02, UC-05, UC-08 chạy trọn trong AutoCAD trên bản vẽ mẫu | S4-15, S5-09, S6-09 |
| 2 | Chạy được trên ít nhất ba bản vẽ thật của khách hàng | S6-12 |
| 3 | Lõi C# khớp prototype trên bản vẽ mẫu | S5-04 |
| 4 | Có số đo đường cơ sở về giờ công | S0-06, S6-17 |
| 5 | Kiểm thử tích hợp đạt trên hai nền .NET | S6-08 |

**Yêu cầu của giai đoạn 1 được hoãn có chủ ý:**

| Yêu cầu | Ưu tiên | Dời tới | Lý do |
|---|---|---|---|
| FR-CFG-02, cấu hình ba cấp | S | Sprint 7 | Thử nghiệm chỉ cần một cấp |
| FR-EXP-05, xuất DXF 3D khi chạy ngoài AutoCAD | S | Danh sách chờ, việc S10-06 | Prototype Python đã có chức năng này để dùng tạm |
| NFR-PRF-01 và 02, đo hiệu năng trên bản vẽ lớn | | Danh sách chờ, việc S6-06 | Nhường chỗ cho việc đọc thuộc tính cấu kiện. Đưa lại ngay khi gặp bản vẽ thật trên 5.000 đối tượng |
| FR-ING-03, block lồng nhau và bản vẽ tham chiếu ngoài | S | Sprint 7 | Đúng theo tài liệu dự án là giai đoạn 2. Kéo lên sớm nếu bản vẽ thật cần |

---
