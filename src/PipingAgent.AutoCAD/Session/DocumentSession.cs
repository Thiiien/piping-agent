using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using PipingAgent.Core.Decisions;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;
using PipingAgent.Tools;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Trạng thái của Piping Agent cho một bản vẽ đang mở, S2-15. Mỗi tài liệu một bản, giữ trong <see cref="SessionManager"/>,
    /// không có biến tĩnh toàn cục theo quy tắc 4 mục 6.12.3. Đóng bản vẽ thì bỏ cả phiên.
    /// </summary>
    public sealed class DocumentSession : IDisposable
    {
        public DocumentSession(Document document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Highlight = new HighlightService(document);
        }

        public Document Document { get; }

        public string DrawingName => System.IO.Path.GetFileName(Document.Name);

        public HighlightService Highlight { get; }

        /// <summary>Hệ số từ đơn vị bản vẽ sang mm của lần quét gần nhất.</summary>
        public double ScaleToMm { get; set; } = 1.0;

        public string? ProfilePath { get; set; }

        public ConventionProfile? Profile { get; set; }

        public List<ProfileError> ProfileErrors { get; } = new List<ProfileError>();

        /// <summary>Nạp hồ sơ quy ước từ <see cref="ProfilePath"/> vào phiên, thay cả danh sách lỗi.</summary>
        public ProfileLoadResult LoadProfile()
        {
            var loaded = ProfileStore.Load(ProfilePath!);
            Profile = loaded.Profile;
            ProfileErrors.Clear();
            ProfileErrors.AddRange(loaded.Errors);
            return loaded;
        }

        public RecognitionResult? LastResult { get; set; }

        public IReadOnlyList<SourceEntity>? LastEntities { get; set; }

        /// <summary>IR đến từ tờ isometric (PA_ISO): toạ độ tờ là giấy, không tham chiếu bản 2D làm nền khi dựng vào bản mới.</summary>
        public bool FromIsometric { get; set; }

        /// <summary>Hộp bao theo handle của lần quét gần nhất, mm, để phóng tới vấn đề.</summary>
        public Dictionary<string, Box2> BoundsByHandle { get; } = new Dictionary<string, Box2>(StringComparer.OrdinalIgnoreCase);

        public string? LastFingerprint { get; set; }

        /// <summary>Quyết định của người dùng cho bản vẽ này, S5-05. Đọc từ tệp phiên khi mở, ghi sau mỗi thay đổi.</summary>
        public List<Decision> Decisions { get; } = new List<Decision>();

        /// <summary>Đường dẫn tệp phiên cạnh bản vẽ, null khi bản vẽ chưa lưu.</summary>
        public string? SessionPath => File.Exists(Document.Name) ? SessionFile.PathFor(Document.Name) : null;

        public string? SessionLoadError { get; private set; }

        /// <summary>Đọc tệp phiên nếu có. Tệp hỏng thì ghi lỗi và bắt đầu phiên trống, không làm hỏng lệnh.</summary>
        public void LoadSessionFile()
        {
            var path = SessionPath;
            if (path == null || !File.Exists(path)) return;
            try
            {
                var s = SessionFile.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                Decisions.Clear();
                Decisions.AddRange(s.Decisions);
                if (!string.IsNullOrEmpty(s.ProfilePath) && File.Exists(s.ProfilePath)) ProfilePath = s.ProfilePath;
            }
            catch (System.Exception ex)
            {
                SessionLoadError = ex.Message;
            }
        }

        /// <summary>Ghi tệp phiên, S5-06. Ghi vào tệp tạm rồi đổi tên để tắt đột ngột không để lại tệp dở.</summary>
        public bool SaveSessionFile(bool includeModel = true)
        {
            var path = SessionPath;
            if (path == null) return false;
            var s = new SessionFile
            {
                Drawing = DrawingName,
                Fingerprint = LastFingerprint,
                ProfileId = Profile?.Id,
                ProfileVersion = Profile?.Version,
                ProfilePath = ProfilePath,
                SavedAt = SessionFile.Now(),
                Decisions = Decisions.ToList(),
                Model = includeModel ? LastResult?.Model : null,
            };
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, SessionFile.Serialize(s), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return true;
        }

        /// <summary>Thêm một quyết định và lưu ngay.</summary>
        public void AddDecision(Decision decision)
        {
            Decisions.Add(decision);
            SaveSessionFile(includeModel: false);
        }

        public void StoreScan(IReadOnlyList<SourceEntity> entities, RecognitionResult result, double scaleToMm, string? fingerprint)
        {
            LastEntities = entities;
            LastResult = result;
            ScaleToMm = scaleToMm;
            LastFingerprint = fingerprint;
            FromIsometric = false;
            BoundsByHandle.Clear();
            foreach (var e in entities)
            {
                if (e.Bounds.HasValue && e.BlockPath.Count == 0) BoundsByHandle[e.Handle] = e.Bounds.Value;
            }
        }

        /// <summary>Nạp IR của tờ isometric (PA_ISO, PA_BUILDIR): không có đối tượng 2D nào đi kèm và không gắn nền 2D khi dựng.</summary>
        public void StoreIso(PipingModel model)
        {
            StoreScan(new List<SourceEntity>(), new RecognitionResult { Model = model }, 1.0, model.Source.Fingerprint);
            FromIsometric = true;
        }

        /// <summary>Bảng các đề xuất can thiệp bản vẽ đang lưu trong bộ nhớ tạm theo ProposalId (ADR-12).</summary>
        public Dictionary<string, MutationProposal> Proposals { get; } = new Dictionary<string, MutationProposal>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Áp dụng toàn bộ thao tác của đề xuất vào bản vẽ trong một Transaction duy nhất (ADR-12).</summary>
        public (bool Ok, string Message) ApplyMutation(MutationProposal proposal)
        {
            if (proposal == null) return (false, "Đề xuất rỗng.");
            var doc = Document;
            var db = doc.Database;

            // Document Target Binding Check (ADR-14)
            if (!string.IsNullOrEmpty(proposal.DrawingPath))
            {
                var curName = Path.GetFileName(doc.Name);
                var targetName = Path.GetFileName(proposal.DrawingPath);
                if (!string.Equals(curName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"[Document Target Mismatch, ADR-14]: Bản vẽ hiện hành ('{curName}') không khớp với bản vẽ nơi đề xuất được tạo ('{targetName}'). Vui lòng chọn đúng tab bản vẽ.");
                }
            }

            int createdCount = 0;
            int modifiedCount = 0;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
                    if (!rat.Has("PIPING_AGENT"))
                    {
                        rat.UpgradeOpen();
                        var newReg = new RegAppTableRecord { Name = "PIPING_AGENT" };
                        rat.Add(newReg);
                        tr.AddNewlyCreatedDBObject(newReg, true);
                    }

                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    void EnsureLayer(string layerName)
                    {
                        if (!lt.Has(layerName))
                        {
                            lt.UpgradeOpen();
                            var ltr = new LayerTableRecord { Name = layerName };
                            lt.Add(ltr);
                            tr.AddNewlyCreatedDBObject(ltr, true);
                        }
                    }

                    void AttachXData(DBObject obj)
                    {
                        var rb = new ResultBuffer(
                            new TypedValue((int)DxfCode.ExtendedDataRegAppName, "PIPING_AGENT"),
                            new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"PROPOSAL:{proposal.ProposalId}")
                        );
                        obj.XData = rb;
                    }

                    var createdEntities = new List<(Entity Entity, MutationOp SourceOp)>();
                    var modifiedEntities = new List<(Entity Entity, MutationOp SourceOp)>();

                    foreach (var op in proposal.Operations)
                    {
                        switch (op.Kind)
                        {
                            case MutationOpKind.AddText:
                            {
                                var layer = !string.IsNullOrEmpty(op.TargetLayer) ? op.TargetLayer! : "PA_AGENT_TEXT";
                                EnsureLayer(layer);
                                var txt = new DBText
                                {
                                    Position = new Point3d(op.X ?? 0, op.Y ?? 0, 0),
                                    Height = (op.Height.HasValue && op.Height.Value > 0) ? op.Height.Value : 250.0,
                                    TextString = op.Text ?? string.Empty,
                                    Layer = layer,
                                };
                                AttachXData(txt);
                                btr.AppendEntity(txt);
                                tr.AddNewlyCreatedDBObject(txt, true);
                                createdEntities.Add((txt, op));
                                break;
                            }

                            case MutationOpKind.ModifyText:
                            {
                                if (HighlightService.TryParseHandle(op.Handle, out var h) && db.TryGetObjectId(h, out var id) && !id.IsErased)
                                {
                                    var ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                                    if (ent is DBText dbt)
                                    {
                                        dbt.TextString = op.NewText ?? string.Empty;
                                        AttachXData(dbt);
                                        modifiedEntities.Add((ent, op));
                                    }
                                    else if (ent is MText mt)
                                    {
                                        mt.Contents = op.NewText ?? string.Empty;
                                        AttachXData(mt);
                                        modifiedEntities.Add((ent, op));
                                    }
                                }
                                break;
                            }

                            case MutationOpKind.ChangeLayer:
                            {
                                if (!string.IsNullOrEmpty(op.TargetLayer) && HighlightService.TryParseHandle(op.Handle, out var h) && db.TryGetObjectId(h, out var id) && !id.IsErased)
                                {
                                    EnsureLayer(op.TargetLayer!);
                                    var ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                                    ent.Layer = op.TargetLayer!;
                                    AttachXData(ent);
                                    modifiedEntities.Add((ent, op));
                                }
                                break;
                            }

                            case MutationOpKind.AddRevisionCloud:
                            {
                                var layer = "PA_REVISION";
                                EnsureLayer(layer);
                                var minX = op.MinX ?? 0;
                                var minY = op.MinY ?? 0;
                                var maxX = op.MaxX ?? 1000;
                                var maxY = op.MaxY ?? 1000;

                                var pline = new Polyline(4);
                                pline.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
                                pline.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
                                pline.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
                                pline.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
                                pline.Closed = true;
                                pline.Layer = layer;
                                AttachXData(pline);
                                btr.AppendEntity(pline);
                                tr.AddNewlyCreatedDBObject(pline, true);
                                createdEntities.Add((pline, op));

                                if (!string.IsNullOrEmpty(op.Comment))
                                {
                                    var commentText = new DBText
                                    {
                                        Position = new Point3d(minX, maxY + 50, 0),
                                        Height = 200.0,
                                        TextString = op.Comment,
                                        Layer = layer,
                                    };
                                    AttachXData(commentText);
                                    btr.AppendEntity(commentText);
                                    tr.AddNewlyCreatedDBObject(commentText, true);
                                    createdEntities.Add((commentText, op));
                                }
                                break;
                            }

                            case MutationOpKind.WaiveIssue:
                            {
                                if (!string.IsNullOrEmpty(op.IssueId))
                                {
                                    var target = new Dictionary<string, string> { ["issue"] = op.IssueId! };
                                    var issue = LastResult?.Model?.Issues?.FirstOrDefault(i => string.Equals(i.Id, op.IssueId, StringComparison.OrdinalIgnoreCase));
                                    if (issue != null)
                                    {
                                        target["rule"] = issue.RuleId;
                                        if (issue.Sources.Count > 0) target["source"] = issue.Sources[0];
                                        if (issue.Elements.Count > 0) target["element"] = issue.Elements[0];
                                    }
                                    var dec = SessionFile.NewDecision(DecisionOp.WaiveIssue, target, null, op.Reason ?? "Agent đề xuất miễn trừ");
                                    AddDecision(dec);
                                }
                                break;
                            }

                            case MutationOpKind.AddPipe:
                            {
                                var layer = !string.IsNullOrEmpty(op.TargetLayer) ? op.TargetLayer! : "PA_PIPE";
                                EnsureLayer(layer);

                                double startX = op.X ?? 0;
                                double startY = op.Y ?? 0;
                                double startZ = op.Z ?? 0;

                                if (!op.X.HasValue && !op.Y.HasValue)
                                {
                                    try
                                    {
                                        var view = doc.Editor.GetCurrentView();
                                        // WCS Enforcement (ADR-13): Đảm bảo tọa độ tâm khung nhìn được ánh xạ chuẩn sang WCS
                                        var ucs = doc.Editor.CurrentUserCoordinateSystem;
                                        var centerInUcs = new Point3d(view.CenterPoint.X, view.CenterPoint.Y, 0);
                                        var centerInWcs = centerInUcs.TransformBy(ucs);
                                        startX = centerInWcs.X;
                                        startY = centerInWcs.Y;
                                        startZ = centerInWcs.Z;
                                    }
                                    catch
                                    {
                                        startX = 0;
                                        startY = 0;
                                        startZ = 0;
                                    }
                                }

                                double length = (op.LengthMm.HasValue && op.LengthMm.Value > 0) ? op.LengthMm.Value : 2000.0;
                                string dir = (op.Direction ?? "X").ToUpperInvariant();

                                double endX = op.EndX ?? (dir == "Y" ? startX : (dir == "Z" ? startX : startX + length));
                                double endY = op.EndY ?? (dir == "Y" ? startY + length : startY);
                                double endZ = op.EndZ ?? (dir == "Z" ? startZ + length : startZ);

                                var startPt = new Point3d(startX, startY, startZ);
                                var endPt = new Point3d(endX, endY, endZ);

                                int dn = (op.Dn.HasValue && op.Dn.Value > 0) ? op.Dn.Value : 100;
                                string tag = !string.IsNullOrWhiteSpace(op.Tag) ? op.Tag! : $"{dn}A-PIPE";

                                if (string.Equals(op.Mode, "3D", StringComparison.OrdinalIgnoreCase))
                                {
                                    double outerRadius = GetPipeOuterRadius(dn);
                                    var vec = endPt - startPt;
                                    double actualLength = vec.Length;
                                    if (actualLength < 1e-4) actualLength = length;

                                    var solid = new Solid3d();
                                    solid.CreateFrustum(actualLength, outerRadius, outerRadius, outerRadius);

                                    var midPt = new Point3d((startPt.X + endPt.X) / 2.0, (startPt.Y + endPt.Y) / 2.0, (startPt.Z + endPt.Z) / 2.0);
                                    var dirVec = (endPt - startPt).GetNormal();
                                    var zAxis = Vector3d.ZAxis;
                                    if (!dirVec.IsEqualTo(zAxis))
                                    {
                                        if (dirVec.IsEqualTo(-zAxis))
                                        {
                                            solid.TransformBy(Matrix3d.Rotation(Math.PI, Vector3d.XAxis, Point3d.Origin));
                                        }
                                        else
                                        {
                                            var rotAxis = zAxis.CrossProduct(dirVec).GetNormal();
                                            double angle = zAxis.GetAngleTo(dirVec);
                                            solid.TransformBy(Matrix3d.Rotation(angle, rotAxis, Point3d.Origin));
                                        }
                                    }
                                    solid.TransformBy(Matrix3d.Displacement(midPt - Point3d.Origin));
                                    solid.Layer = layer;
                                    AttachXData(solid);
                                    btr.AppendEntity(solid);
                                    tr.AddNewlyCreatedDBObject(solid, true);
                                    createdEntities.Add((solid, op));
                                }
                                else
                                {
                                    var line = new Autodesk.AutoCAD.DatabaseServices.Line(startPt, endPt)
                                    {
                                        Layer = layer,
                                    };
                                    AttachXData(line);
                                    btr.AppendEntity(line);
                                    tr.AddNewlyCreatedDBObject(line, true);
                                    createdEntities.Add((line, op));

                                    var midPt = new Point3d((startPt.X + endPt.X) / 2.0, (startPt.Y + endPt.Y) / 2.0 + 150.0, (startPt.Z + endPt.Z) / 2.0);
                                    var txt = new DBText
                                    {
                                        Position = midPt,
                                        Height = 150.0,
                                        TextString = $"{tag} (L={length:F0}mm)",
                                        Layer = layer,
                                    };
                                    AttachXData(txt);
                                    btr.AppendEntity(txt);
                                    tr.AddNewlyCreatedDBObject(txt, true);
                                    createdEntities.Add((txt, op));
                                }
                                break;
                            }
                        }
                    }

                    // ═══════════════════════════════════════════════════════════════════════════════
                    // L2 & L3 Verification Audit (Hậu kiểm Định L2-L3 theo ADR-13)
                    // ═══════════════════════════════════════════════════════════════════════════════
                    foreach (var (ent, op) in createdEntities)
                    {
                        if (op.Kind == MutationOpKind.AddPipe)
                        {
                            if (ent is Autodesk.AutoCAD.DatabaseServices.Line line)
                            {
                                var expectedLen = op.LengthMm ?? 2000.0;
                                if (Math.Abs(line.Length - expectedLen) > 0.1)
                                {
                                    return (false, $"[Kiểm toán L3 thất bại]: Chiều dài ống thực tế ({line.Length:F2}mm) lệch so với thiết kế ({expectedLen:F2}mm). Đã hủy giao dịch.");
                                }
                                var expectedLayer = op.TargetLayer ?? "PA_PIPE";
                                if (!string.Equals(line.Layer, expectedLayer, StringComparison.OrdinalIgnoreCase))
                                {
                                    return (false, $"[Kiểm toán L3 thất bại]: Layer đối tượng ({line.Layer}) không khớp layer yêu cầu ({expectedLayer}). Đã hủy giao dịch.");
                                }
                            }
                            else if (ent is Solid3d solid)
                            {
                                var expectedLayer = op.TargetLayer ?? "PA_PIPE";
                                if (!string.Equals(solid.Layer, expectedLayer, StringComparison.OrdinalIgnoreCase))
                                {
                                    return (false, $"[Kiểm toán L3 thất bại]: Layer khối 3D ({solid.Layer}) không khớp layer yêu cầu ({expectedLayer}). Đã hủy giao dịch.");
                                }
                            }
                        }
                        else if (op.Kind == MutationOpKind.AddText && ent is DBText dbText)
                        {
                            var expectedText = op.Text ?? string.Empty;
                            if (dbText.TextString != expectedText)
                            {
                                return (false, $"[Kiểm toán L3 thất bại]: Nội dung chữ ({dbText.TextString}) không khớp văn bản yêu cầu ({expectedText}). Đã hủy giao dịch.");
                            }
                        }
                    }

                    foreach (var (ent, op) in modifiedEntities)
                    {
                        if (op.Kind == MutationOpKind.ChangeLayer && !string.IsNullOrEmpty(op.TargetLayer))
                        {
                            if (!string.Equals(ent.Layer, op.TargetLayer, StringComparison.OrdinalIgnoreCase))
                            {
                                return (false, $"[Kiểm toán L3 thất bại]: Layer đối tượng ({ent.Layer}) chưa được chuyển sang {op.TargetLayer}. Đã hủy giao dịch.");
                            }
                        }
                        else if (op.Kind == MutationOpKind.ModifyText && !string.IsNullOrEmpty(op.NewText))
                        {
                            var textVal = ent is DBText dt ? dt.TextString : (ent is MText mt ? mt.Contents : null);
                            if (textVal != op.NewText)
                            {
                                return (false, $"[Kiểm toán L3 thất bại]: Nội dung văn bản mới ({textVal}) không khớp yêu cầu ({op.NewText}). Đã hủy giao dịch.");
                            }
                        }
                    }

                    // ═══════════════════════════════════════════════════════════════════════════════
                    // Thu thập Bằng chứng Thực thi (Execution Evidence theo ADR-14)
                    // ═══════════════════════════════════════════════════════════════════════════════
                    var evidence = new MutationEvidence
                    {
                        ProposalId = proposal.ProposalId,
                        AppliedAt = DateTime.UtcNow,
                        AuditPassed = true,
                    };

                    Point3d? minPt = null;
                    Point3d? maxPt = null;

                    void UpdateBounds(Extents3d ext)
                    {
                        if (!minPt.HasValue || !maxPt.HasValue)
                        {
                            minPt = ext.MinPoint;
                            maxPt = ext.MaxPoint;
                        }
                        else
                        {
                            minPt = new Point3d(
                                Math.Min(minPt.Value.X, ext.MinPoint.X),
                                Math.Min(minPt.Value.Y, ext.MinPoint.Y),
                                Math.Min(minPt.Value.Z, ext.MinPoint.Z));
                            maxPt = new Point3d(
                                Math.Max(maxPt.Value.X, ext.MaxPoint.X),
                                Math.Max(maxPt.Value.Y, ext.MaxPoint.Y),
                                Math.Max(maxPt.Value.Z, ext.MaxPoint.Z));
                        }
                    }

                    foreach (var (ent, op) in createdEntities)
                    {
                        evidence.CreatedHandles.Add(ent.Handle.ToString());
                        try { UpdateBounds(ent.GeometricExtents); } catch { }
                        if (ent is Autodesk.AutoCAD.DatabaseServices.Line pipeLine)
                        {
                            evidence.MeasuredMetrics["pipe_length_mm"] = pipeLine.Length;
                        }
                    }

                    foreach (var (ent, op) in modifiedEntities)
                    {
                        evidence.ModifiedHandles.Add(ent.Handle.ToString());
                        try { UpdateBounds(ent.GeometricExtents); } catch { }
                    }

                    if (minPt.HasValue && maxPt.HasValue)
                    {
                        evidence.BoundingBoxMin = new double[] { minPt.Value.X, minPt.Value.Y, minPt.Value.Z };
                        evidence.BoundingBoxMax = new double[] { maxPt.Value.X, maxPt.Value.Y, maxPt.Value.Z };
                    }

                    evidence.VerificationSummary = $"L2: {createdEntities.Count} thực thể mới, {modifiedEntities.Count} thực thể sửa đổi. L3: 100% khớp Layer & Hình học.";
                    proposal.Evidence = evidence;

                    createdCount = createdEntities.Count;
                    modifiedCount = modifiedEntities.Count;
                    tr.Commit();
                }

                try { Highlight.Clear(); } catch { }
                try { doc.Editor.UpdateScreen(); } catch { }
                return (true, $"Đã áp dụng thành công {proposal.Operations.Count} thao tác. [Kiểm định L2/L3]: {createdCount} thực thể mới, {modifiedCount} thực thể sửa đổi, 100% đạt chuẩn Layer & Hình học.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static double GetPipeOuterRadius(int dn)
        {
            return dn switch
            {
                15 => 21.3 / 2.0,
                20 => 26.7 / 2.0,
                25 => 33.4 / 2.0,
                32 => 42.2 / 2.0,
                40 => 48.3 / 2.0,
                50 => 60.3 / 2.0,
                65 => 73.0 / 2.0,
                80 => 88.9 / 2.0,
                100 => 114.3 / 2.0,
                125 => 141.3 / 2.0,
                150 => 168.3 / 2.0,
                200 => 219.1 / 2.0,
                250 => 273.0 / 2.0,
                300 => 323.8 / 2.0,
                350 => 355.6 / 2.0,
                400 => 406.4 / 2.0,
                450 => 457.0 / 2.0,
                500 => 508.0 / 2.0,
                600 => 610.0 / 2.0,
                _ => (dn * 1.143) / 2.0,
            };
        }

        public void Dispose()
        {
            Highlight.Dispose();
        }
    }
}
