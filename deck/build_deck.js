// Builds the investor pitch deck. Numbers on the demo slide come from the prototype output (model.json).
const fs = require("fs");
const path = require("path");
// let the theme helper (outside this folder) find jszip in deck/node_modules
process.env.NODE_PATH = path.join(__dirname, "node_modules");
require("module").Module._initPaths();
const pptxgen = require("pptxgenjs");
const { applyTheme } = require(
  "C:/Users/pvthien/.claude/skills/synced/e27cb62f-6730-4187-abce-56f65458a9ea_662f7021-dcec-4b42-be80-6981805c1adc/pptx/scripts/apply_theme.js"
);

const OUT_DIR = path.join(__dirname, "..", "output");
const OUT = path.join(OUT_DIR, "pitch_deck.pptx");
const model = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "prototype", "data", "model.json"), "utf8"));
const st = model.stats;
const vn = (x, nd) => x.toFixed(nd).replace(".", ",");
const convertSec = (model.timing_ms.read_dxf + model.timing_ms.convert) / 1000;

const THEME = {
  name: "Piping Agent",
  headFontFace: "Arial",
  bodyFontFace: "Arial",
  colors: {
    dk1: "0B1522", lt1: "F4F7FB", dk2: "16273F", lt2: "93A4BA",
    accent1: "F26B21", accent2: "35D6E8", accent3: "7EE081", accent4: "F4D35E",
    accent5: "2A3F5C", accent6: "C9D5E3", hlink: "35D6E8", folHlink: "93A4BA",
  },
};

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.33 x 7.5 in
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = "AI Agent cho thiết kế đường ống";
pres.subject = "Tài liệu giới thiệu cho nhà đầu tư";
const C = pres.SchemeColor;
const BG = C.text1, INK = C.background1, CARD = C.text2, MUTED = C.background2;
const ORANGE = C.accent1, CYAN = C.accent2, BORDER = C.accent5, SOFT = C.accent6;
const DECK_NAME = "AI Agent cho thiết kế đường ống";

// ---- layouts ---------------------------------------------------------------
pres.defineSlideMaster({
  title: "COVER",
  background: { path: path.join(OUT_DIR, "hero_title.png") },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: 0.6, y: 2.25, w: 5.9, h: 1.9, fontSize: 40, bold: true,
        color: INK, align: "left", valign: "bottom", margin: 0 }, text: "" } },
    { placeholder: { options: { name: "body", type: "body", x: 0.6, y: 4.3, w: 5.6, h: 1.1, fontSize: 20,
        color: SOFT, align: "left", valign: "top", margin: 0 }, text: "" } },
  ],
});
pres.defineSlideMaster({
  title: "CONTENT",
  background: { color: BG },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: 0.6, y: 0.45, w: 12.13, h: 0.9, fontSize: 36, bold: true,
        color: INK, align: "left", valign: "middle", margin: 0 }, text: "" } },
    { text: { text: DECK_NAME, options: { x: 0.6, y: 7.0, w: 8, h: 0.3, fontSize: 10, color: MUTED, margin: 0 } } },
  ],
  slideNumber: { x: 12.13, y: 7.0, w: 0.6, h: 0.3, fontSize: 10, color: MUTED, align: "right" },
});

// ---- helpers ---------------------------------------------------------------
let uid = 0;
const nm = (s) => `${s} ${++uid}`;
function text(slide, s, o) {
  slide.addText(s, { isTextBox: true, margin: 0, color: INK, fontSize: 14, valign: "top", objectName: nm("Text"), ...o });
}
function card(slide, x, y, w, h, o = {}) {
  slide.addShape(pres.ShapeType.roundRect, { x, y, w, h, rectRadius: 0.1, fill: { color: CARD },
    line: { color: BORDER, width: 1 }, objectName: nm("Card"), ...o });
}
function badge(slide, label, x, y, d = 0.62, o = {}) {
  slide.addText(label, { isTextBox: true, shape: pres.ShapeType.ellipse, x, y, w: d, h: d, fill: { color: ORANGE },
    color: INK, bold: true, fontSize: Math.round(d * 30), align: "center", valign: "middle", margin: 0,
    objectName: nm("Badge"), ...o });
}
function arrow(slide, x, y, w, h, o = {}) {
  slide.addShape(pres.ShapeType.line, { x, y, w, h, line: { color: ORANGE, width: 2, endArrowType: "triangle" },
    objectName: nm("Arrow"), ...o });
}
function content(section, title, notes) {
  const s = pres.addSlide({ masterName: "CONTENT", sectionTitle: section });
  s.addText(title, { placeholder: "title" });
  s.addNotes(notes);
  return s;
}

// ---- 1. cover ----------------------------------------------------------------
pres.addSection({ title: "Mở đầu" });
{
  const s = pres.addSlide({ masterName: "COVER", sectionTitle: "Mở đầu" });
  s.addText("TÀI LIỆU GIỚI THIỆU CHO NHÀ ĐẦU TƯ", { isTextBox: true, shape: pres.ShapeType.roundRect, rectRadius: 0.06,
    x: 0.6, y: 1.95, w: 4.3, h: 0.4, fill: { color: ORANGE }, color: INK, bold: true, fontSize: 12, align: "center",
    valign: "middle", margin: 0, objectName: "Nhãn tài liệu" });
  s.addText("AI Agent cho thiết kế đường ống", { placeholder: "title" });
  s.addText("Từ bản vẽ 2D đến mô hình 3D, tự động trong AutoCAD và Smart 3D", { placeholder: "body" });
  text(s, "[Họ và tên người trình bày]  ·  Tháng 11/2026", { x: 0.6, y: 6.55, w: 6, h: 0.35, fontSize: 14, color: MUTED });
  s.addNotes("Mở đầu bằng một câu: chúng tôi dùng AI agent để dựng mô hình đường ống 3D từ bản vẽ 2D, ngay trong phần mềm kỹ sư đang dùng. Hình bên phải là kết quả thật của prototype. Cần điền tên người trình bày.");
}

// ---- 2. problem --------------------------------------------------------------
pres.addSection({ title: "Vấn đề và giải pháp" });
{
  const s = content("Vấn đề và giải pháp", "Dựng 3D từ bản vẽ 2D vẫn làm bằng tay",
    "Nêu ba nỗi đau. Nên bổ sung số liệu thật: số giờ họa viên cho một dự án, tỉ lệ sai sót phải làm lại, lấy từ khách hàng đã từng làm việc. Hiện slide chỉ nêu định tính vì chưa có số liệu kiểm chứng.");
  const items = [
    ["Tốn giờ công", "Họa viên đọc từng nhãn cao độ và đường kính trên bản vẽ, rồi dựng lại từng đoạn ống trong phần mềm 3D."],
    ["Dễ sai sót", "Hàng trăm thông số được nhập lại bằng tay. Sai một cao độ có thể thành va chạm ống ngoài công trường."],
    ["Dữ liệu cũ kẹt ở 2D", "Nhiều nhà máy đang vận hành chỉ có bản vẽ 2D. Muốn cải tạo hay số hóa đều phải dựng lại 3D từ đầu."],
  ];
  items.forEach(([h, b], i) => {
    const x = 0.6 + i * 4.15;
    card(s, x, 1.75, 3.85, 3.75);
    badge(s, String(i + 1), x + 0.35, 2.05);
    text(s, h, { x: x + 0.35, y: 2.95, w: 3.15, h: 0.45, fontSize: 22, bold: true });
    text(s, b, { x: x + 0.35, y: 3.5, w: 3.15, h: 1.85, fontSize: 16, color: SOFT });
  });
  text(s, "Tiến độ thiết kế đang phụ thuộc vào số lượng họa viên, không phải vào độ khó kỹ thuật.",
    { x: 0.6, y: 5.95, w: 12.13, h: 0.5, fontSize: 18, italic: true, color: CYAN });
}

// ---- 3. solution -------------------------------------------------------------
{
  const s = content("Vấn đề và giải pháp", "Agent đọc bản vẽ 2D và dựng mô hình 3D",
    "Giải thích 4 bước. Nhấn mạnh agent chạy bên trong AutoCAD và Smart 3D thông qua plug-in, không thay thế phần mềm thiết kế. Kỹ sư vẫn là người duyệt cuối.");
  const steps = [
    ["Nạp bản vẽ", "Bản vẽ mặt bằng đường ống dạng DWG hoặc DXF."],
    ["Nhận diện", "Tuyến ống, nhãn đường kính và cao độ, van, thiết bị, kết cấu."],
    ["Dựng 3D", "Agent gọi lệnh có sẵn của AutoCAD và Smart 3D qua API để dựng hình."],
    ["Kiểm tra", "Bộ luật kiểm tra tự động. Kỹ sư duyệt trước khi lưu."],
  ];
  steps.forEach(([h, b], i) => {
    const x = 0.6 + i * 3.13;
    card(s, x, 1.8, 2.75, 3.3);
    badge(s, String(i + 1), x + 0.3, 2.1);
    text(s, h, { x: x + 0.3, y: 3.0, w: 2.2, h: 0.45, fontSize: 20, bold: true });
    text(s, b, { x: x + 0.3, y: 3.55, w: 2.2, h: 1.4, fontSize: 14, color: SOFT });
    if (i < 3) arrow(s, x + 2.79, 3.45, 0.3, 0);
  });
  card(s, 0.6, 5.5, 12.13, 0.95, { fill: { color: BG }, line: { color: ORANGE, width: 1.5 } });
  text(s, "Kỹ sư vẫn là người ký duyệt. Agent làm phần việc lặp lại.",
    { x: 0.95, y: 5.5, w: 11.4, h: 0.95, fontSize: 20, bold: true, valign: "middle" });
}

// ---- 4. demo -----------------------------------------------------------------
pres.addSection({ title: "Demo" });
{
  const s = content("Demo", "Prototype đã chạy trên bản vẽ mẫu",
    "Bấm vào video để phát (56 giây). Video được dựng từ dữ liệu do prototype tạo ra: file DXF 2D đi vào, bộ chuyển đổi đọc và dựng mô hình, xuất DXF 3D. Nói rõ giới hạn: đây là bản vẽ mẫu tự tạo, nhận diện bằng quy tắc cố định, chưa dùng AI agent và chưa chạy bên trong AutoCAD. Các con số bên phải lấy từ kết quả chạy thật.");
  const poster = fs.readFileSync(path.join(OUT_DIR, "video_poster.jpg")).toString("base64");
  s.addMedia({ type: "video", path: path.join(OUT_DIR, "demo_2d_to_3d.mp4"), cover: "image/jpeg;base64," + poster,
    x: 0.6, y: 1.65, w: 7.7, h: 4.33, objectName: "Video demo" });
  text(s, "Video dựng từ dữ liệu do prototype tạo ra. Bản vẽ mẫu, quy tắc nhận diện cố định, chưa chạy trong AutoCAD.",
    { x: 0.6, y: 6.12, w: 7.7, h: 0.5, fontSize: 12, color: MUTED });
  const stats = [
    [String(st.entities_2d), "đối tượng 2D trong bản vẽ đầu vào"],
    [String(st.entities_3d), "khối 3D được dựng, chia layer theo tuyến ống"],
    [vn(convertSec, 2) + " giây", "để đọc bản vẽ và dựng mô hình"],
    [vn(st.pipe_length_m, 1) + " m", `ống, ${st.elbows} co, ${st.tees} tê, ${st.valves} van`],
  ];
  stats.forEach(([big, label], i) => {
    const y = 1.6 + i * 1.2;
    text(s, big, { x: 8.75, y, w: 3.98, h: 0.62, fontSize: 38, bold: true, color: ORANGE, valign: "middle" });
    text(s, label, { x: 8.75, y: y + 0.64, w: 3.98, h: 0.4, fontSize: 14, color: SOFT });
  });
}

// ---- 5. before / after -------------------------------------------------------
{
  const s = content("Demo", "Từ nét vẽ phẳng đến mô hình có cao độ",
    "Bên trái là bản vẽ đầu vào, chỉ có nét 2D và chữ. Cao độ nằm trong nhãn, ví dụ EL+4100. Bên phải là mô hình prototype dựng ra: mỗi tuyến ống ở đúng cao độ và đường kính, có co, tê, van, ống đứng nối hai cao độ.");
  s.addImage({ path: path.join(OUT_DIR, "still_2d_drawing.png"), x: 0.6, y: 1.65, w: 5.55, h: 4.32,
    altText: "Bản vẽ mặt bằng đường ống 2D", objectName: "Bản vẽ 2D" });
  arrow(s, 6.22, 3.8, 0.4, 0);
  s.addImage({ path: path.join(OUT_DIR, "hero_model.png"), x: 6.7, y: 1.65, w: 6.03, h: 4.27,
    altText: "Mô hình đường ống 3D do prototype dựng", objectName: "Mô hình 3D" });
  text(s, [{ text: "Đầu vào  ", options: { bold: true, color: ORANGE } }, { text: "Bản vẽ mặt bằng 2D, cao độ chỉ có trong nhãn chữ", options: { color: SOFT } }],
    { x: 0.6, y: 6.15, w: 5.55, h: 0.6, fontSize: 14 });
  text(s, [{ text: "Đầu ra  ", options: { bold: true, color: ORANGE } }, { text: "Mô hình 3D đúng cao độ và đường kính, xuất file DXF", options: { color: SOFT } }],
    { x: 6.7, y: 6.15, w: 6.03, h: 0.6, fontSize: 14 });
}

// ---- 6. architecture ---------------------------------------------------------
{
  const s = content("Demo", "Vì sao kết quả tin cậy được",
    "Điểm khác biệt về kỹ thuật. Các nghiên cứu năm 2026 cho thấy mô hình ngôn ngữ còn yếu về hình học và kích thước chính xác. Vì vậy agent không tự sinh tọa độ. Agent chọn lệnh, lệnh tham số tính hình, bộ luật kiểm tra lại, kỹ sư duyệt. Các lệnh tham số chính là phần plug-in mà đội ngũ đã có kinh nghiệm làm.");
  const rows = [
    ["Agent không tự vẽ tọa độ", "Agent chỉ quyết định gọi lệnh nào. Kích thước và vị trí do lệnh tham số tính ra."],
    ["Kiểm tra bằng luật sau mỗi bước", "Ống hở đầu, nhãn thiếu, nhãn gắn nhầm tuyến được phát hiện tự động."],
    ["Kỹ sư duyệt cuối cùng", "Chỗ agent không chắc được đánh dấu để kỹ sư xem trước khi lưu."],
  ];
  rows.forEach(([h, b], i) => {
    const y = 1.75 + i * 1.62;
    badge(s, String(i + 1), 0.6, y);
    text(s, h, { x: 1.45, y: y - 0.02, w: 4.5, h: 0.42, fontSize: 19, bold: true });
    text(s, b, { x: 1.45, y: y + 0.45, w: 4.5, h: 0.95, fontSize: 14, color: SOFT });
  });
  const boxes = ["Bản vẽ 2D và yêu cầu", "Agent chọn lệnh cần gọi", "Lệnh tham số trong plug-in",
    "AutoCAD / Smart 3D dựng hình", "Bộ kiểm tra bằng luật", "Kỹ sư duyệt và lưu"];
  boxes.forEach((b, i) => {
    const y = 1.65 + i * 0.88;
    const hot = i === 1;
    s.addText(b, { isTextBox: true, shape: pres.ShapeType.roundRect, rectRadius: 0.08, x: 6.75, y, w: 3.9, h: 0.6,
      fill: { color: hot ? ORANGE : CARD }, line: { color: hot ? ORANGE : BORDER, width: 1 }, color: INK, bold: hot,
      fontSize: 15, align: "center", valign: "middle", margin: 0, objectName: nm("Khối sơ đồ") });
    if (i < boxes.length - 1) arrow(s, 8.7, y + 0.62, 0, 0.24, { line: { color: MUTED, width: 1.5, endArrowType: "triangle" } });
  });
  // feedback loop: checker (box 5) back to agent (box 2)
  const yCheck = 1.65 + 4 * 0.88 + 0.3, yAgent = 1.65 + 1 * 0.88 + 0.3;
  const loop = { color: CYAN, width: 1.5 };
  s.addShape(pres.ShapeType.line, { x: 10.65, y: yCheck, w: 0.55, h: 0, line: loop, objectName: nm("Vòng lặp") });
  s.addShape(pres.ShapeType.line, { x: 11.2, y: yAgent, w: 0, h: yCheck - yAgent, line: loop, objectName: nm("Vòng lặp") });
  s.addShape(pres.ShapeType.line, { x: 10.69, y: yAgent, w: 0.51, h: 0, flipH: true,
    line: { ...loop, endArrowType: "triangle" }, objectName: nm("Vòng lặp") });
  text(s, "Sai thì sửa và thử lại", { x: 11.35, y: 3.3, w: 1.38, h: 0.9, fontSize: 13, color: CYAN });
}

// ---- 7. why now --------------------------------------------------------------
pres.addSection({ title: "Thị trường" });
{
  const s = content("Thị trường", "Thời điểm: mảng nhà máy còn bỏ ngỏ",
    "Bốn tín hiệu thị trường, đều có nguồn. MCP là Model Context Protocol, chuẩn để AI agent gọi công cụ. Lưu ý trung thực: nhận định về Smart 3D dựa trên một lượt tìm kiếm tháng 11/2026, nên kiểm tra lại thông báo mới nhất của Hexagon trước khi trình bày.");
  const cards = [
    ["Autodesk mở đường cho agent", "Autodesk Assistant dùng chuẩn kết nối MCP cho agent. Fusion đã có bộ kết nối chính thức, AutoCAD thì chưa."],
    ["Nhu cầu đã được chứng minh", "Siemens Solid Edge 2026 dùng AI tạo tới 80% hình chiếu 2D. Nhưng đó là cơ khí, không phải nhà máy."],
    ["Smart 3D chưa có trợ lý AI", "Chưa thấy công bố nào của Hexagon về AI cho Smart 3D. Bản v14 chỉ mở rộng API và sinh bản vẽ theo lô."],
    ["Nhà đầu tư toàn cầu gọi tên", "Y Combinator mùa hè 2026 nêu phần mềm công nghiệp và thiết kế là mảng AI có thể làm lại với chi phí thấp hơn nhiều."],
  ];
  cards.forEach(([h, b], i) => {
    const x = 0.6 + (i % 2) * 6.21, y = 1.65 + Math.floor(i / 2) * 2.5;
    card(s, x, y, 5.92, 2.25);
    text(s, h, { x: x + 0.35, y: y + 0.3, w: 5.2, h: 0.45, fontSize: 20, bold: true, color: CYAN });
    text(s, b, { x: x + 0.35, y: y + 0.85, w: 5.2, h: 1.25, fontSize: 15, color: SOFT });
  });
  text(s, "Nguồn: Autodesk, Engineering.com (4/2026); Siemens; Hexagon; Y Combinator Requests for Startups (2026). Tra cứu tháng 11/2026.",
    { x: 0.6, y: 6.6, w: 12.13, h: 0.3, fontSize: 10, color: MUTED });
}

// ---- 8. competition ----------------------------------------------------------
{
  const s = content("Thị trường", "Khoảng trống: dựng lại 3D từ bản vẽ đã thiết kế",
    "Chưa tìm thấy công ty nào được gọi vốn chuyên dựng mô hình 3D cho nhà máy từ bản vẽ 2D. Các hãng lớn làm trợ lý chung hoặc làm chiều ngược lại, từ 3D ra 2D. Nhóm startup hiện có chủ yếu đọc bản vẽ để bóc khối lượng. Nhóm tự động định tuyến như PlantStream và Aspen OptiPlant vạch tuyến ống mới từ bố trí thiết bị ở giai đoạn thiết kế sơ bộ. Chúng tôi không vạch tuyến mới. Chúng tôi dựng lại đúng tuyến mà kỹ sư đã thiết kế trên bản vẽ 2D, quyền quyết định tuyến vẫn thuộc về người thiết kế.");
  const hdr = { bold: true, color: INK, fill: { color: BORDER }, fontSize: 14 };
  const cell = { color: SOFT, fontSize: 14, fill: { color: CARD } };
  const us = { color: INK, bold: true, fontSize: 14, fill: { color: ORANGE } };
  const row = (a, o) => a.map((t) => ({ text: t, options: o }));
  const rows = [
    row(["Nhóm", "Ví dụ", "Họ làm gì", "Khoảng trống"], hdr),
    row(["Hãng phần mềm CAD", "Autodesk Assistant", "Trợ lý chung trong sản phẩm", "Chưa chuyên cho đường ống nhà máy"], cell),
    row(["Thiết kế nhà máy", "Hexagon Smart 3D", "Sinh bản vẽ theo lô từ mô hình có sẵn", "Chưa có AI dựng mô hình từ 2D"], cell),
    row(["CAD cơ khí", "Siemens Solid Edge", "AI tạo hình chiếu 2D từ mô hình 3D", "Chiều ngược lại, khác ngành"], cell),
    row(["Tự động định tuyến", "PlantStream, Aspen OptiPlant", "Vạch tuyến ống mới từ bố trí thiết bị", "Không dựng lại tuyến đã thiết kế"], cell),
    row(["Công cụ đọc bản vẽ", "theTakeoff.ai, Acuvate", "Bóc khối lượng, số hóa sơ đồ", "Không dựng mô hình 3D"], cell),
    row(["Chúng tôi", "Plug-in có agent", "Dựng lại mô hình 3D từ bản vẽ 2D đã thiết kế", "Ngay trong AutoCAD và Smart 3D"], us),
  ];
  s.addTable(rows, { x: 0.6, y: 1.7, w: 12.13, colW: [2.55, 2.75, 3.7, 3.13], rowH: 0.65, valign: "middle",
    margin: [0, 0.15, 0, 0.15], border: { type: "solid", pt: 1, color: THEME.colors.dk1 }, objectName: "Bảng cạnh tranh" });
  text(s, "Nguồn: trang sản phẩm và thông cáo của các hãng, tra cứu tháng 11/2026.",
    { x: 0.6, y: 6.6, w: 12.13, h: 0.3, fontSize: 10, color: MUTED });
}

// ---- 9. risks ----------------------------------------------------------------
{
  const s = content("Thị trường", "Rủi ro chính và cách giảm",
    "Chủ động nêu rủi ro để tạo niềm tin. Ba rủi ro lớn nhất: giới hạn của AI với hình học, hãng lớn tự làm, và yêu cầu bảo mật dữ liệu dự án.");
  const rows = [
    ["AI còn yếu về hình học", "Mô hình ngôn ngữ hay sai kích thước và quan hệ không gian.",
      "Agent chỉ gọi lệnh tham số. Bộ luật kiểm tra lại. Kỹ sư duyệt cuối."],
    ["Hãng lớn tự làm tính năng này", "Autodesk và Hexagon có thể đưa AI vào sản phẩm của họ.",
      "Đi sâu một ngành hẹp. Bán dịch vụ có kết quả đo được trước khi đóng gói phần mềm."],
    ["Dữ liệu dự án cần bảo mật", "Tổng thầu thường không cho đưa bản vẽ ra ngoài mạng nội bộ.",
      "Thiết kế để chạy trong mạng của khách hàng ngay từ đầu."],
  ];
  rows.forEach(([h, risk, fix], i) => {
    const y = 1.7 + i * 1.62;
    card(s, 0.6, y, 12.13, 1.42);
    badge(s, String(i + 1), 0.9, y + 0.4);
    text(s, h, { x: 1.8, y: y + 0.22, w: 4.6, h: 0.42, fontSize: 19, bold: true });
    text(s, risk, { x: 1.8, y: y + 0.68, w: 4.6, h: 0.62, fontSize: 14, color: SOFT });
    arrow(s, 6.6, y + 0.71, 0.45, 0);
    text(s, "CÁCH GIẢM", { x: 7.3, y: y + 0.24, w: 5.1, h: 0.3, fontSize: 12, bold: true, color: CYAN });
    text(s, fix, { x: 7.3, y: y + 0.58, w: 5.1, h: 0.75, fontSize: 14 });
  });
}

// ---- 10. customers and business model ----------------------------------------
{
  const s = content("Thị trường", "Khách hàng và cách thu tiền",
    "Ba nhóm khách hàng, bắt đầu từ nhóm dễ tiếp cận nhất là công ty gia công bản vẽ. Tổng thầu ở đây là tổng thầu thiết kế, mua sắm, xây dựng, thường gọi là EPC. Nên bổ sung: quy mô thị trường, mức giá dự kiến, và tên khách hàng đã từng làm plug-in cho họ nếu được phép nêu.");
  text(s, "Ai trả tiền", { x: 0.6, y: 1.65, w: 5.8, h: 0.4, fontSize: 20, bold: true, color: CYAN });
  const who = [
    ["Công ty gia công bản vẽ", "Làm thuê cho khách Nhật, Úc, Mỹ. Doanh thu theo sản lượng nên cần làm nhanh hơn."],
    ["Phòng thiết kế của tổng thầu", "Dự án dầu khí, hóa chất, nhà máy điện. Khối lượng mô hình lớn, tiến độ gấp."],
    ["Chủ nhà máy cần số hóa", "Có bản vẽ 2D cũ, cần mô hình 3D để cải tạo và vận hành."],
  ];
  who.forEach(([h, b], i) => {
    const y = 2.3 + i * 1.45;
    badge(s, "ABC"[i], 0.6, y);
    text(s, h, { x: 1.45, y: y - 0.03, w: 4.75, h: 0.42, fontSize: 18, bold: true });
    text(s, b, { x: 1.45, y: y + 0.42, w: 4.75, h: 0.8, fontSize: 14, color: SOFT });
  });
  text(s, "Thu tiền theo ba giai đoạn", { x: 6.9, y: 1.65, w: 5.83, h: 0.4, fontSize: 20, bold: true, color: CYAN });
  const stages = [
    ["Giai đoạn 1: Dịch vụ", "Nhận bản vẽ 2D, giao mô hình 3D. Tính tiền theo bản vẽ."],
    ["Giai đoạn 2: Plug-in thuê bao", "Khách tự chạy trong AutoCAD. Tính tiền theo người dùng."],
    ["Giai đoạn 3: Doanh nghiệp lớn", "Triển khai trong mạng nội bộ, tích hợp Smart 3D."],
  ];
  stages.forEach(([h, b], i) => {
    const y = 2.25 + i * 1.45;
    card(s, 6.9, y, 5.83, 1.25);
    text(s, h, { x: 7.2, y: y + 0.18, w: 5.25, h: 0.4, fontSize: 18, bold: true });
    text(s, b, { x: 7.2, y: y + 0.62, w: 5.25, h: 0.5, fontSize: 14, color: SOFT });
  });
}

// ---- 11. roadmap -------------------------------------------------------------
pres.addSection({ title: "Kế hoạch" });
{
  const s = content("Kế hoạch", "Lộ trình 12 tháng",
    "Lộ trình đề xuất, cần điều chỉnh theo nguồn lực thực tế. Mốc quan trọng nhất là thử nghiệm với một khách hàng trên bản vẽ thật trong 3 tháng đầu. Mục tiêu 3 đến 5 khách hàng trả tiền ở cuối năm là mục tiêu đề xuất, chưa phải cam kết.");
  const ms = [
    ["Hiện tại", "Prototype", "Đọc bản vẽ mẫu, dựng 3D, xuất DXF. Nhận diện bằng quy tắc cố định."],
    ["Tháng 1 đến 3", "Plug-in AutoCAD", "Chạy trong AutoCAD. Thử nghiệm với một khách hàng trên bản vẽ thật."],
    ["Tháng 4 đến 8", "Agent và Smart 3D", "Agent xử lý bản vẽ không theo chuẩn. Tích hợp Smart 3D."],
    ["Tháng 9 đến 12", "Thương mại hóa", "Bản thương mại. Mục tiêu 3 đến 5 khách hàng trả tiền."],
  ];
  s.addShape(pres.ShapeType.line, { x: 2.05, y: 2.55, w: 9.24, h: 0, line: { color: BORDER, width: 3 }, objectName: "Trục thời gian" });
  ms.forEach(([when, h, b], i) => {
    const x = 0.6 + i * 3.08;
    text(s, when, { x, y: 1.7, w: 2.9, h: 0.4, fontSize: 16, bold: true, color: i === 0 ? ORANGE : CYAN, align: "center" });
    s.addShape(pres.ShapeType.ellipse, { x: x + 1.27, y: 2.37, w: 0.36, h: 0.36, fill: { color: i === 0 ? ORANGE : BG },
      line: { color: i === 0 ? ORANGE : CYAN, width: 2.5 }, objectName: nm("Mốc") });
    card(s, x, 3.1, 2.9, 2.6);
    text(s, h, { x: x + 0.28, y: 3.4, w: 2.34, h: 0.75, fontSize: 19, bold: true });
    text(s, b, { x: x + 0.28, y: 4.2, w: 2.34, h: 1.35, fontSize: 14, color: SOFT });
  });
  text(s, "Mốc quyết định: một khách hàng thử nghiệm trên bản vẽ thật trong 3 tháng đầu.",
    { x: 0.6, y: 6.1, w: 12.13, h: 0.5, fontSize: 18, italic: true, color: CYAN });
}

// ---- 12. team and ask ----------------------------------------------------------
{
  const s = content("Kế hoạch", "Đội ngũ và đề xuất đầu tư",
    "Các ô trong ngoặc vuông cần điền trước khi trình bày: họ tên, số năm kinh nghiệm, dự án và khách hàng tiêu biểu, thành viên khác, số vốn cần gọi và tỉ lệ phân bổ. Điểm mạnh cần nhấn: đã trực tiếp làm plug-in cho AutoCAD và Smart 3D, là kỹ năng hiếm.");
  card(s, 0.6, 1.7, 6.0, 4.45);
  text(s, "NGƯỜI SÁNG LẬP", { x: 0.95, y: 1.98, w: 5.3, h: 0.3, fontSize: 12, bold: true, color: CYAN });
  text(s, "[Họ và tên]", { x: 0.95, y: 2.4, w: 5.3, h: 0.6, fontSize: 28, bold: true });
  text(s, [
    { text: "Lập trình viên, đã phát triển plug-in cho AutoCAD và Smart 3D", options: { bullet: true, breakLine: true } },
    { text: "[Số năm kinh nghiệm, dự án và khách hàng tiêu biểu]", options: { bullet: true, breakLine: true } },
    { text: "[Thành viên khác hoặc vị trí cần tuyển]", options: { bullet: true } },
  ], { x: 0.95, y: 3.3, w: 5.3, h: 2.6, fontSize: 17, color: SOFT, paraSpaceAfter: 14 });
  card(s, 6.9, 1.7, 5.83, 4.45);
  text(s, "ĐỀ XUẤT ĐẦU TƯ", { x: 7.25, y: 1.98, w: 5.1, h: 0.3, fontSize: 12, bold: true, color: CYAN });
  text(s, "[Số vốn cần gọi]", { x: 7.25, y: 2.4, w: 5.1, h: 0.7, fontSize: 36, bold: true, color: ORANGE });
  text(s, "Sử dụng trong 12 tháng đầu", { x: 7.25, y: 3.35, w: 5.1, h: 0.4, fontSize: 18, bold: true });
  text(s, [
    { text: "Phát triển plug-in và agent", options: { bullet: true, breakLine: true } },
    { text: "License phần mềm và hạ tầng tính toán", options: { bullet: true, breakLine: true } },
    { text: "Thử nghiệm với khách hàng đầu tiên", options: { bullet: true } },
  ], { x: 7.25, y: 3.95, w: 5.1, h: 1.95, fontSize: 17, color: SOFT, paraSpaceAfter: 14 });
}

// ---- 13. closing ---------------------------------------------------------------
{
  const s = pres.addSlide({ masterName: "COVER", sectionTitle: "Kế hoạch" });
  s.addText("Cảm ơn", { placeholder: "title" });
  s.addText("Mời xem demo trực tiếp và trao đổi", { placeholder: "body" });
  text(s, "[Họ và tên]  ·  [Email]  ·  [Số điện thoại]", { x: 0.6, y: 6.55, w: 6, h: 0.35, fontSize: 14, color: MUTED });
  s.addNotes("Kết thúc bằng lời mời xem demo trực tiếp. Cần điền thông tin liên hệ.");
}

(async () => {
  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);
  console.log("wrote", OUT);
})();
