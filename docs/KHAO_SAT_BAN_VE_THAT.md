# Khảo sát bản vẽ thật, đợt 1

| Mục | Giá trị |
|---|---|
| Việc | S0-12 và một phần S0-17 trong [`KE_HOACH_SPRINT.md`](KE_HOACH_SPRINT.md) |
| Quyết định liên quan | Q13, Q14 trong [`TAI_LIEU_DU_AN.md`](TAI_LIEU_DU_AN.md), mục 14.2 |
| Ngày | 10/11/2026 |
| Trạng thái | Phân tích bằng công cụ xong trên một bản vẽ. Chờ chuyên gia đường ống đọc cùng theo việc S0-19 |
| Dữ liệu gốc | `docs/dataTemplate/`, đã loại khỏi kho mã. Bản DXF xuất ra ở `data/private/`. Không chép nội dung bản vẽ vào tài liệu này ngoài tên layer, tên block và dạng mẫu của chữ |

## 1. Bộ dữ liệu nhận được

| Tệp | Loại | Ghi chú |
|---|---|---|
| `A410B4001.dwg` | Bản vẽ mặt bằng đường ống, DWG định dạng AutoCAD 2007, 1,7 MB | Đối tượng chính của khảo sát này. Khổ A0, tỉ lệ 1/60, đơn vị mm. Bốn tờ nằm chung trong một không gian mô hình, mỗi tờ một khung tên |
| `335-PA-286.pdf` | Bản vẽ isometric một tuyến ống, PDF vector, 1 trang | Có bảng vật tư, toạ độ E/N/EL, số hiệu tuyến, mã cấp vật liệu. Dùng để đối chiếu đầu ra ở giai đoạn sau, chưa phải đầu vào của giai đoạn 1 |
| `P&ID.pdf` | Sơ đồ P&ID một cụm thiết bị, PDF vector, 11 trang | Trang 3 là bảng chú giải ký hiệu và chữ viết tắt. Trang 4 trở đi là sơ đồ. Là nguồn chú giải riêng, tách khỏi bản vẽ mặt bằng |

Ba tệp thuộc hai dự án khác nhau, nên không đối chiếu chéo được giữa mặt bằng và isometric.

Bản vẽ mặt bằng được xuất sang DXF bằng `accoreconsole` của AutoCAD 2025 với lệnh `DXFOUT`, độ chính xác 16 chữ số. Số liệu dưới đây lấy từ `tools/dxf_survey.py` chạy trên bản DXF đó. Kết quả đọc bằng thư viện .NET ở [`DANH_GIA_THU_VIEN_DXF.md`](DANH_GIA_THU_VIEN_DXF.md).

## 2. Số liệu tổng quát

| Chỉ số | Giá trị |
|---|---|
| Đối tượng trong không gian mô hình | 19.054 |
| Theo loại | LINE 9.095, INSERT 4.705, TEXT 2.095, LWPOLYLINE 854, CIRCLE 702, ARC 385, DIMENSION 375, LEADER 339, ELLIPSE 243, SOLID 117, POINT 74, MULTILEADER 57, HATCH 7, SPLINE 6 |
| Layer định nghĩa, có đối tượng | 61, 48 |
| Định nghĩa block, được chèn, lần chèn | 847, 433, 4.705 |
| Xref, đối tượng proxy, MTEXT | Không có |
| Không gian giấy | Một layout tên tiếng Nhật, 2 viewport. Nội dung nằm hết ở không gian mô hình |
| Chiều cao chữ phổ biến | 125 đơn vị, tức 2,1 mm trên giấy ở tỉ lệ 1/60. Khung tên và tên thiết bị dùng 300 |

## 3. Quy ước nhận ra được

### 3.1 Layer

Layer đặt tên theo chức năng nhưng không sạch. Một layer chứa nhiều loại nội dung, và nhiều nội dung nằm trên layer `0`.

| Layer | Nội dung suy đoán | Số đối tượng | Ghi chú |
|---|---|---|---|
| `LINE-5` | Ống chính, nét đơn | 5.202 | 2.515 LINE và 2.628 INSERT. Các INSERT là mũi tên dòng chảy và dấu mối nối, không phải cấu kiện |
| `LINE-2`, `LINE-1`, `LINE1`, `LINE2` | Ống nhóm khác, có thể theo cỡ hoặc tình trạng | 1.076, 244, 11, 87 | `LINE-2` chỉ 61% nét trực giao, cần chuyên gia giải thích |
| `CENT`, `CEN2`, `Center`, `S_CEN` | Đường tâm | 163, 483, 35, 6 | `CEN2` gần như toàn INSERT |
| `TEXT` | Số hiệu tuyến, cao độ, ghi chú | 1.878 | Nguồn chính của nhãn |
| `DIMN` | Kích thước, đường dẫn, chữ rời | 897 | Có 134 TEXT là mảnh của nhãn: mã cấp vật liệu, số thứ tự, cao độ |
| `LEADER` | Đường dẫn và vòng tròn ghi chú | 1.552 | 1.166 LINE, 153 CIRCLE, 129 LEADER, 40 MULTILEADER |
| `FITT2`, `FITT` | Phụ kiện vẽ bằng nét rời | 689, 10 | 175 ELLIPSE, 276 LINE, 103 LWPOLYLINE |
| `SYM1`, `SYM2` | Ký hiệu nét rời | 242, 2 | 201 CIRCLE |
| `WELD` | Mối hàn | 116 | LWPOLYLINE và ARC |
| `EQUIP`, `EQUIPMENT`, `EQ`, `EQPLIN` | Thiết bị | 417, 126, 25, 40 | Tên thiết bị lại nằm trên layer `0` |
| `STRUCTURE`, `STRLIN`, `BEAM`, `SUPPORT` | Kết cấu và giá đỡ | 1.311, 251, 10, 902 | |
| `MREV`, `MARK` | Dấu sửa đổi, dấu đánh số | 730, 230 | Gần như toàn INSERT |
| `0` | Lẫn lộn | 834 | Có 62 TEXT gồm tên thiết bị, tên trục, toạ độ lưới |

### 3.2 Nhãn tuyến ống

399 chữ theo dạng `<cỡ>"-<mã lưu chất>-<mã đơn vị>-<số thứ tự>-<mã cấp vật liệu>-<mã cách nhiệt>(<dày cách nhiệt>)`. Ví dụ đã che: `2"-XXX-nnn-nnnn-A1AS-HS(40)`.

- Cỡ ống ghi theo inch với dấu `"`, có dạng phân số như `3/4"` và `1-1/2"`.
- Phần dày cách nhiệt trong ngoặc có lúc thiếu, có lúc viết liền không ngoặc, có lúc là `(xx)` hoặc `XXXX` vì chưa chốt.
- Có nhãn bị cắt làm nhiều TEXT trên layer `DIMN`: mã cấp vật liệu, số thứ tự và cỡ nằm rời nhau. Quy tắc gộp chữ theo khoảng cách ở FR-REC là cần thiết.
- Một tuyến có thể xuất hiện nhiều lần trên bản vẽ với cùng số hiệu.

### 3.3 Cao độ

510 chữ có `EL`. Dạng phổ biến: `EL 109000 BOP`, `PF EL 107000`, `EL 108000 TOS ~ EL 110000`.

- Giá trị tuyệt đối theo mm, cao độ nhà máy, không có dấu `+`.
- Mốc cao độ là **đáy ống** `BOP`, **mặt trên giá đỡ** `TOS`, hoặc **sàn** `PF`. Không thấy mốc tâm ống. Để ra tâm ống phải biết đường kính ngoài và chiều dày đỡ, nghĩa là cần danh mục ống ngay từ bước đọc cao độ.
- Dạng `EL a TOS ~ EL b` là khoảng cao độ của một giàn ống nhiều tầng, không phải cao độ một tuyến.

### 3.4 Ký hiệu cấu kiện

Cấu kiện vẽ chủ yếu bằng block. Tên block mã hoá loại, cỡ và kiểu nối theo một quy ước riêng của nhà thầu, ví dụ dạng `e<cấp>C<cỡ>swM`, `t<cỡ>x<cỡ>M`, `r<cỡ>X<cỡ>M`, `f<...>M`, `v<...>M`, `pc<...>M`, `sh<...>`, `olet<...>`.

| Tiền tố | Suy đoán | Lần chèn |
|---|---|---|
| `e` | Co | khoảng 1.500 |
| `f` | Mặt bích | khoảng 350 |
| `t` | Tê | khoảng 80 |
| `r` | Côn thu | khoảng 50 |
| `v`, `x`, `w` | Van các loại | khoảng 150 |
| `pc`, `h`, `n` | Nút bịt, nối, nối nhỏ, chưa rõ | khoảng 250 |
| `sh`, `su`, `SUPPORT`, `L`, `PL`, `HW`, `WH` | Giá đỡ và thép hình | khoảng 700 |
| `WL`, `FlowLine_green`, `yajirusi` | Mũi tên dòng chảy, dấu mối nối | 935 |
| `DRAIN`, `vent`, `steamtrap` | Cụm xả, xả khí, bẫy hơi | khoảng 150 |

Nhận xét cho mục 7.9 của tài liệu dự án:

- 433 tên block khác nhau, viết hoa thường không nhất quán, có cả tên vô danh dạng `A$C...`. Bảng `blocks` trong hồ sơ quy ước phải cho phép **khớp theo mẫu** trên tên block, không chỉ khớp tên chính xác, và phải không phân biệt hoa thường.
- Không block cấu kiện nào có thuộc tính. Chỉ khung tên có 65 thuộc tính, gồm tên bản vẽ, tỉ lệ, số tờ, đơn vị. Vậy nguồn bằng chứng "thuộc tính block" của mục 7.9.3 không dùng được cho bản vẽ này, chỉ dùng được cho siêu dữ liệu của tờ vẽ.
- Nhiều block lồng một đến hai cấp, một số lồng đến 13 cấp. Bộ đọc phải nổ block đệ quy hoặc đọc theo tên block ngoài cùng.
- 57 MULTILEADER đều không có chữ, chỉ là mũi tên. Chữ của chúng là TEXT riêng gần đầu mũi tên. Luật gắn chú thích phải xét cả trường hợp này.

### 3.5 Khung tên và nhiều tờ trong một tệp

Bốn tờ vẽ cùng nằm trong không gian mô hình, mỗi tờ là một block khung `NSRP_1_50` chèn ở tỉ lệ 60 kèm block khung tên `nsrp001` có thuộc tính. Tên bản vẽ, tỉ lệ, số tờ đọc được từ thuộc tính này. Tài liệu dự án đang ngầm coi một tệp là một tờ. Cần thêm bước **tách tờ theo khung tên** trước khi nhận diện, nếu không nhãn của tờ này sẽ gắn nhầm vào ống của tờ kia ở vùng giáp ranh.

### 3.6 Mức độ sạch

- 441 nét trên layer ống ngắn hơn 100 mm, có nét dài 0. Cần bước làm sạch hình học.
- Chữ dùng mã `%%U`, `%%C`, `%%%` của AutoCAD. Cần chuẩn hoá chữ trước khi khớp mẫu.
- Tên layout tiếng Nhật và mã trang `ANSI_1252`. Ba thư viện .NET đều đọc đúng, không lỗi mã hoá.
- Không có xref, không có đối tượng proxy. Đây là trường hợp dễ. Bản vẽ của nhà thầu khác có thể khó hơn.

## 4. Đối chiếu sáu giả định ở mục 5.10

| Giả định | Kết luận | Căn cứ |
|---|---|---|
| ASM-01 Bản vẽ vector, có layer dùng được | **Đúng, có điều kiện** | Vector hoàn toàn. 61 layer có tên theo chức năng nhưng trộn nội dung, và 834 đối tượng trên layer `0` gồm cả tên thiết bị. Hồ sơ quy ước phải cho một loại nội dung nằm trên nhiều layer và một layer chứa nhiều loại |
| ASM-02 Cao độ và đường kính ghi bằng chữ trên mặt bằng | **Đúng, nhưng khác giả định về mốc** | 510 chữ cao độ, 399 nhãn tuyến có cỡ. Mốc cao độ là đáy ống hoặc mặt giá đỡ, không phải tâm ống như prototype giả định. Phải thêm trường mốc cao độ vào hồ sơ quy ước và vào IR |
| ASM-03 Quy ước nhất quán giữa các tờ trong một dự án | **Chưa đủ dữ liệu** | Bốn tờ trong tệp này dùng chung layer, block và dạng nhãn. Chưa có tờ thứ hai của cùng dự án ở tệp khác để so |
| ASM-04 Khách hàng chấp nhận solid có thuộc tính ở giai đoạn đầu | **Chưa đủ dữ liệu** | Không suy ra được từ bản vẽ |
| ASM-05 Ống chạy chủ yếu trực giao | **Đúng phần lớn** | Layer ống chính: 92,4% nét trực giao, 3,6% nét 45 độ, 4,0% góc khác. Layer ống phụ `LINE-2` chỉ 61% trực giao. Một phần nét xiên có thể là nét vẽ phụ chứ không phải ống, cần chuyên gia xác nhận |
| ASM-06 Có khách hàng cung cấp bản vẽ thật | **Đúng** | Đã có một bản vẽ mặt bằng, một isometric và một P&ID |

## 5. Số liệu cho hai quyết định mở

### Q13: giữ phạm vi ống trực giao ở giai đoạn 1

Đề xuất: **giữ**, nhưng mở rộng thành trực giao cộng 45 độ trên mặt bằng, vì nét 45 độ chiếm 3,6% trên layer ống chính và là trường hợp co 45 độ phổ biến. Nét ở góc khác, khoảng 4%, giai đoạn 1 đánh dấu "chưa hỗ trợ" và bỏ qua theo FR-REC-10, không dựng sai. Ống dốc không nhận ra được từ bản vẽ mặt bằng này, vì cao độ chỉ ghi tại điểm, phù hợp với việc để sang giai đoạn 2.

### Q14: kéo nhận diện ký hiệu nét rời lên giai đoạn 1 không

Số liệu: 4.705 lần chèn block so với khoảng 1.000 nét rời trên các layer ký hiệu `FITT2`, `FITT`, `SYM1`, `SYM2`, `WELD`. Nếu mỗi ký hiệu nét rời gồm 3 đến 5 nét thì có khoảng 200 đến 350 ký hiệu nét rời, tức **5 đến 7%** số ký hiệu. Trong đó 175 ELLIPSE trên `FITT2` nhiều khả năng là mặt bích hoặc co nhìn nghiêng, cần chuyên gia xác nhận.

Đề xuất: **không kéo FR-CLS-05 lên giai đoạn 1**. Thay vào đó, thêm vào giai đoạn 1 việc khớp tên block theo mẫu, vì đó mới là điểm nghẽn thật trên bản vẽ này: 433 tên block, không block nào có thuộc tính. Ký hiệu nét rời để giai đoạn 2 như kế hoạch, và giai đoạn 1 báo chúng là "chưa phân loại" để người dùng xử lý tay.

## 6. Việc phát sinh đề xuất cho kế hoạch

| Việc | Lý do | Gợi ý nơi đặt |
|---|---|---|
| Khớp tên block theo mẫu trong hồ sơ quy ước | Mục 3.4 | Sprint 4 hoặc 5, cùng FR-PRF |
| Trường mốc cao độ `BOP`, `TOS`, `COP`, `PF` trong hồ sơ quy ước và IR, kèm phép đổi về tâm ống bằng danh mục ống | Mục 3.3 | Sprint 1 cho IR, Sprint 5 cho luật |
| Tách tờ theo khung tên trong một không gian mô hình | Mục 3.5 | Sprint 3 hoặc 4, trước bước gắn nhãn |
| Gộp mảnh nhãn rời trên layer kích thước | Mục 3.2 | Đã có trong FR-REC, cần ca kiểm thử từ bản vẽ này |
| Bộ dữ liệu vàng đầu tiên lấy từ bản vẽ này sau khi ẩn danh | ASM-06 | Sprint 1, việc chuẩn bị dữ liệu |

Những việc này chưa đưa vào kế hoạch sprint. Chờ người sáng lập duyệt và chuyên gia đường ống xác nhận các suy đoán ở mục 3.

## 7. Điều chưa làm

- Chưa có chuyên gia đường ống đọc cùng. Mọi suy đoán về ý nghĩa tiền tố block, layer `LINE-2` và các ELLIPSE đều chưa được xác nhận.
- Chưa mở bản vẽ bằng giao diện AutoCAD để nhìn trực quan. Mọi số liệu lấy từ dữ liệu, không từ hình ảnh.
- Chưa đọc isometric và P&ID bằng công cụ, chỉ xem bằng mắt để phân loại.
- Chưa ẩn danh bản vẽ. Chưa được đưa vào bộ dữ liệu vàng.
