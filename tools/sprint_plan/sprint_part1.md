# Kế hoạch sprint dự án Piping Agent

**Phân rã lộ trình 12 tháng thành 25 sprint và các việc nhỏ**

| Mục | Giá trị |
|---|---|
| Phiên bản | 0.11 (bản nháp) |
| Ngày lập | 09/11/2026 |
| Tài liệu gốc | [`TAI_LIEU_DU_AN.md`](TAI_LIEU_DU_AN.md) phiên bản 0.4. Mọi mã `FR`, `NFR`, `ADR`, `UC`, `Q`, `R` và số mục trong cột "Truy vết" đều trỏ về tài liệu đó |
| Danh sách việc dạng bảng tính | [`backlog.csv`](backlog.csv), sinh tự động từ tài liệu này để nhập vào công cụ quản lý công việc |
| Trạng thái | Toàn bộ là **[ĐỀ XUẤT]** của người soạn. Chưa có số liệu năng suất thật của đội. Phải hiệu chỉnh sau Sprint 1 và Sprint 2 |

## Mục lục

1. [Cách đọc và các giả định](#1-cách-đọc-và-các-giả-định)
2. [Cách làm việc](#2-cách-làm-việc)
3. [Lịch sprint và các mốc](#3-lịch-sprint-và-các-mốc)
4. [Sprint 0: chuẩn bị](#4-sprint-0-chuẩn-bị)
5. [Giai đoạn 1: plug-in AutoCAD, Sprint 1 đến 6](#5-giai-đoạn-1-plug-in-autocad-sprint-1-đến-6)
6. [Giai đoạn 2: agent, Sprint 7 đến 12](#6-giai-đoạn-2-agent-sprint-7-đến-12)
7. [Giai đoạn 3: Smart 3D, Sprint 13 đến 16](#7-giai-đoạn-3-smart-3d-sprint-13-đến-16)
8. [Giai đoạn 4: thương mại hóa, Sprint 17 đến 24](#8-giai-đoạn-4-thương-mại-hóa-sprint-17-đến-24)
9. [Đường găng và phụ thuộc chính](#9-đường-găng-và-phụ-thuộc-chính)
10. [Kịch bản chỉ có một lập trình viên](#10-kịch-bản-chỉ-có-một-lập-trình-viên)
11. [Theo dõi rủi ro theo sprint](#11-theo-dõi-rủi-ro-theo-sprint)
12. [Việc người sáng lập cần chốt](#12-việc-người-sáng-lập-cần-chốt)
13. [Danh sách chờ](#13-danh-sách-chờ)

---

## 1. Cách đọc và các giả định

### 1.1 Mức chi tiết giảm dần theo thời gian

Kế hoạch này lập theo kiểu cuốn chiếu. Sprint càng gần thì càng chi tiết:

| Phạm vi | Mức chi tiết | Lý do |
|---|---|---|
| Sprint 0 đến 6 | Từng việc từ nửa ngày đến hai ngày rưỡi, có kết quả kiểm được | Đủ thông tin để làm ngay |
| Sprint 7 đến 12 | Từng việc từ nửa ngày đến ba ngày | Phụ thuộc kết quả thử nghiệm của giai đoạn 1 |
| Sprint 13 đến 24 | Từng việc từ một đến ba ngày, ở mức hạng mục | Phụ thuộc khảo sát Smart 3D và khách hàng đầu tiên. Phải chia nhỏ lại ở cuối giai đoạn trước |

Viết chi tiết Sprint 20 ngay bây giờ sẽ là chi tiết giả. Cuối mỗi giai đoạn có một việc lập lại kế hoạch cho giai đoạn sau.

### 1.2 Giả định về đội

Tài liệu dự án chưa chốt quy mô đội. Kế hoạch này giả định đội hình ở mục 12.7 của tài liệu dự án:

| Mã vai trò | Vai trò | Thời lượng | Ghi chú |
|---|---|---|---|
| `CAD` | Lập trình viên plug-in CAD | Toàn thời gian | Người sáng lập |
| `LÕI` | Lập trình viên lõi và agent | Toàn thời gian | Người thứ hai. Nếu chưa có, xem chương 10 |
| `ỐNG` | Chuyên gia đường ống | Hai đến ba ngày mỗi sprint | Lập đáp án, rà bộ luật, rà bản vẽ mẫu |
| `KH` | Phụ trách khách hàng | Theo việc | Nếu chưa có người riêng thì người sáng lập kiêm. Khi đó sức làm của `CAD` giảm tương ứng |

Vai trò ghi trên mỗi việc là người chịu trách nhiệm chính. Hai lập trình viên vẫn rà soát mã cho nhau.

### 1.3 Giả định về sức làm

- Một sprint dài hai tuần, tức 10 ngày làm việc.
- Mỗi lập trình viên được xếp tối đa **8 ngày công** mỗi sprint. Hai ngày còn lại dành cho họp, rà soát mã và việc phát sinh.
- Đơn vị ước lượng là ngày công lý tưởng của một người, làm một việc không bị ngắt.
- Con số ước lượng là của người soạn, chưa dựa trên năng suất thật. Sau Sprint 2, lấy số ngày công thực tế chia cho số ước lượng để ra hệ số, rồi nhân hệ số đó cho các sprint sau.
- Công cụ lập trình có AI hỗ trợ có thể làm nhanh hơn ở phần mã lõi. Kế hoạch này không tính trước mức tăng đó.

### 1.4 Cấu trúc một dòng việc

| Cột | Ý nghĩa |
|---|---|
| Mã | `S<số sprint>-<số thứ tự>`. Mã này không đổi khi việc bị dời sang sprint khác, nên số sprint trong mã có thể khác sprint đang chứa việc đó |
| Việc | Việc phải làm |
| Kết quả phải có | Điều kiện để coi việc là xong. Phải kiểm được |
| Vai trò | Người chịu trách nhiệm chính |
| Công | Ngày công ước lượng |
| Phụ thuộc | Việc phải xong trước |
| Truy vết | Yêu cầu, quyết định hoặc mục tương ứng trong tài liệu dự án |

---

## 2. Cách làm việc

### 2.1 Nhịp làm việc cho đội hai đến bốn người

| Hoạt động | Thời điểm | Thời lượng | Nội dung |
|---|---|---|---|
| Lập kế hoạch sprint | Sáng thứ Hai đầu sprint | 1 giờ | Rà danh sách việc của sprint, điều chỉnh theo việc tồn, chốt mục tiêu |
| Trao đổi hằng ngày | Mỗi sáng | 10 phút | Hôm qua xong gì, hôm nay làm gì, đang vướng gì |
| Rà soát mã | Liên tục | | Mọi thay đổi vào nhánh chính đều qua một người khác xem |
| Trình diễn cuối sprint | Chiều thứ Sáu cuối sprint | 30 phút | Chạy thật trên bản vẽ. Mời chuyên gia đường ống, và khách hàng thử nghiệm nếu được |
| Nhìn lại | Ngay sau trình diễn | 30 phút | Một điều giữ, một điều bỏ, một điều thử. Ghi lại ngày công thực tế |

### 2.2 Điều kiện để một việc được đưa vào sprint

1. Kết quả phải có đã ghi rõ và kiểm được.
2. Các việc phụ thuộc đã xong hoặc xong trong cùng sprint trước nó.
3. Ước lượng không quá ba ngày công. Lớn hơn thì chia nhỏ.
4. Dữ liệu cần dùng đã có: bản vẽ, bảng kích thước, môi trường phần mềm.

### 2.3 Điều kiện để một việc được coi là xong

1. Mã đã gộp vào nhánh chính qua rà soát.
2. Có kiểm thử tự động cho phần lõi. Phần chạy trong AutoCAD có ít nhất một kịch bản kiểm tra được ghi lại.
3. Dựng tự động đạt, gồm kiểm tra kiến trúc: lõi không tham chiếu CAD, giao diện, mạng.
4. Không làm giảm chỉ số nào trên bộ bản vẽ chuẩn so với lần đo trước.
5. Nếu thay đổi hợp đồng dữ liệu thì lược đồ JSON và tài liệu dự án đã được cập nhật.
6. Đã chạy thử trên bản vẽ mẫu, và trên bản vẽ thật khi có.

### 2.4 Điều kiện để một sprint được coi là đạt

- Mục tiêu sprint được trình diễn bằng phần mềm chạy thật, không phải bằng slide.
- Việc chưa xong được ghi rõ lý do và dời sang sprint sau, không âm thầm kéo dài.
- Bảng rủi ro ở chương 11 được cập nhật.

### 2.5 Quy tắc khi trễ

- Không kéo dài sprint. Việc chưa xong dời sang sprint sau.
- Trễ quá 20% số ngày công trong hai sprint liên tiếp thì cắt phạm vi theo thứ tự ưu tiên `C`, rồi `S`, giữ `M`.
- Không cắt ba thứ: kiểm thử trên bản vẽ thật, bộ bản vẽ chuẩn có đáp án, và bộ đánh giá agent. Cắt chúng thì không còn biết sản phẩm tốt hay xấu.

---

## 3. Lịch sprint và các mốc

### 3.1 Lịch

Ngày bắt đầu là giả định, khớp với biểu đồ ở mục 12.1 của tài liệu dự án. Nếu lùi ngày bắt đầu thì lùi cả lịch. Lịch nghỉ Tết chính thức chưa có khi lập kế hoạch, nên giả định nghỉ một tuần.

{{LICH}}

Các sprint có ngày lễ: Sprint 3 có ngày 01/01, Sprint 11 có ngày 30/04 và 01/05, Sprint 20 có ngày 02/09. Ở các sprint này sức làm giảm khoảng một ngày mỗi người. Các ngày nghỉ khác cần đối chiếu lịch chính thức.

### 3.2 Các mốc

| Mốc | Thời điểm | Nội dung | Quyết định tại mốc |
|---|---|---|---|
| M0 | Cuối Sprint 0 | Đầu ra của prototype mở được trong AutoCAD. Có khách hàng đồng ý thử nghiệm | Nếu chưa có bản vẽ thật: vẫn làm Sprint 1 và 2, nhưng dồn sức của `KH` vào việc này |
| M1 | Cuối Sprint 2 | Hạn chót có bản vẽ thật | Nếu vẫn chưa có: dừng lại xem xét. Phân khúc khách hàng hoặc cách tiếp cận đang sai |
| M2 | Cuối Sprint 3 | Ống đầu tiên được dựng trong AutoCAD từ bản vẽ mẫu | Đo năng suất thật, hiệu chỉnh ước lượng cho Sprint 4 đến 6 |
| M3 | Cuối Sprint 5 | Quy trình trọn vẹn trên bản vẽ mẫu. Lõi C# khớp prototype | Đủ điều kiện mang đi cài cho khách hàng thử nghiệm |
| M4 | Cuối Sprint 6 | Nghiệm thu giai đoạn 1 theo mục 5.11 | Chốt Q7. Quyết định vào giai đoạn 2 hay kéo dài giai đoạn 1 |
| M5 | Cuối Sprint 9 | Agent lập hồ sơ và xử lý mơ hồ chạy được từ đầu đến cuối | Kết quả khảo sát Smart 3D. Chốt Q9 |
| M6 | Cuối Sprint 12 | Nghiệm thu giai đoạn 2 | Chốt Q3: Smart 3D hay Plant 3D trước |
| M7 | Cuối Sprint 16 | Nghiệm thu giai đoạn 3 | Chốt phạm vi bản 1.0 |
| M8 | Cuối Sprint 24 | Phát hành bản 1.0. Có khách hàng trả tiền | |

---

## 4. Sprint 0: chuẩn bị

**Mục tiêu.** Có bản vẽ thật, có người hiểu nghề đường ống, xác nhận đầu ra của prototype dùng được trong AutoCAD, và có khung dự án dựng được.

**Trình diễn cuối sprint.** Mở mô hình 3D của prototype trong AutoCAD. Trình bày bản phân tích ít nhất một bản vẽ thật. Chạy lệnh thử của plug-in trên hai phiên bản AutoCAD.

Sprint này xử lý luôn hai điểm từ phản hồi ngày 09/11/2026 của người xem demo: demo bị hiểu là công cụ tự động định tuyến, và bản vẽ mẫu chưa đúng thực tế thiết kế.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S0-01 | Trả lời người góp ý về demo. Nói rõ sản phẩm dựng lại tuyến đã thiết kế, không tự định tuyến. Xin một bản vẽ thật hoặc 30 phút rà bản vẽ mẫu | Đã gửi. Ghi lại câu trả lời của họ | KH | 0,5 | | R1 |
| S0-02 | Lập danh sách 10 công ty có thể thử nghiệm: nhà thầu thiết kế, xưởng gia công ống, công ty làm thuê bản vẽ. Liên hệ từng nơi | Bảng theo dõi: tên, người liên hệ, trạng thái, phần mềm họ dùng, phiên bản AutoCAD | KH | 1,5 | | Việc 0.2, R1 |
| S0-03 | Soạn thỏa thuận bảo mật và một trang mô tả cách xử lý bản vẽ: lưu ở đâu, ai xem, ẩn danh thế nào, xóa khi nào | Hai tài liệu đã được người có chuyên môn pháp lý xem qua | KH | 1 | | Mục 13.2, CON-05 |
| S0-04 | Ký thỏa thuận với một đến hai khách hàng thử nghiệm và nhận bản vẽ | Từ 5 đến 10 bản vẽ mặt bằng đường ống thật, dạng DWG hoặc DXF, kèm đồng ý bằng văn bản | KH | 1 | S0-02, S0-03 | Việc 0.2, ASM-06 |
| S0-05 | Tìm một kỹ sư đường ống làm bán thời gian. Thống nhất khối lượng hai đến ba ngày mỗi sprint | Có người nhận lời, đã thống nhất việc và thù lao | KH | 1 | | Q10, mục 12.7 |
| S0-06 | Tổ chức đo đường cơ sở: một họa viên dựng thủ công mô hình 3D từ một bản vẽ, ghi lại số giờ | Con số giờ công cho ít nhất một bản vẽ, kèm mô tả cách đo | KH | 0,5 | S0-04 | Việc 0.4, Q7 |
| S0-07 | Thêm nhóm công cụ tự động định tuyến vào trang đối thủ của bộ slide và vào tài liệu dự án. Thêm một câu định vị phân biệt | Bộ slide và tài liệu dự án đã cập nhật | KH | 0,5 | S0-01 | R5 |
| S0-08 | Cài hai phiên bản AutoCAD trên máy phát triển: một bản chạy .NET Framework 4.8 và một bản chạy .NET 8 | Cả hai bản khởi động được. Ghi lại nguồn giấy phép | CAD | 0,5 | | Q2, NFR-CMP-02 |
| S0-09 | Mở `prototype/data/model_3d.dxf` trong AutoCAD. Kiểm tra hiển thị, layer, kích thước. Quay màn hình | Danh sách lỗi hiển thị nếu có. Một đoạn quay màn hình AutoCAD thật cho bộ slide | CAD | 0,5 | S0-08 | Việc 0.1 |
| S0-10 | Viết plug-in tối thiểu, một dự án dựng ra hai bản cho hai nền .NET, có một lệnh in ra số đối tượng trong bản vẽ | Lệnh chạy được trên cả hai phiên bản AutoCAD từ cùng một bộ mã | CAD | 1,5 | S0-08 | Mục 10.1, CON-01 |
| S0-11 | Họp chốt các quyết định phải có trước giai đoạn 1: Q2, Q4, Q5, Q8, Q10, Q11, Q13, Q14 | Mục 14.2 của tài liệu dự án được cập nhật trạng thái từng câu | CAD | 0,5 | S0-12, S0-13 | Việc 0.5 |
| S0-12 | Phân tích bản vẽ thật: quy ước layer, cách ghi nhãn, ngữ nghĩa cao độ, có ống xiên không, mức độ sạch. Đếm tỷ lệ ký hiệu là block so với nét rời. Ghi nhận có bảng chú giải ký hiệu không và chú thích của cấu kiện được ghi thế nào | Bản ghi nhận khác biệt so với sáu giả định ở mục 5.10. Mỗi giả định ghi rõ đúng, sai, hay chưa đủ dữ liệu. Có con số tỷ lệ ký hiệu nét rời để chốt Q14 | LÕI | 2 | S0-04 | Việc 0.3, ASM-01 đến 05, Q14, mục 7.9.3 |
| S0-13 | Đánh giá hai đến ba thư viện đọc DXF cho .NET trên bản vẽ mẫu và bản vẽ thật. Xét giấy phép, tình trạng bảo trì, loại đối tượng đọc được, tốc độ | Bảng so sánh và thư viện được chọn | LÕI | 2 | | Q11, FR-ING-02 |
| S0-14 | Tạo kho mã theo mục 6.10. Tạo các dự án rỗng. Thiết lập dựng và kiểm thử tự động cho mỗi lần đẩy mã | Một lần đẩy mã kích hoạt dựng và chạy kiểm thử, kết quả xanh | LÕI | 1,5 | | Việc 0.6 |
| S0-15 | Viết kiểm tra kiến trúc: dự án lõi không được tham chiếu thư viện CAD, giao diện hay mạng. Đưa quy ước mã và điều kiện "xong" vào kho | Thử thêm một tham chiếu cấm vào lõi thì dựng tự động báo đỏ | LÕI | 0,5 | S0-14 | NFR-MNT-09, hạng mục 1.3c |
| S0-16 | Sửa bản vẽ mẫu theo góp ý của chuyên gia thành bản thứ hai. Giữ nguyên bản đầu để kiểm thử đối chứng. Dựng lại video và slide từ bản mới | `piping_plan_2d_v2.dxf` được chuyên gia chấp nhận. Video và slide mới. Bản đầu không đổi | LÕI | 1,5 | S0-18 | Mục 13.3 |
| S0-17 | Tham gia họp chốt quyết định, chuẩn bị số liệu cho Q11, Q13 và Q14 | Có đề xuất bằng văn bản cho Q11, Q13 và Q14 | LÕI | 0,5 | S0-12, S0-13 | Việc 0.5 |
| S0-18 | Rà bản vẽ mẫu của prototype theo thực tế thiết kế đường ống. Ví dụ: ống hút từ bồn về bơm đang nằm trọn trong một mặt phẳng | Danh sách điểm chưa đúng thực tế, kèm cách sửa | ỐNG | 1 | S0-05 | Phản hồi 09/11/2026 |
| S0-19 | Cùng phân tích bản vẽ thật, giải thích các quy ước ngành mà lập trình viên chưa biết | Ghi chú thuật ngữ và quy ước bổ sung vào bản phân tích | ỐNG | 1 | S0-04 | Việc 0.3 |

{{TONG:S0}}

**Điểm cần chú ý.**
- S0-04 là việc quan trọng nhất của sprint và có thể kéo dài hơn hai tuần vì phụ thuộc bên ngoài. S0-06, S0-12 và S0-19 trễ theo. Các việc này được phép dời sang Sprint 1 và 2 mà không chặn việc lập trình.
- Nếu người sáng lập kiêm `KH` thì tổng việc của `CAD` và `KH` trong sprint này vừa khít sức một người.
- Nếu `LÕI` chưa vào đội thì S0-13 đến S0-16 dời sang Sprint 1, và áp dụng chương 10.

**Tiến độ, cập nhật ngày 10/11/2026.** Các việc không cần người ngoài đã được làm trước ngày bắt đầu sprint. Ngày 10/11/2026 người sáng lập cài AutoCAD 2025 và cung cấp một bản vẽ mặt bằng thật, một isometric và một P&ID.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S0-07 | Trang đối thủ của bộ slide có nhóm tự động định tuyến. Tài liệu dự án có mục 1.8 về định vị |
| Xong | Việc S0-14 | Kho mã có 9 dự án, dựng và kiểm thử đạt trên máy phát triển. Tệp cấu hình dựng tự động đã viết nhưng chưa chạy trên máy chủ nào, vì chưa chọn nơi lưu kho mã |
| Xong | Việc S0-15 | Kiểm tra kiến trúc có 6 ca. Đã thử thêm tham chiếu cấm vào lõi và kiểm thử báo đỏ đúng như yêu cầu |
| Xong | Việc S0-13 | Ba thư viện đo trên hai tệp mẫu và trên bản vẽ thật, cả DXF lẫn DWG. ACadSharp là thư viện duy nhất đọc đủ. Kết quả ở [`DANH_GIA_THU_VIEN_DXF.md`](DANH_GIA_THU_VIEN_DXF.md) |
| Xong, chờ xác nhận | Việc S0-12 | Phân tích bằng công cụ ở [`KHAO_SAT_BAN_VE_THAT.md`](KHAO_SAT_BAN_VE_THAT.md): sáu giả định đã đối chiếu, có số liệu cho Q13 và Q14. Các suy đoán về quy ước chờ chuyên gia đường ống xác nhận theo S0-19 |
| Xong một phần | Việc S0-10 | Bản .NET 8 đã nạp và chạy trong AutoCAD 2025: `PA_HELLO` đếm đúng 19.054 đối tượng trên bản vẽ thật, khớp với ezdxf và ACadSharp. Kiểm thử lặp lại bằng `build\test_plugin.ps1` qua `accoreconsole`, cần thư mục `bin` của plug-in nằm trong `TRUSTEDPATHS`. Bản .NET Framework 4.8 chỉ mới biên dịch, chưa có AutoCAD bản 2024 trở về trước để thử |
| Xong một phần | Việc S0-17 | Có đề xuất bằng văn bản cho cả Q11, Q13 và Q14. Chờ họp chốt |
| Chờ chuyên gia đường ống | Việc S0-16, S0-18, S0-19 | S0-19 có sẵn danh sách câu hỏi ở mục 7 của bản khảo sát |
| Chờ người sáng lập | Việc S0-01 đến S0-06, S0-08, S0-09, S0-11 | S0-08 chặn phần còn lại của S0-10 |

---

## 5. Giai đoạn 1: plug-in AutoCAD, Sprint 1 đến 6

**Mục tiêu giai đoạn.** Chạy trọn UC-01, UC-02, UC-05, UC-08 trong AutoCAD bằng hồ sơ quy ước, không có AI, trên bản vẽ mẫu và ít nhất ba bản vẽ thật.

**Cách chia việc.** `LÕI` làm chuỗi nhận diện và kiểm thử hoàn toàn ngoài AutoCAD. `CAD` làm plug-in, bộ dựng và giao diện song song. Hai nhánh gặp nhau ở hợp đồng dữ liệu: đối tượng nguồn, mô hình trung gian IR, và ba giao diện ở mục 10.4.

### Sprint 1: khung dữ liệu và đọc bản vẽ

**Mục tiêu.** Đọc được bản vẽ thành đối tượng trung lập bằng hai đường, từ tệp DXF và từ AutoCAD, và hai đường cho kết quả giống nhau.

**Trình diễn cuối sprint.** Gõ lệnh `PA_SCAN` trong AutoCAD trên bản vẽ mẫu, xem bản tóm tắt bản vẽ. Chạy công cụ so sánh cho thấy bộ đọc AutoCAD và bộ đọc DXF trả về cùng tập đối tượng.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S1-01 | Viết các lớp của IR: mô hình, tuyến, đoạn chạy, đoạn ống, nút, cấu kiện, thiết bị, kết cấu, vấn đề, quyết định, nguồn gốc | Các lớp khớp bảng ở mục 7.4. Không có kiểu dữ liệu nào của CAD | LÕI | 1,5 | S0-14 | Hạng mục 1.1, ADR-02 |
| S1-02 | Tuần tự hóa IR ra JSON và đọc lại, có trường phiên bản lược đồ | Ví dụ ở mục 7.5 đọc vào rồi ghi ra cho kết quả giống hệt | LÕI | 1 | S1-01 | FR-EXP-03, NFR-CMP-06 |
| S1-03 | Viết tệp lược đồ JSON cho IR và kiểm thử đối chiếu | Tệp lược đồ trong kho. IR sai lược đồ bị từ chối kèm thông báo chỉ rõ trường sai | LÕI | 0,5 | S1-02 | Mục 6.12.2 |
| S1-04 | Thư viện hình học: điểm, véc-tơ 2D và 3D, hộp bao, so sánh có dung sai, khoảng cách điểm tới đoạn, giao hai đoạn, kiểm tra thẳng hàng | Kiểm thử đơn vị cho từng phép, gồm các ca biên: song song, trùng nhau, chạm đầu mút | LÕI | 1,5 | S0-14 | Hạng mục 1.2, NFR-ACC-01 |
| S1-05 | Chỉ mục không gian để tìm đối tượng gần một điểm hoặc trong một vùng | Truy vấn trên 5.000 đoạn trả kết quả đúng như duyệt toàn bộ và nhanh hơn rõ rệt | LÕI | 1 | S1-04 | NFR-PRF-01 |
| S1-06 | Định nghĩa đối tượng nguồn trung lập, bộ lọc và bản tóm tắt bản vẽ | Các lớp dùng chung cho mọi bộ đọc. Giữ mã định danh nguồn, layer, màu, kiểu nét, đường dẫn block | LÕI | 0,5 | S1-01 | FR-ING-07, mục 10.4 |
| S1-07 | Bộ đọc DXF: đường thẳng, polyline, cung, đường tròn, chữ một dòng và nhiều dòng, block kèm thuộc tính, đường dẫn | Đọc bản vẽ mẫu ra đúng 80 đối tượng như prototype. Đọc được bản vẽ thật mà không dừng giữa chừng | LÕI | 1,5 | S1-06, S0-13 | Hạng mục 1.4, FR-ING-02 |
| S1-08 | Nhận biết đơn vị bản vẽ và quy đổi về milimét. Không xác định được thì trả về trạng thái cần hỏi người dùng | Kiểm thử với bản vẽ đơn vị milimét, mét, inch và không khai báo | LÕI | 0,5 | S1-07 | FR-ING-04, R-GEN-02 |
| S1-09 | Hoàn chỉnh dự án plug-in: cấu trúc gói `.bundle`, tệp khai báo, tự nạp khi mở AutoCAD | Chép thư mục gói vào máy là plug-in tự nạp trên cả hai phiên bản | CAD | 1 | S0-10 | Hạng mục 1.12, mục 11.3 |
| S1-10 | Khung lệnh: lớp cơ sở có bắt ngoại lệ, khóa tài liệu, ghi nhật ký. Đăng ký `PA_SCAN` và `PA_PANEL` | Cố ý ném ngoại lệ trong lệnh thì AutoCAD không thoát, người dùng thấy thông báo rõ ràng | CAD | 1 | S1-09 | NFR-REL-01, NFR-USE-02 |
| S1-11 | Bộ đọc AutoCAD: đường thẳng, polyline, cung, đường tròn, chữ, block kèm thuộc tính. Giữ handle | Trả về đối tượng trung lập cho mọi đối tượng thuộc các loại trên ở không gian mô hình | CAD | 2 | S1-06, S1-10 | FR-ING-01, FR-ING-07 |
| S1-12 | Bộ đọc AutoCAD: đường dẫn, đường dẫn nhiều nhánh, kích thước | Đọc được ba loại trên. Block lồng nhau để giai đoạn 2 | CAD | 1 | S1-11 | FR-ING-01 |
| S1-13 | Bản tóm tắt bản vẽ: layer, block, kiểu chữ, số lượng theo loại, phạm vi tọa độ | `PA_SCAN` in bản tóm tắt ra dòng lệnh | CAD | 0,5 | S1-11 | FR-ING-09 |
| S1-14 | Giới hạn vùng xử lý bằng cửa sổ chọn hoặc tập đối tượng đã chọn | Chỉ đối tượng trong vùng được đọc | CAD | 0,5 | S1-11 | FR-ING-05 |
| S1-15 | Thư viện nhật ký có cấu trúc: mỗi dòng một bản ghi JSON, có mã phiên | Một lần chạy `PA_SCAN` tạo tệp nhật ký có bản ghi phiên và bản ghi từng bước | CAD | 0,5 | S0-14 | NFR-MNT-06, mục 11.5 |
| S1-16 | Công cụ so sánh hai bộ đọc: xuất đối tượng trung lập từ AutoCAD và từ DXF của cùng bản vẽ rồi so | Trên bản vẽ mẫu hai bộ đọc khớp nhau. Mọi khác biệt được liệt kê | CAD | 1 | S1-07, S1-12 | Mục 13.6 |
| S1-17 | Dấu vân tay bản vẽ nguồn: băm nội dung trước và sau khi chạy để chứng minh không sửa bản vẽ | Kiểm thử chạy `PA_SCAN` rồi so dấu vân tay, phải giống nhau | CAD | 0,5 | S1-11 | NFR-REL-03, FR-ING-06 |
| S1-18 | Lập đáp án cho bản vẽ mẫu thứ hai và cho bản vẽ thật đầu tiên: tuyến, DN, cao độ, kết nối | Hai thư mục trong bộ bản vẽ chuẩn có `expected.json` và `notes.md` | ỐNG | 2 | S0-16, S0-04 | Hạng mục 1.18, mục 13.2 |
| S1-19 | Theo dõi khách hàng thử nghiệm. Hỏi rõ phiên bản AutoCAD họ dùng và chính sách về dịch vụ AI bên ngoài | Bảng theo dõi cập nhật. Có câu trả lời cho Q2 | KH | 1 | S0-02 | Q2, Q6, R7 |

{{TONG:S1}}

**Điểm cần chú ý.**
- S1-06 phải xong trong hai ngày đầu vì cả hai nhánh cùng dùng.
- Đáp án ở S1-18 do chuyên gia lập tay. Không lấy đầu ra của chính hệ thống làm đáp án.

**Tiến độ, cập nhật ngày 10/11/2026.** Phần lõi được làm trước ngày bắt đầu sprint, ngay sau khi có bản vẽ thật.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S1-01 | Các lớp IR trong `src/PipingAgent.Core/Ir`. Thêm mốc cao độ `BOP`, `TOP`, `TOS`, `Unknown` và trường tờ vẽ theo khảo sát bản vẽ thật. Bộ kiểm tra `ModelValidator` bắt định danh trùng, tham chiếu treo, quyết định của agent thiếu lý do |
| Xong | Việc S1-02 | `IrJson` ghi và đọc, khoá camelCase, liệt kê snake_case, điểm là mảng số. Ví dụ mục 7.5 đọc vào rồi ghi ra giống hệt về nội dung. Phiên bản lược đồ lạ hoặc thiếu bị từ chối. Lõi dùng `System.Text.Json` 8.0.6, là gói bên thứ ba duy nhất, chọn cùng dòng .NET 8 của AutoCAD 2025 |
| Xong | Việc S1-03 | Lược đồ `schemas/piping-ir-1.0.json`. Kiểm thử bằng `JsonSchema.Net` chỉ trong dự án kiểm thử: IR sai bị từ chối với đường dẫn tới trường sai, sáu ca mẫu |
| Xong | Việc S1-04 | `src/PipingAgent.Core/Geometry`: điểm, véc-tơ, hộp bao, đoạn, dung sai 0,01 mm, khoảng cách, giao hai đoạn phân loại bảy quan hệ, thẳng hàng. 30 ca kiểm thử gồm song song, trùng đường, chạm đầu mút, chữ T, hở trong dung sai |
| Xong | Việc S1-05 | `SpatialGrid` lưới đều. Trên 5.000 đoạn: trùng kết quả duyệt toàn bộ ở 600 truy vấn ngẫu nhiên, nhanh hơn ít nhất 5 lần theo kiểm thử |
| Xong | Việc S1-06 | `SourceEntity` lớp phẳng có handle, layer, màu, kiểu nét, đường dẫn block; `EntityFilter`; `DrawingSummary` có danh sách tờ vẽ; giao diện `IDrawingReader` |
| Xong | Việc S1-07 | `DxfDrawingReader` bằng ACadSharp, đọc cả DWG. Bản vẽ mẫu ra đúng 80 đối tượng. Bản vẽ thật ra đủ 19.054, không bỏ sót, DWG và DXF cho cùng dấu vân tay. Đọc được MULTILEADER, kích thước, hatch, block lồng nhau có lật gương. 19 kiểm thử |
| Xong | Việc S1-08 | Đọc đơn vị từ tệp, quy về mm. Không khai báo thì giữ hệ số 1 và bật cờ cần xác nhận, có hệ số do người dùng đặt. Kiểm thử với mm, m, inch và không khai báo |
| Xong một phần | Việc S1-09 | `build\pack_bundle.ps1` dựng `dist\PipingAgent.bundle` với hai nền và `PackageContents.xml`. Chưa cài thử vào thư mục tự nạp của AutoCAD vì bước đó cần người sáng lập làm. Tên công ty và mã sản phẩm trong tệp khai báo là tạm |
| Xong | Việc S1-10 | `CommandBase`: khoá tài liệu, giao dịch chỉ đọc, bắt ngoại lệ, báo lỗi rõ kèm đường dẫn nhật ký. Lệnh `PA_SCAN`, `PA_SCANWINDOW`, `PA_EXPORT`, `PA_PANEL`, `PA_HELLO`. `PA_PANEL` tự nhận ra môi trường không có giao diện |
| Xong | Việc S1-11, S1-12 | `AcadDrawingReader` đọc đủ các loại kể cả đường dẫn, đường dẫn nhiều nhánh, kích thước, và nổ block lồng nhau. Block lồng nhau làm luôn ở giai đoạn 1 vì cần để so khớp với bộ đọc DXF |
| Xong | Việc S1-13 | `PA_SCAN` in tóm tắt: đơn vị, số theo loại, layer, block, phạm vi, dấu vân tay. Bản vẽ thật quét trong 0,35 giây |
| Xong | Việc S1-14 | `PA_SCAN` nhận tập chọn trước, `PA_SCANWINDOW` nhận cửa sổ hai điểm |
| Xong | Việc S1-15 | `JsonLineLogger` trong lõi, mỗi dòng một bản ghi JSON có mã phiên. Plug-in ghi vào `%LOCALAPPDATA%\PipingAgent\logs`. Không ghi nội dung bản vẽ |
| Xong | Việc S1-16 | `PipingAgent.Cli compare`. Trên bản vẽ thật, bộ đọc AutoCAD và bộ đọc DXF trùng tuyệt đối: 19.054 đối tượng không nổ block, 88.409 đối tượng khi nổ block, cùng dấu vân tay. Bản vẽ mẫu cũng trùng |
| Xong | Việc S1-17 | `DrawingFingerprint` SHA-256 trên dạng chuẩn hoá, không phụ thuộc thứ tự và bộ đọc. Chạy `PA_SCAN` ba lần trong một phiên cho cùng dấu vân tay |
| Chờ người ngoài | Việc S1-18, S1-19 | |

Kết quả so hai bộ đọc đã đạt tiêu chí trình diễn cuối sprint. Những khác biệt tìm ra và đã sửa trong quá trình so: điểm chèn block lật gương lưu theo hệ toạ độ đối tượng trong DXF, đỉnh cuối của đường dẫn nhiều nhánh, đỉnh lặp ở vòng biên hatch, khoảng góc của đối tượng trong block.

### Sprint 2: phân loại và đọc nhãn

**Mục tiêu.** Từ đối tượng trung lập, hệ thống biết đối tượng nào là ống, nhãn, van, thiết bị, và gắn được nhãn vào đoạn chạy kèm điểm tin cậy.

**Trình diễn cuối sprint.** Trong AutoCAD, mở bảng điều khiển, chạy quét, xem tỷ lệ đối tượng đã phân loại và danh sách đoạn chạy kèm số hiệu tuyến, DN, cao độ. Bấm vào một đoạn chạy thì bản vẽ phóng tới và tô sáng.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S2-01 | Mô hình hồ sơ quy ước, lược đồ JSON, bộ nạp có kiểm tra | Hồ sơ ở mục 7.7 nạp được. Hồ sơ sai báo lỗi chỉ rõ dòng và trường | LÕI | 1 | S1-03 | Hạng mục 1.1, FR-PRF-01, 02 |
| S2-02 | Viết hồ sơ mẫu cho bản vẽ mẫu, cả hai bản | Hai tệp hồ sơ trong `data/profiles/` | LÕI | 0,5 | S2-01 | FR-PRF-08 |
| S2-03 | Bộ phân loại: khớp theo tên layer có ký tự đại diện, tên block, loại đối tượng, màu, kiểu nét, mẫu văn bản | Trên bản vẽ mẫu mọi đối tượng được gán đúng vai trò. Kết quả không đổi giữa các lần chạy | LÕI | 1,5 | S2-01, S1-07 | Hạng mục 1.5, FR-CLS-01, 02 |
| S2-04 | Báo cáo đối tượng chưa phân loại, gom theo layer | Có tỷ lệ tổng và bảng theo layer | LÕI | 0,5 | S2-03 | FR-CLS-03, R-GEN-01 |
| S2-05 | Đọc nhãn theo mẫu trong hồ sơ: tách số hiệu tuyến, DN, cao độ và trường tùy chọn. Bỏ mã định dạng của chữ nhiều dòng | 11 nhãn của bản vẽ mẫu được tách đúng. Nhãn không khớp mẫu nào được ghi nhận, không bị bỏ qua | LÕI | 1 | S2-01 | Hạng mục 1.6, FR-REC-01 |
| S2-06 | Làm sạch hình học: gộp đoạn nối tiếp thẳng hàng, bắt đầu mút gần nhau trong dung sai, loại đối tượng trùng | Kiểm thử trên các ca dựng sẵn: khe hở nhỏ, đoạn trùng, polyline bị cắt vụn | LÕI | 1 | S1-04, S1-05 | FR-ING-08 |
| S2-07 | Dựng đoạn chạy từ polyline và chuỗi đoạn thẳng | 11 đoạn chạy trên bản vẽ mẫu | LÕI | 0,5 | S2-06 | Hạng mục 1.6 |
| S2-08 | Gắn nhãn với đoạn chạy theo ba mức ưu tiên: đường dẫn chạm đoạn chạy, nhãn nằm trên hoặc song song sát, đoạn gần nhất trong bán kính | Trên bản vẽ mẫu 11 nhãn gắn đúng 11 đoạn chạy | LÕI | 1,5 | S2-05, S2-07 | FR-REC-02 |
| S2-09 | Tính điểm tin cậy cho mỗi liên kết. Hai ứng viên có điểm gần nhau thì tạo vấn đề nhãn mơ hồ thay vì tự chọn | Ca dựng sẵn có hai đoạn chạy cách đều một nhãn phải ra vấn đề, không ra liên kết | LÕI | 0,5 | S2-08 | FR-REC-03, mục 9.4, NFR-ACC-06 |
| S2-10 | Danh mục ống và phụ kiện: định dạng dữ liệu, bộ nạp, hàm tra theo loại, DN, tiêu chuẩn. Nhập bảng ống ASME B36.10 và phụ kiện hàn ASME B16.9 cho DN15 đến DN600 | Tra được đường kính ngoài, bán kính co, kích thước tê. DN không có trả về kết quả rõ ràng, không ném lỗi | CAD | 1,5 | S0-14 | Hạng mục 1.3, FR-CFG-03, FR-REC-17 |
| S2-11 | Bảng điều khiển gắn trong AutoCAD: khung các thẻ Tổng quan, Vấn đề, Tuyến ống, Hồ sơ, Nhật ký. Tách phần hiển thị và phần dữ liệu để dùng lại cho Smart 3D | `PA_PANEL` mở bảng. Bảng nằm trong dự án giao diện dùng chung, không gọi API AutoCAD trực tiếp | CAD | 2 | S1-10 | Hạng mục 1.14, FR-REV-01 |
| S2-12 | Nối `PA_SCAN` với lõi: đọc, phân loại, hiện kết quả ở thẻ Tổng quan | Thẻ Tổng quan hiện số đối tượng theo vai trò và tỷ lệ chưa phân loại | CAD | 1 | S2-03, S2-11 | UC-01 |
| S2-13 | Tô sáng bằng đồ họa tạm thời theo danh sách handle. Phóng tới một vùng. Xóa tô sáng | Tô sáng không tạo đối tượng thật. Dấu vân tay bản vẽ không đổi | CAD | 1,5 | S1-17 | FR-REV-02, mục 10.4 |
| S2-14 | Thanh tiến độ và nút hủy cho thao tác dài. Hàm đưa việc về luồng chính của AutoCAD | Thao tác dài hơn 2 giây có tiến độ và hủy được giữa chừng | CAD | 1 | S1-10 | NFR-PRF-05, CON-02 |
| S2-15 | Ngữ cảnh phiên theo từng bản vẽ đang mở, không dùng biến tĩnh toàn cục. Xử lý sự kiện chuyển và đóng bản vẽ | Mở hai bản vẽ, quét cả hai, dữ liệu không lẫn nhau | CAD | 1 | S2-11 | Mục 6.12.3 quy tắc 4 |
| S2-16 | Kiểm tra bảng kích thước trong danh mục đối chiếu với tiêu chuẩn gốc | Bảng được ký xác nhận. Sai lệch được sửa | ỐNG | 0,5 | S2-10 | FR-CFG-03 |
| S2-17 | Lập đáp án cho hai bản vẽ thật tiếp theo | Hai thư mục mới trong bộ bản vẽ chuẩn | ỐNG | 2 | S0-04 | Hạng mục 1.18 |
| S2-18 | Chạy phân loại và đọc nhãn trên các bản vẽ thật đã có. Ghi lại chỗ sai và nguyên nhân | Bảng lỗi theo loại. Tỷ lệ chưa phân loại của từng bản vẽ | KH | 1 | S2-08 | R2, mốc M1 |

{{TONG:S2}}

**Tiến độ, cập nhật ngày 10/11/2026.** Phần lập trình làm trước ngày bắt đầu sprint. Người sáng lập đã xác nhận bảng điều khiển hiện được trong AutoCAD 2025.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S2-01 | `src/PipingAgent.Core/Profile`: lớp hồ sơ, bộ nạp `ProfileLoader`, kế thừa `extends`, hợp nhất con lên cha. Lỗi cú pháp và lỗi nội dung đều báo dòng, cột và đường dẫn trường, ví dụ `dòng 6, $.roles[1].role`. Lõi không đọc tệp, phần đọc tệp nằm ở dự án mới `src/PipingAgent.Hosting` |
| Xong | Việc S2-02 | `data/profiles/base-default.json` làm nền, `sample-pump-station.json` cho bản vẽ mẫu, thêm `nsrp-piping-layout.json` cho bản vẽ thật. Thêm mục `elevationPatterns` cho chữ cao độ ghi rời, vì bản vẽ thật ghi cao độ tách khỏi số hiệu tuyến |
| Xong | Việc S2-03 | `Classifier`: layer có ký tự đại diện `*`, `?`, `#`, tên block, loại gốc hoặc loại trung lập, màu, kiểu nét, mẫu chữ. Quy tắc đầu tiên khớp thắng nên kết quả ổn định. Bản vẽ mẫu: 80/80 đối tượng có vai trò |
| Xong | Việc S2-04 | `ClassificationReport`: tỷ lệ tổng, bảng theo layer kèm loại đối tượng còn thiếu quy tắc, so với ngưỡng R-GEN-01 của hồ sơ |
| Xong | Việc S2-05 | `TagParser`: nhóm đặt tên chuẩn `line`, `dn`, `el`, `datum`, `service`, `spec`, `insul`; cỡ inch kể cả phân số quy về DN; chữ không khớp được ghi nhận thành R-TAG-03. 11 nhãn bản vẽ mẫu tách đúng, dạng nhãn bản vẽ thật `2"-XXX-nnn-nnnn-A1AS-HS(40)` cũng tách được |
| Xong | Việc S2-06 | `CenterlineCleaner`: loại nét suy biến, bắt điểm trong dung sai, bỏ đoạn trùng và giữ handle của nó làm nguồn, gộp thẳng hàng, nối nét rời tại đỉnh bậc hai. Polyline là ranh giới đoạn chạy, không nối. 7 ca kiểm thử |
| Xong | Việc S2-07 | `RunBuilder`: 11 đoạn chạy trên bản vẽ mẫu, mỗi đoạn có nguồn gốc `REC-RUN-01` |
| Xong | Việc S2-08 | `TagAttacher` ba mức: đường dẫn chạm, nằm sát và song song với hình chiếu chồng lên đoạn, gần nhất trong bán kính. Bản vẽ mẫu: 11/11 nhãn gắn đúng, 4 bằng đường dẫn, 6 nằm sát, 1 gần nhất, không có vấn đề nào |
| Xong | Việc S2-09 | Điểm tin cậy theo mục 9.4, ngưỡng 0,75 tự quyết, 0,40 đến 0,75 kèm R-TAG-02, dưới 0,40 không gắn và tạo R-TAG-02 với gợi ý `assign_tag` cho từng ứng viên. Ca hai đoạn cách đều ra vấn đề, không ra liên kết |
| Xong, chờ ký | Việc S2-10 | `PipeCatalog` và hai tệp `data/catalog/asme-b36.10.json`, `asme-b16.9.json` sinh từ `tools/make_catalog.py`, DN15 đến DN600: đường kính ngoài, dày STD và XS, co 90 LR và SR, co 45, tê, côn thu, nắp. Số liệu chép tay, tệp ghi `verified: false` cho đến khi S2-16 ký |
| Xong | Việc S2-11 | `PanelViewModel` và `IHostActions` trong `src/PipingAgent.UI`, không tham chiếu AutoCAD. `MainPanel` năm thẻ, bảng danh sách, nút Quét, thanh tiến độ và nút Hủy |
| Xong | Việc S2-12 | `PA_SCAN` và `PA_SCANWINDOW` chạy `RecognitionPipeline` sau khi đọc, in báo cáo ra dòng lệnh và đổ vào bảng. Đã chạy trong accoreconsole trên bản vẽ thật: 19.054 đối tượng, 13,3% chưa phân loại, 2.723 đoạn chạy, 1,0 giây |
| Xong, chưa thử tay | Việc S2-13 | `HighlightService`: đồ hoạ tạm thời `TransientManager`, phóng theo vùng giữ tỉ lệ khung nhìn, xoá tô sáng. Bấm dòng ở thẻ Tuyến ống hoặc Vấn đề thì phóng và tô sáng. Chưa bấm thử trong giao diện vì cần người sáng lập |
| Xong | Việc S2-14 | `AcadProgress`: thước tiến độ của AutoCAD, Esc hoặc nút Hủy của bảng đều hủy qua `CancellationToken`. `HostThread.Post` đưa việc về ngữ cảnh lệnh |
| Xong, chưa thử hai bản vẽ | Việc S2-15 | `SessionManager` giữ một `DocumentSession` cho mỗi bản vẽ, bỏ khi đóng, đổi dữ liệu bảng khi chuyển bản vẽ. Chưa thử mở hai bản vẽ trong giao diện |
| Chờ người ngoài | Việc S2-16, S2-17 | Cần chuyên gia đường ống |
| Xong một phần | Việc S2-18 | Đã chạy trên bản vẽ thật đầu tiên bằng `PipingAgent.Cli recognize`, xem mốc M1 dưới đây. Hai bản vẽ thật còn lại chưa có |

**Mốc M1 trên bản vẽ thật đầu tiên, ngày 10/11/2026.** Tỷ lệ chưa phân loại 13,3%, dưới ngưỡng 30% nên rủi ro R2 chưa kích hoạt. Phần chưa phân loại tập trung ở layer `0`, `HA1`, `Aa_Pipe Trench`, `PLIN`, `BALLOON`, toàn nét vẽ nền và mương ống, chưa rõ vai trò. Nhãn: 2.004 chữ trên layer nhãn, 1.096 khớp mẫu gồm 625 nhãn tuyến và 471 cao độ rời, 908 chưa khớp phần lớn là số đơn, ghi chú, cỡ côn `6"x4"`. Gắn nhãn: 409 trong 1.096. Mô hình: 126 tuyến, 2.723 đoạn chạy nhưng chỉ 207 có nhãn. Nguyên nhân chính: bản vẽ thật vẽ ống bằng nét rời bị ngắt tại chỗ đặt block phụ kiện, khoảng 70% đầu mút đoạn chạy cách đầu mút của đoạn khác dưới 200 mm. Vì vậy số đoạn chạy gấp hơn mười lần số tuyến và phần lớn không có nhãn, 2.516 vấn đề R-TAG-01. Kết luận cho Sprint 3: việc S3-03 phải nối đoạn chạy qua block phụ kiện bằng bảng `blocks` của hồ sơ, và S3-02 lan truyền nhãn qua các đoạn đã nối. 59 vấn đề R-TAG-05 cho thấy gắn theo khoảng cách còn chọn nhầm khi nhiều tuyến chạy song song, cần xét hướng chữ và thứ tự dọc tuyến ở S3-09.

**Điểm cần chú ý.**
- Cuối sprint này là mốc M1. Nếu tỷ lệ chưa phân loại trên bản vẽ thật đầu tiên vượt 30% thì đó là dấu hiệu của rủi ro R2. Dành Sprint 3 để mở rộng hồ sơ quy ước trước khi làm tiếp.
- S2-18 ghi cho `KH` vì đây là việc quan sát kết quả cùng khách hàng. Nếu `KH` không rành kỹ thuật thì `LÕI` làm và lấy từ phần dự phòng.

### Sprint 3: đồ thị 3D và ống đầu tiên trong AutoCAD

**Mục tiêu.** Có mô hình trung gian IR đầy đủ kết nối 3D cho ống, co, tê, ống đứng. Dựng được ống thật trong AutoCAD.

**Trình diễn cuối sprint.** Gõ `PA_BUILD` trên bản vẽ mẫu. AutoCAD tạo bản vẽ mới có ống, co, tê dạng solid, chia layer theo tuyến. Bấm Undo một lần thì mọi thứ biến mất.

| Mã | Việc | Kết quả phải có | Vai trò | Công | Phụ thuộc | Truy vết |
|---|---|---|---|---|---|---|
| S3-01 | Ngữ nghĩa cao độ: tâm ống, đáy ống, đỉnh ống. Quy đổi về cao độ tâm theo đường kính ngoài | Kiểm thử ba ngữ nghĩa với DN150. Thiếu khai báo thì tạo vấn đề chặn | LÕI | 0,5 | S2-08, S2-10 | FR-REC-07, R-ELV-05 |
| S3-02 | Lan truyền thuộc tính: đoạn chạy không nhãn nối thẳng với đoạn có nhãn thì thừa hưởng tuyến và DN, có đánh dấu là suy ra | Ca dựng sẵn cho kết quả đúng và cờ suy ra được đặt | LÕI | 1 | S2-08 | FR-REC-04 |
| S3-03 | Dựng đồ thị kết nối 3D: nút tại đầu mút, điểm gãy, điểm giao. Cạnh là đoạn ống thẳng | Trên bản vẽ mẫu ra 26 đoạn ống. Hai đoạn khác cao độ cắt nhau trên mặt bằng không bị nối nhầm | LÕI | 2 | S2-07, S3-01 | Hạng mục 1.7, FR-REC-05 |
| S3-04 | Suy ra ống đứng khi hai đoạn chạy khác cao độ có chung điểm trên mặt bằng. Đối chiếu ký hiệu ống đứng nếu có | 3 ống đứng trên bản vẽ mẫu. Có vấn đề khi ký hiệu và hình học không khớp | LÕI | 1 | S3-03 | FR-REC-06, R-ELV-01, 02 |
| S3-05 | Phân loại nút: co 90 độ, tê, đầu hở. Nút đầu chờ để Sprint 4 | 15 co và 3 tê trên bản vẽ mẫu | LÕI | 1 | S3-03 | FR-REC-08 |
| S3-06 | Áp kích thước phụ kiện từ danh mục: tính phần co và tê chiếm chỗ, cắt đoạn ống thẳng tương ứng | Tổng chiều dài theo đường tâm bằng 115,9 m. Chiều dài từng đoạn thẳng sau khi trừ phụ kiện không âm | LÕI | 1 | S3-05, S2-10 | FR-REC-17, R-GEO-02 |
| S3-07 | Ghi nguồn gốc cho mọi phần tử IR: đối tượng nguồn, quy tắc đã áp, điểm tin cậy, cờ suy ra | Không phần tử nào trong IR thiếu nguồn gốc | LÕI | 0,5 | S3-03 | FR-REC-16 |
| S3-08 | Cô lập lỗi: đối tượng gây lỗi thành một vấn đề, phần còn lại vẫn xử lý | Bản vẽ có một polyline hỏng vẫn ra IR cho các tuyến khác | LÕI | 0,5 | S3-03 | FR-REC-10 |
| S3-09 | Chạy chuỗi nhận diện đến đồ thị trên bản vẽ thật. Ghi lại sai lệch so với đáp án | Bảng lỗi theo loại cho từng bản vẽ thật | LÕI | 0,5 | S3-05, S1-18 | R2, R3 |
| S3-10 | Khung bộ dựng: giao diện dựng mô hình, tùy chọn dựng, xem trước số lượng theo loại mà không đổi bản vẽ | Xem trước trả đúng số ống, co, tê của IR. Dấu vân tay bản vẽ không đổi | CAD | 1 | S1-01 | Hạng mục 1.13, FR-BLD-06, 11 |
| S3-11 | Dựng đoạn ống thẳng: solid hình trụ theo đường kính ngoài, đặt theo trục đoạn ống | Hộp bao của solid khớp tính toán trong 0,1 mm, cho cả ống ngang và ống đứng | CAD | 1 | S3-10 | FR-BLD-01, NFR-ACC-01 |
| S3-12 | Dựng co: quét tiết diện tròn theo cung tâm co | Co nối khít hai đoạn ống ở mọi hướng trong không gian | CAD | 1,5 | S3-11 | FR-BLD-01 |
| S3-13 | Dựng tê: hợp hai hình trụ | Tê đúng hướng nhánh | CAD | 0,5 | S3-11 | FR-BLD-01 |
| S3-14 | Tổ chức layer theo tuyến và loại đối tượng, tên theo mẫu cấu hình được | Mỗi tuyến một nhóm layer. Đổi mẫu tên trong cấu hình thì tên layer đổi theo | CAD | 0,5 | S3-11 | FR-BLD-03 |
| S3-15 | Gắn dữ liệu vào từng solid: số hiệu tuyến, DN, loại, mã phần tử IR, handle nguồn 2D | Chọn một solid bất kỳ đọc lại được đủ năm trường | CAD | 1 | S3-11 | FR-BLD-04, mục 10.1.5 |
| S3-16 | Gói cả lần dựng vào một nhóm hoàn tác. Lỗi giữa chừng thì hủy toàn bộ | Một lần Undo đưa bản vẽ về trạng thái trước. Tiêm lỗi giữa chừng không để lại đối tượng dở dang | CAD | 1 | S3-11 | FR-BLD-05, NFR-REL-02 |
| S3-17 | Dựng vào bản vẽ mới, tham chiếu bản 2D làm nền. Tùy chọn dựng vào chính bản đang mở trên layer riêng | Cả hai chế độ chạy được. Bản 2D gốc không đổi | CAD | 1,5 | S3-16 | FR-BLD-02, ADR-08, Q5 |
| S3-18 | Lập đáp án cho hai bản vẽ thật tiếp theo. Rà danh sách luật giai đoạn 1 ở chương 9 | Hai thư mục mới. Ghi chú góp ý cho từng luật: giữ, sửa ngưỡng, hay bỏ | ỐNG | 2,5 | S0-04 | Hạng mục 1.18, 1.9 |

{{TONG:S3}}

**Tiến độ, cập nhật ngày 10/11/2026.** Phần lập trình làm trước ngày bắt đầu sprint.

| Trạng thái | Việc | Ghi chú |
|---|---|---|
| Xong | Việc S3-01 | `ElevationResolver`: CL giữ nguyên, BOP cộng nửa đường kính ngoài, TOP trừ, TOS cộng chiều cao đỡ và nửa đường kính ngoài. Kiểm thử ba ngữ nghĩa với DN150. Thiếu mốc hoặc thiếu DN thì tạo R-ELV-05 mức chặn và bỏ đoạn chạy đó khỏi đồ thị, các đoạn khác vẫn dựng. Hồ sơ có `defaultSupportHeight` cho TOS |
| Xong | Việc S3-02 | `TagPropagator`: thẳng hàng 0,85, qua co 0,75, không qua tê. Cờ suy ra được đặt. Giai đoạn hai lan truyền cao độ giữa các đoạn cùng tuyến. Thêm `FittingBridger` nối đoạn chạy bị ngắt tại block phụ kiện theo kết luận M1, và `GapCloser` khép khe hở nhỏ theo R-CON-02; cả hai bật bằng `fittingReach` và `gapTolerance` trong hồ sơ |
| Xong | Việc S3-03 | `GraphBuilder`: nút tại đầu mút, điểm gãy, điểm đâm vào; tách đoạn tại tê; hai đoạn khác cao độ cắt nhau trên mặt bằng không nối, cùng cao độ mà không có nút thì R-CON-03. Bản vẽ mẫu ra đúng 26 đoạn ống |
| Xong | Việc S3-04 | Ống đứng suy ra khi hai nút trùng mặt bằng khác cao độ và ít nhất một là đầu mút. 3 ống đứng trên bản vẽ mẫu, khớp 3 ký hiệu. Không có ký hiệu thì R-ELV-01, ký hiệu thừa thì R-ELV-02 |
| Xong | Việc S3-05 | Nút bậc một là đầu hở, bậc hai là co với góc, bậc ba là tê với nhánh là đoạn không thẳng hàng. 15 co và 3 tê trên bản vẽ mẫu. Góc ngoài tập `allowedBendAngles` thì R-GEO-01, bốn nhánh thì R-CON-04 |
| Xong | Việc S3-06 | Co 90 LR cắt `A`, co 45 cắt `B`, tê cắt `C` trên nhánh chạy và `M` trên nhánh rẽ. Tổng chiều dài tâm 115,9 m, đúng số của kế hoạch. Đoạn ngắn hơn phần cắt thì R-GEO-02 |
| Xong | Việc S3-07 | Mọi đoạn ống và nút có `prov` với handle nguồn, mã quy tắc `REC-SEG-01`, `REC-SEG-02`, `REC-NODE-01`; ống đứng đánh dấu suy ra. Kiểm thử trên bản vẽ mẫu xác nhận không phần tử nào thiếu |
| Xong | Việc S3-08 | Mỗi đoạn chạy xử lý trong try/catch riêng, lỗi thành R-GEO-05 và các đoạn khác vẫn vào đồ thị. Kế hoạch dựng cũng bỏ qua từng phần tử lỗi thay vì dừng |
| Xong một phần | Việc S3-09 | Bản vẽ thật: 2.723 đoạn chạy, sau nối qua block 1.160 chỗ và khép 451 khe hở thì 126 đoạn thừa hưởng nhãn. Chỉ 71 đoạn chạy có đủ cao độ để vào đồ thị, ra 94 đoạn ống, 40 co, 2 tê, 425,8 m. Nguyên nhân chính: cao độ ghi rời rạc, 471 chữ cao độ chỉ gắn được cho 118 đoạn chạy. Chưa có đáp án để so vì S1-18 chưa làm |
| Xong | Việc S3-10 | `BuildPlan.FromModel` trong lõi: danh sách ống, co, tê với toạ độ đã trừ phụ kiện, không đổi IR. `PA_BUILD` mặc định `Xemtruoc` in số lượng, dấu vân tay không đổi vì không chạm cơ sở dữ liệu |
| Xong | Việc S3-11 | `AcadBuilder.Pipe`: `Solid3d.CreateFrustum` rồi xoay theo trục đoạn ống. Chưa đo hộp bao so với tính toán trong 0,1 mm bằng máy, mới kiểm tra số lượng |
| Xong | Việc S3-12 | Co: tiết diện tròn tại điểm tiếp, xoay quanh trục qua tâm cung đúng góc uốn, chiều xoay chọn tự động để tới điểm tiếp bên kia |
| Xong | Việc S3-13 | Tê: hợp hai hình trụ bằng `BooleanOperation` |
| Xong | Việc S3-14 | Layer `PA-{line}-{kind}` đổi được bằng `BuildOptions.LayerPattern`, màu theo tuyến. Bản vẽ mẫu ra 17 layer |
| Xong | Việc S3-15 | Xdata `PIPINGAGENT`: tuyến, DN, loại, mã IR, handle nguồn. Đọc lại bằng `entget` trong accoreconsole đủ năm trường |
| Xong | Việc S3-16 | Cả lần dựng trong một giao dịch của một lệnh. Thử trong accoreconsole: dựng 44 solid, `UNDO 1` còn 0 solid; tiêm lỗi sau 3 phần tử bằng từ khoá `Loithu` thì không để lại solid nào. Kịch bản `build\test_build.ps1` |
| Xong một phần | Việc S3-17 | `Moi` dựng vào bản vẽ mới, ghi `<tên>-3D.dwg` cạnh bản 2D và mở nếu có giao diện; `Hientai` dựng vào bản đang mở. Tham chiếu bản 2D làm nền chỉ nhận DWG và bị `eFileAccessErr` trong accoreconsole vì tệp đang mở; chưa thử trong giao diện |
| Chờ người ngoài | Việc S3-18 | |

**Kết quả trình diễn.** Trên bản vẽ mẫu, `PA_SCAN` rồi `PA_BUILD Hientai` trong accoreconsole tạo 26 ống, 15 co, 3 tê, tức 44 solid trên 17 layer; một lần Undo bỏ hết. Chưa xem bằng mắt trong giao diện.

**Điểm cần chú ý.**
- Cuối sprint là mốc M2. Tính hệ số giữa ngày công thực tế và ước lượng của ba sprint đầu, áp cho Sprint 4 đến 6.
- S3-03 là việc khó nhất của giai đoạn. Nếu bản phân tích ở S0-12 cho thấy bản vẽ thật có ống xiên thì giả định ASM-05 sai và việc này phải tăng thêm công.
