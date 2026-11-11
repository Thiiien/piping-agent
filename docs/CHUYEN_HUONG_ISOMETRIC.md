# Chuyển hướng: isometric sang 3D

| Mục | Nội dung |
|---|---|
| Ngày | 10/11/2026 |
| Quyết định | Người sáng lập thu hẹp sản phẩm từ "mặt bằng 2D sang 3D" thành "**isometric sang 3D**", nhận đầu vào PDF và DWG |
| Lý do | Mặt bằng thật `A410B4001` sau một ngày chỉnh dựng được 707 trong 2.723 đoạn chạy, còn 2.016 đoạn không có cao độ ở bất kỳ đâu trong mặt bằng. Thông tin đó nằm ở isometric. Isometric sinh từ hệ thống 3D chứa đủ toạ độ, cao độ, kích thước, cỡ và phụ kiện |
| Trạng thái | Người sáng lập duyệt ngày 10/11/2026. I1 làm xong cùng ngày, xem mục 7 |

## 1. Khảo sát bản isometric mẫu `335-PA-286.pdf`

Trích bằng `tools/iso_survey.py` và `tools/iso_extract.py`, ngày 10/11/2026.

| Thuộc tính | Kết quả |
|---|---|
| Dạng | PDF vector, 1 trang A3 ngang, 1.664 nét thẳng, 722 từ trích được, phông Arial. Ba ảnh là logo |
| Nguồn | Isometric sinh tự động từ hệ thống 3D: có `ISOMETRIC ID`, `EXTRACTION DATE`, bảng vật tư theo mã vật tư |
| Trục | Nét tập trung ở 90°, 30°, 150°, đúng ba trục isometric. Hình không theo tỉ lệ, kích thước ghi là số thật |
| Nét ống | 12 nét dày 2,88 pt, nằm đúng ba trục, nối thành một chuỗi liền |
| Điểm neo | 4 điểm nối tiếp có toạ độ tuyệt đối `E`, `N`, `EL` mm. Hai neo đầu tuyến cách nhau 2.409 mm theo Đông; tổng kích thước ghi trên đoạn 143+1375+279+279+330 = 2.406 mm, khớp |
| Kích thước | 82 số ứng viên, có đường gióng và mũi tên kèm theo |
| Nhãn | Số hiệu tuyến `nnn-PA-nnn-nn"-...`, cỡ từng đoạn `14"NPD`, cỡ phụ kiện `14X10"NPD`, cao độ `EL +106461`, mã nối tiếp `SEE ISO ...` với toạ độ |
| Bảng vật tư | 17 dòng: ống, tê, côn, sockolet, co 90 LR, nipple, mặt bích, gioăng, bu lông, van, giá đỡ. Có cỡ inch, số lượng, cấp vật liệu |
| Khác | Số thứ tự phụ kiện trong ô vuông trên hình trỏ tới dòng bảng vật tư. Bảng điều kiện thiết kế ở chân trang |

Chữ sở hữu của chủ đầu tư nằm trên bản vẽ. Không chép nội dung bản vẽ vào kho mã; chỉ dùng tại chỗ để phát triển.

## 2. Vì sao bài toán này đặt đúng

| | Mặt bằng 2D | Isometric |
|---|---|---|
| Cao độ | Ghi thưa, phải suy ra | Ghi tại mọi điểm đổi cao độ, cộng neo tuyệt đối |
| Chiều dài đoạn | Đo từ hình | Ghi thành số, hình không cần đúng tỉ lệ |
| Phụ kiện | Block theo quy ước riêng, chưa giải mã | Ký hiệu chuẩn cộng số thứ tự trỏ tới bảng vật tư có cỡ, loại |
| Một tờ | Nhiều tờ, nhiều tuyến lẫn nhau | Một tuyến hoặc một phần tuyến, có điểm nối tiếp sang tờ khác |
| Kiểm tra khép kín | Không có | Neo toạ độ ở hai đầu cho phép kiểm tra tổng kích thước |

Giới hạn: isometric vẽ tay hoặc quét ảnh không nằm trong phạm vi giai đoạn đầu. Isometric từ phần mềm khác nhau có quy ước ký hiệu khác nhau, hồ sơ quy ước vẫn cần.

## 3. Cách làm và phần dùng lại

Chuỗi xử lý mới, các bước có dấu * dùng lại mã đã có:

1. **Đọc nguồn**: PDF vector bằng PyMuPDF trong một bộ đọc riêng, hoặc DWG bằng `DxfDrawingReader`* hiện có. Cả hai ra `SourceEntity`* phẳng: nét, chữ, độ dày, vị trí.
2. **Phân loại**: nét ống theo độ dày và trục; đường gióng, mũi tên; chữ theo mẫu. Dùng `Classifier`* và hồ sơ quy ước* với trường mới cho isometric.
3. **Đồ thị trên giấy**: nối nét ống thành chuỗi, mỗi đỉnh là điểm gãy hoặc điểm đặt phụ kiện. Dùng `CenterlineCleaner`*.
4. **Gán kích thước**: mỗi đoạn trên giấy nhận một số kích thước từ đường gióng gần, hướng theo trục. Cách gắn ba mức như `TagAttacher`*.
5. **Dựng toạ độ thật**: đi từ một neo, cộng kích thước theo trục, kiểm tra khép kín tại neo kế tiếp. Lệch quá dung sai thì tạo vấn đề. Ra `Run`, `Segment`, `Node`* trong Piping IR* với toạ độ 3D thật.
6. **Phụ kiện**: số thứ tự trong ô vuông ghép với bảng vật tư, đặt tại đỉnh tương ứng. Tra `PipeCatalog`* cho kích thước.
7. **Bộ luật*** chạy lại, thêm luật khép kín neo và luật tổng chiều dài ống bằng số mét trong bảng vật tư.
8. **Dựng 3D** bằng `AcadBuilder`* không đổi. Bảng điều khiển*, quyết định*, tệp phiên*, agent* dùng nguyên.

Ước lượng: khoảng 70% mã giai đoạn 1 dùng lại, phần mới là bước 1, 4, 5, 6.

## 4. Kế hoạch

| Sprint | Mục tiêu | Trình diễn |
|---|---|---|
| I1 | Bộ đọc PDF vector ra `SourceEntity`; phân loại nét ống, chữ, kích thước; đồ thị trên giấy cho bản mẫu | Dòng lệnh in chuỗi 12 đoạn với trục và kích thước đã gán, tổng khớp neo |
| I2 | Dựng toạ độ thật từ neo và kích thước; IR 3D; luật khép kín; `PA_BUILD` cho ra ống, co, tê đúng toạ độ E, N, EL | Mở isometric mẫu trong AutoCAD, dựng 3D, toạ độ đầu cuối trùng neo trong 5 mm |
| I3 | Phụ kiện từ bảng vật tư: côn, mặt bích, van, sockolet; đầu vào DWG isometric; hồ sơ quy ước cho isometric | Hai bản isometric, một PDF một DWG, dựng đủ phụ kiện; bảng vật tư sinh ra khớp bảng trên tờ |
| I4 | Nhiều tờ nối tiếp qua `SEE ISO`; xuất sang bản vẽ 3D chung; agent giải quyết chỗ mơ hồ; chạy trên bộ isometric thật của khách hàng | Ba tờ nối thành một tuyến liền trong một bản vẽ 3D |

Mỗi sprint hai tuần theo nhịp cũ, nhưng với tốc độ làm ngày 10/11 thì I1 và I2 có thể xong trong vài ngày nếu có thêm vài bản isometric mẫu.

## 5. Rủi ro

| Rủi ro | Cách giảm |
|---|---|
| Isometric từ phần mềm khác dùng ký hiệu, độ dày nét, bố cục bảng khác | Hồ sơ quy ước cho isometric, giống hồ sơ mặt bằng. Cần ít nhất hai nguồn isometric khác nhau sớm |
| PDF quét ảnh | Ngoài phạm vi. Báo rõ cho khách hàng, có thể thêm OCR sau |
| Kích thước gán nhầm đoạn | Kiểm tra khép kín bằng neo ở hai đầu; sai thì tạo vấn đề và hỏi người |
| Thiếu neo tuyệt đối trên một số tờ | Nối tiếp từ tờ trước qua `SEE ISO`, hoặc hỏi người một điểm gốc |

## 6. Việc cần người sáng lập

- Duyệt kế hoạch I1 đến I4 và mục tiêu trình diễn.
- Cung cấp thêm isometric: vài tờ cùng dự án, và nếu có, isometric từ phần mềm khác và một bản DWG isometric.
- Cập nhật tài liệu dự án và kế hoạch sprint cũ: giai đoạn 1 mặt bằng dừng ở mức đã đạt, giữ làm chức năng phụ; việc này tôi làm sau khi được duyệt.

## 7. Kết quả I1, ngày 10/11/2026

Mã: `src/PipingAgent.Iso` gồm `IsoPdfReader` đọc PDF vector bằng PdfPig ra `SourceEntity` có độ dày nét, và `IsoGraph` dựng đồ thị trên giấy, gán kích thước, đi từ neo ra toạ độ thật, đổ ra Piping IR. Lệnh `PipingAgent.Cli iso <pdf> --out <ir.json>`. Lệnh AutoCAD `PA_BUILDIR` nạp IR rồi `PA_BUILD` dựng.

Trên `335-PA-286.pdf`:

| Bước | Kết quả |
|---|---|
| Nét ống | 12 nét dày, nối 2 khe tại ký hiệu phụ kiện theo hướng thẳng hàng, còn đúng 2 đầu hở là hai đầu tuyến |
| Neo | 4 cụm E, N, EL. Neo của nhánh 3/4" vẽ nét mỏng được nhận ra là không thuộc tuyến chính nhờ quy tắc chữ nối tiếp nằm phía trước đầu hở |
| Kích thước | Số viết xoay theo trục lấy trục từ hướng chữ; số viết ngang đi theo đường chỉ tới đường kích thước. Ô số thứ tự phụ kiện, là hình bình hành xoay, được loại bằng vòng bốn nét khép |
| Ràng buộc | Chữ `EL` rời và neo làm ràng buộc; tổng kích thước trên chuỗi đoạn cùng trục phải bằng hiệu toạ độ, sai thì chọn lại tổ hợp. Nhờ đó 195, 150 của nhánh bị loại khỏi trục Đứng, 61 và 335 khỏi trục Bắc |
| Khép kín | Neo `SEE ISO 335-PA-288`: lệch 3 mm. Neo `SEE ISO 335-WG-157`: lệch 4,2 mm. Cao độ trung gian 106461 và 105828 khớp tuyệt đối. Sai số còn lại là khe gioăng 3 mm chưa đọc |
| 3D | `335-PA-286-3D.dwg`: 5 ống, 1 tê 14x14, 1 co 90 LR, toạ độ thật E, N, EL mm |

Chưa làm, thuộc I2 và I3: côn thu, mặt bích, van, sockolet và nhánh 3/4" từ bảng vật tư; khe gioăng 3 mm; đầu vào DWG; hồ sơ quy ước cho isometric; kiểm thử tự động cho `IsoGraph`.

## 8. Kết quả I3 trên hai bản DWG ISOGEN, ngày 10/11/2026

Người sáng lập đưa thêm `3E204-1-250A-SUS-Sheet1.DWG` và `3E204-1-350A-SUS_02-Sheet1.DWG`. Hai bản này do **ISOGEN** sinh ra, định dạng rất phổ biến trong ngành vì PDMS, E3D, SmartPlant, CADWorx, Plant 3D đều xuất qua ISOGEN. Khác hẳn bản PDF mẫu:

| Thuộc tính | Bản PDF `335-PA-286` | Bản DWG ISOGEN |
|---|---|---|
| Nét ống | Nét dày 2,88 pt | Layer `PIPE`, lineweight 1,00 mm; nét đứt nối tiếp cũng trên `PIPE` nhưng 0,35 mm |
| Co | Hai nét chạm nhau | Hai nét không chạm, giữa là cung tròn; phải kéo tới giao điểm |
| Toạ độ | Neo E, N, EL tuyệt đối | Không có E, N; chỉ cao độ `FL`, `GL`, và `B.O.P.` là đáy ống |
| Kích thước | Chữ xoay theo trục, gán cho đoạn gần nhất rồi dùng neo để loại | Mỗi đường kích thước có đường gióng về đúng hai đỉnh của mạng, trên layer `DIMLINES`, chữ trên `DIMTEXT` |
| Bảng vật tư | `DESCRIPTION`, `SIZE`, `QTY`, cỡ inch | Layer `MATLIST`, `PT.NO`, `SIZE(MM)`, `Q'TY`, cỡ mm, thêm bảng cắt ống `PIECE NO.` |
| Ô số thứ tự | Ô bốn nét, mã mặt bích `F7 G10 B12` | Giống, thêm ô số đoạn cắt hai nét cần loại; đường chỉ gãy khúc, xuất phát từ mép tag |
| Nhánh | Nhánh 3/4" vẽ nét mỏng | Nhánh 25A vẽ nét dày, đứng, cụt, kết thúc bằng mặt bích, van cầu, bích mù |
| Đoạn chéo | Không có | Tờ 350A có đoạn chéo 45° nằm ngang với hộp kích thước lệch 538 |

Mã mới: `IsoDxfReader` đọc DWG trực tiếp bằng ACadSharp, đổi mm sang điểm để dùng chung ngưỡng với PDF; `DxfDrawingReader` đọc lineweight; `IsoOptions.ForIsogen()`; trong `IsoGraph`: nối góc co, gán kích thước theo hai đầu đường gióng (`MapToChain`, `ResolveSpans`), cao độ `FL` và đáy ống, gốc tổng hợp khi không có E, N, đoạn chéo 45°, một đoạn chạy cho mỗi chuỗi thẳng hàng, đoạn chạy thẳng đứng (`Run.EndElevation`, mới trong IR), cấu kiện xếp chồng ngoài đầu hở, chiều dài cấu kiện từ kích thước chưa gán; nút nhánh qua olet không đặt tê. Bản PDF mẫu chạy lại vẫn khép kín 3 mm và 4,2 mm.

| Tờ | Kết quả |
|---|---|
| `3E204-1-250A-SUS` | 1056, 3605, 574, 206, 84, 458 đều đúng; cao độ `FL +5605` tới `FL +2000` lệch 0,5 mm, sai số do B.O.P. 1864 cộng nửa đường kính; nhánh 25A dài 345; 8 cấu kiện: hai mặt bích 250A, van bướm, half coupling, mặt bích 25A, van cầu dài 127 theo kích thước trên hình, bích mù, côn thu 350x250 dài 330. Bản 3D 17 khối |
| `3E204-1-350A-SUS_02` | Đoạn chéo 761 ra đúng 538 Tây và 538 Bắc, khớp số 538 trên hộp lệch; 805, 3309, 1154, 1437, 118, 298 đúng; 5 cấu kiện: ba mặt bích, van bướm, bích mù. Bản 3D 13 khối |

Giới hạn còn lại: toạ độ mặt bằng là tương đối, gốc đặt tại đầu tuyến và trục Đông giả định 30°; hai tờ cùng nối vào `3E204-1-600A-SUS` nên ghép chung cần toạ độ của tuyến 600A, việc của I4. Chiều dài mặt bích và van bướm vẫn ước lượng theo DN khi hình không ghi. Vị trí mối hàn và giá đỡ giữa đoạn chia theo tỉ lệ trên giấy, chỉ gần đúng. Kích thước mang dấu `(*)` được dùng như số thường. Chưa có kiểm thử tự động cho `IsoGraph`.

## 9. Quy trình hai bước, ngày 11/11/2026

Người sáng lập yêu cầu: bước một lấy hết thông tin của mọi đối tượng cần vẽ ra khỏi bản vẽ, bước hai mới vẽ. Đã tách như vậy:

1. **Kiểm kê** (`IsoExtraction`): lệnh `iso <tệp> --out <ir.json>` ghi thêm `<tên>.kiem-ke.md` gồm tuyến, nối tiếp, đoạn chạy, cấu kiện, bảng vật tư, bảng cắt ống, mọi kích thước và chỗ dùng, cao độ. Bản vẽ isometric tự chứa các phép kiểm tra chéo nên kiểm kê tự đối chiếu: số lượng bảng vật tư với số ô trên hình và số nút trong đồ thị; mọi số trên hình phải có chỗ dùng; cao độ ghi với cao độ suy ra; tổng bảng cắt ống với chiều dài ống suy ra; chiều dài cấu kiện theo hình hay ước lượng. Mỗi dòng ghi `khớp`, `xem lại` hoặc `THIẾU`.
2. **Dựng**: IR chỉ được ghi khi không còn `THIẾU`; `--force` để ghi dù vậy. `PA_BUILDIR` và `PA_BUILD` không đổi.

Kết quả: hai tờ ISOGEN đủ để dựng, mỗi tờ 7 điểm `xem lại` (gốc toạ độ tương đối, chiều dài van bướm ước lượng, mối hàn giữa đoạn chia theo tỉ lệ, tổng cắt ống chênh 271 mm do phụ kiện ở điểm nối tiếp nằm trên tờ khác). Bản PDF mẫu bị chặn đúng: 12 `THIẾU`, toàn bộ là nhánh 3/4" vẽ nét mỏng và hai giá đỡ chưa đọc.

## 10. Chất lượng hình 3D và gộp hai tờ, ngày 11/11/2026

Người sáng lập xem bản 3D và nói chất lượng vẫn rất kém. Tự xuất STL từ bản DWG rồi vẽ lại để nhìn được cùng thứ: mặt bích là đĩa trơn, van là trụ có que, olet không có, côn thu ngược chiều, mặt bích và van lệch mối nối tới 50 mm. Ba việc đã làm:

1. **Danh mục kích thước cấu kiện** (`ComponentDims`, I2): mặt bích JIS 10K theo JIS B2220 (đường kính đĩa, vòng bu lông, số lỗ, chiều dày, cổ SO), mặt đối mặt van bướm wafer JIS B2032, van cầu và van cổng mặt bích JIS B2002, côn thu đồng tâm B16.9, khớp nối nửa 3000#. Bảng lấy theo trí nhớ, **chưa chuyên gia đối chiếu**, nên mọi chiều dài tra từ đó ghi nguồn `catalog_unverified` và bản kiểm kê vẫn ghi `xem lại`. Kích thước trên hình luôn thắng danh mục: 127 van cầu, 330 côn thu, 84 và 118 khe van bướm.
2. **Hình theo loại** (`AcadBuilder.Component`): mặt bích SO là đĩa có lỗ bu lông, mặt nâng và cổ lồng về phía ống; bích mù là đĩa; van bướm wafer là thân tròn nằm giữa hai mặt bích, cổ, hộp số, tay quay; van mặt bích có thân, hai bích đầu, cổ, trục, tay quay; côn thu là nón cụt từ đường kính ống này sang ống kia, đầu nào cùng cỡ ống đang nối thì đầu đó chạm ống; olet là ống lót trên mặt ống chính theo hướng nhánh. Cổ mặt bích và olet trùm lên ống nên không cắt ống.
3. **Đặt theo đỉnh** (`ISO-CMP-03`): trên bản ISOGEN, nét ống dừng tại ký hiệu mặt bích và kích thước đi tới mối nối, nên đỉnh tại khe ký hiệu chính là mặt bích. Mặt bích đặt mặt tại đỉnh, cổ về phía ống; van trong khe dài đúng bằng khe; olet đặt tại đỉnh rẽ nhánh của ống chính. Ngoài đầu hở: mặt bích SO đầu tiên lồng lên ống, van và bích mù xếp tiếp ra ngoài. Kết quả tờ 250A: mối nối tại 542 và 458, van 84 ở giữa, đúng số trên hình.

**Gộp hai tờ** (`IsoMerge`, lệnh `iso-merge`, I4): hai tờ không nối tiếp nhau mà cùng rẽ từ ống góp `3E204-1-600A-SUS`, tờ 250A tại `FL +5605` qua nhánh `600X250A`, tờ 350A tại `B.O.P. 7952` qua nhánh `600X350A`. Tờ nào cũng không ghi E, N và chưa có tờ 600A, nên khoảng cách giữa hai điểm rẽ là **giả định**: tờ 350A dời `(-3000, 266, 0)` để điểm rẽ của nó cách điểm rẽ của tờ 250A 3 m về Tây, ghi `@?` trong lệnh và ghi rõ trong `3E204-1-250A+350A.kiem-ke.md`. Vẽ thêm hai đoạn ống 600A tham chiếu dài 3 m qua hai điểm rẽ, mỗi đoạn theo hướng trên tờ (Đông Tây tại 250A, Bắc Nam tại 350A), độ tin cậy 0,3, không có trong bảng vật tư. Đồ thị gộp 15 nút, trong đó tê thu 600x250 và 600x350; quy tắc R-CON-04 sửa để tê thu không bị coi là lệch DN.

```
PipingAgent.Cli iso-merge a.ir.json b.ir.json@?-3000,266,0 --out gop.ir.json ^
  --stub "3E204-1-600A-SUS (tham chiếu):0,266,5605:E:600:1500" ^
  --stub "3E204-1-600A-SUS_01 (tham chiếu):-3000,266,8129.8:N:600:1500"
```

Kết quả: `3E204-1-250A+350A-3D.dwg` 35 khối, 15 ống, 5 co, 2 tê, 13 cấu kiện. Hai bản riêng dựng lại cùng cách. 261 kiểm thử xanh, thêm kiểm thử danh mục và tê thu.

Cách tự kiểm tra hình: `STLOUT` từng khối trong accoreconsole sau khi dời cả bản vẽ về góc dương, vì `STLOUT` hợp nhất các khối chạm nhau và thất bại khi cổ mặt bích đồng trục với ống, và dịch toạ độ âm; rồi vẽ lại bằng matplotlib.

Giới hạn: vị trí tương đối hai tờ cần tờ 600A hoặc người cho toạ độ; bảng JIS 10K chưa ký xác nhận; chưa có gioăng, bu lông, giá đỡ; tay quay van bướm chưa theo ghi chú `ORIENT HANDLE WEST`; chưa có kiểm thử tự động cho `IsoGraph`.

### 10.1 Lệnh PA_ISO trong AutoCAD

Đường chạy isometric gom vào `PipingAgent.Iso.IsoPipeline.Run(path, catalog, profile)`: đọc tờ, đồ thị giấy, toạ độ thật, đồ thị 3D, gắn cấu kiện, kiểm kê. CLI `iso` và plug-in dùng chung, chỉ khác cách in. Plug-in có lệnh `PA_ISO`: Enter lấy tờ đang mở hoặc dán đường dẫn DWG, DXF, PDF; ghi `<tên>.kiem-ke.md` cạnh tờ; chỉ nạp IR vào phiên khi kiểm kê không còn khoảng trống, còn thì hỏi `Dungvan/Thoi`. Sau đó `PA_BUILD` như cũ; nguồn isometric thì chế độ `Moi` không gắn tờ làm nền vì toạ độ tờ là giấy.

Plug-in cần `CopyLocalLockFileAssemblies` để ACadSharp và PdfPig nằm cạnh DLL, AutoCAD không tìm thư viện ở nơi khác. Đã thử bằng accoreconsole trên hai tờ 3E204: `PA_ISO`, Enter, `PA_BUILD`, `Moi` ra `<tên tờ>-3D.dwg`. Bản cho AutoCAD 2021 đến 2024 chỉ biên dịch, chưa thử.

Hiển thị: người dùng thấy ống là bốn nét nối nhau vì bản vẽ mở ở 2D Wireframe với ISOLINES 4. `PA_BUILD` giờ đặt ISOLINES 24, FACETRES 10, DISPSILH 1, góc nhìn SW Isometric, Shaded, thu phóng vừa mô hình; màu layer theo loại: mặt bích xanh lơ, van đỏ, phụ kiện nối vàng, ống theo tuyến. Bảng VPORT ghi vào cơ sở dữ liệu phụ không được AutoCAD giữ khi lưu, nên chế độ `Moi` xếp hàng lệnh `-VIEW SWISO`, `VSCURRENT Shaded`, `ZOOM E` cho bản vẽ vừa mở; chế độ `Hientai` đặt qua `Editor.SetCurrentView`, đã kiểm tra headless ra VIEWDIR (−1,−1,1).

### 10.2 Cổng nối và mối nối, S4-12

AutoCAD thuần không có khái niệm nối giữa hai khối như port trong S3D hay Plant 3D. Piping Agent tự tính trong `BuildPorts`: mỗi khối có cổng tại mặt nối (ống hai đầu; co hai tiếp điểm; tê hai đầu chạy và nhánh; van, côn thu hai đầu; bích SO một cổng mặt và một cổng ống cùng điểm vì lồng lên đầu ống; bích mù một cổng mặt; olet cổng nhánh tại tâm ống chính và cổng `host` hàn lên thân ống). Hai cổng trùng điểm trong 6 mm và ngược hướng là một mối nối; cổng ống của bích ghép với đầu ống trước để mặt bích còn lại cho van. Kết quả ghi vào xdata của khối (`ports=TÊN@x,y,z>mã:cổng|...`) và vẽ vòng tròn tại cổng trên layer `PA-<tuyến>-PORT`, xanh lá đã nối, đỏ hở. Lệnh `PA_PORT` chọn khối hoặc vòng để in. Trên hai tờ 3E204 mọi cổng đều khớp, chỉ hở đúng chỗ nối sang tờ khác: 250A hở tại đầu nối ống góp 600A và sau côn thu sang `C41E-1-350A-SUS`; 350A hở tại đầu nối ống góp. Bích mù trong `PlaceAtNodes` trước đây có Axis quay ra ngoài, trái quy ước cổ về mặt, đã sửa. 231 kiểm thử xanh.

### 10.3 Thẻ ribbon

`Ribbon/PipingRibbon.cs` tạo thẻ `Piping Agent` bằng AdWindows: ba panel, mỗi nút gửi `_.PA_xxx ` vào dòng lệnh qua `SendStringToExecute`, biểu tượng vẽ bằng WPF nên không cần tệp ảnh. Tạo khi ribbon sẵn sàng (`ComponentManager.ItemInitialized`) và tạo lại khi đổi `WSCURRENT`. Mọi chỗ chạm AdWindows nằm trong phương thức `NoInlining` sau kiểm tra `IsGuiAvailable`, để accoreconsole vẫn nạp được; đã thử headless. Phần hiện trên giao diện chưa tự kiểm tra được, người dùng xác nhận. Lệnh `PA_RIBBON` hiện lại thẻ.
