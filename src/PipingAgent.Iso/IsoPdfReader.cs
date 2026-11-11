using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Graphics;

namespace PipingAgent.Iso
{
    /// <summary>
    /// Đọc một trang PDF vector thành đối tượng nguồn, I1. Đơn vị là điểm PDF, trục Y hướng lên như hệ toạ độ PDF.
    /// Đường thành <see cref="SourceKind.Line"/> có <see cref="SourceEntity.LineWidth"/>; chữ thành <see cref="SourceKind.Text"/>
    /// có vị trí, chiều cao và góc xoay theo hướng viết. Không đổi tỉ lệ: isometric không vẽ đúng tỉ lệ, kích thước ghi mới là số thật.
    /// </summary>
    public static class IsoPdfReader
    {
        public sealed class Page
        {
            public int Number { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }

            public List<SourceEntity> Entities { get; } = new List<SourceEntity>();
        }

        public static List<Page> Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Không thấy tệp PDF.", path);
            using (var stream = File.OpenRead(path))
            {
                return Read(stream);
            }
        }

        public static List<Page> Read(Stream stream)
        {
            var pages = new List<Page>();
            using (var doc = PdfDocument.Open(stream))
            {
                foreach (var pdfPage in doc.GetPages())
                {
                    var page = new Page { Number = pdfPage.Number, Width = pdfPage.Width, Height = pdfPage.Height };
                    var n = 0;
                    foreach (var path in pdfPage.Paths)
                    {
                        var width = path.LineWidth;
                        void AddLine(PdfPoint from, PdfPoint to) => page.Entities.Add(new SourceEntity
                        {
                            Handle = "P" + (++n).ToString("X", CultureInfo.InvariantCulture),
                            Kind = SourceKind.Line,
                            NativeType = "PDFLINE",
                            Layer = width >= 2.0 ? "THICK" : (width <= 0.01 ? "HAIR" : "THIN"),
                            LineWidth = width,
                            Points = new List<Point3> { new Point3(from.X, from.Y, 0), new Point3(to.X, to.Y, 0) },
                        });

                        foreach (var sub in path)
                        {
                            PdfPoint? current = null;
                            PdfPoint? first = null;
                            foreach (var cmd in sub.Commands)
                            {
                                switch (cmd)
                                {
                                    case PdfSubpath.Move m:
                                        current = m.Location;
                                        first = m.Location;
                                        break;
                                    case PdfSubpath.Line l:
                                        if (current.HasValue) AddLine(l.From, l.To);
                                        current = l.To;
                                        break;
                                    case PdfSubpath.BezierCurve b:
                                        current = b.EndPoint;
                                        break;
                                    case PdfSubpath.Close _:
                                        // Cạnh khép của hình chữ nhật, cần để nhận ra ô số thứ tự phụ kiện.
                                        if (current.HasValue && first.HasValue && !current.Value.Equals(first.Value)) AddLine(current.Value, first.Value);
                                        break;
                                }
                            }
                        }
                    }

                    foreach (var word in pdfPage.GetWords(NearestNeighbourWordExtractor.Instance))
                    {
                        var box = word.BoundingBox;
                        var rotation = box.Rotation;
                        var letter = word.Letters.FirstOrDefault();
                        var height = letter != null ? Math.Max(letter.PointSize, 1) : box.Height;
                        page.Entities.Add(new SourceEntity
                        {
                            Handle = "T" + (++n).ToString("X", CultureInfo.InvariantCulture),
                            Kind = SourceKind.Text,
                            NativeType = "PDFTEXT",
                            Layer = "TEXT",
                            Text = word.Text,
                            RawText = word.Text,
                            TextHeight = height,
                            RotationDeg = Math.Round(rotation, 1),
                            Center = new Point3(box.BottomLeft.X, box.BottomLeft.Y, 0),
                            Bounds = new Box2(new Point2(Math.Min(box.BottomLeft.X, box.TopRight.X), Math.Min(box.BottomLeft.Y, box.TopRight.Y)), new Point2(Math.Max(box.BottomLeft.X, box.TopRight.X), Math.Max(box.BottomLeft.Y, box.TopRight.Y))),
                        });
                    }

                    foreach (var e in page.Entities.Where(e => e.Bounds == null)) e.Bounds = e.ComputeBounds();
                    pages.Add(page);
                }
            }

            return pages;
        }
    }
}
