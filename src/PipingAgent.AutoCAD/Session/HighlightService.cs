using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using PipingAgent.Core.Geometry;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Tô sáng bằng đồ hoạ tạm thời và phóng khung nhìn, S2-13. Không tạo đối tượng thật, không đổi cơ sở dữ liệu bản vẽ,
    /// nên dấu vân tay bản vẽ không đổi (FR-REV-02, mục 10.4).
    /// </summary>
    public sealed class HighlightService : IDisposable
    {
        private readonly Document _document;
        private readonly List<Entity> _transients = new List<Entity>();

        public HighlightService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Tô sáng các đối tượng theo handle. Handle không tồn tại thì bỏ qua.</summary>
        public int Highlight(IEnumerable<string> handles, short colorIndex = 1)
        {
            if (handles == null) throw new ArgumentNullException(nameof(handles));
            var db = _document.Database;
            var tm = TransientManager.CurrentTransientManager;
            var added = 0;
            using (_document.LockDocument())
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var h in handles)
                {
                    if (!TryParseHandle(h, out var handle)) continue;
                    if (!db.TryGetObjectId(handle, out var id) || id.IsErased) continue;
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity entity)) continue;
                    var clone = (Entity)entity.Clone();
                    clone.ColorIndex = colorIndex;
                    clone.LineWeight = LineWeight.LineWeight060;
                    tm.AddTransient(clone, TransientDrawingMode.Highlight, 128, new IntegerCollection());
                    _transients.Add(clone);
                    added++;
                }
            }

            _document.Editor.UpdateScreen();
            return added;
        }

        public void Clear()
        {
            if (_transients.Count == 0) return;
            var tm = TransientManager.CurrentTransientManager;
            foreach (var e in _transients)
            {
                try
                {
                    tm.EraseTransient(e, new IntegerCollection());
                }
                finally
                {
                    e.Dispose();
                }
            }

            _transients.Clear();
            _document.Editor.UpdateScreen();
        }

        /// <summary>Phóng khung nhìn tới vùng mm. Hệ số đổi mm về đơn vị bản vẽ truyền vào.</summary>
        public void ZoomTo(Box2 regionMm, double scaleToMm)
        {
            if (scaleToMm <= 0) scaleToMm = 1;
            var min = new Point2d(regionMm.Min.X / scaleToMm, regionMm.Min.Y / scaleToMm);
            var max = new Point2d(regionMm.Max.X / scaleToMm, regionMm.Max.Y / scaleToMm);
            var width = Math.Max(max.X - min.X, 1e-3);
            var height = Math.Max(max.Y - min.Y, 1e-3);
            using (_document.LockDocument())
            using (var view = _document.Editor.GetCurrentView())
            {
                // Giữ tỉ lệ khung nhìn hiện tại để vùng không bị méo.
                var aspect = view.Height > 0 ? view.Width / view.Height : 1.0;
                if (width / height > aspect) height = width / aspect; else width = height * aspect;
                view.CenterPoint = new Point2d((min.X + max.X) / 2, (min.Y + max.Y) / 2);
                view.Width = width;
                view.Height = height;
                _document.Editor.SetCurrentView(view);
            }
        }

        public static bool TryParseHandle(string? text, out Handle handle)
        {
            handle = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                handle = new Handle(Convert.ToInt64(text, 16));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                Clear();
            }
            catch (System.Exception)
            {
                // Bản vẽ đang đóng, đồ hoạ tạm thời đã bị huỷ cùng khung nhìn.
            }
        }
    }
}
