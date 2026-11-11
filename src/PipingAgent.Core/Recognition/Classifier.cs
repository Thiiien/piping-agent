using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Một đối tượng nguồn đã được gán vai trò.</summary>
    public sealed class ClassifiedEntity
    {
        public ClassifiedEntity(SourceEntity entity, string role, int ruleIndex)
        {
            Entity = entity;
            Role = role;
            RuleIndex = ruleIndex;
        }

        public SourceEntity Entity { get; }

        /// <summary>Vai trò, một trong <see cref="EntityRoles"/>. <c>unclassified</c> nếu không khớp quy tắc nào.</summary>
        public string Role { get; }

        /// <summary>Chỉ số quy tắc đã khớp trong <c>roles</c> của hồ sơ, hoặc -1.</summary>
        public int RuleIndex { get; }

        public bool IsClassified => Role != EntityRoles.Unclassified;
    }

    public sealed class ClassificationResult
    {
        public List<ClassifiedEntity> Items { get; } = new List<ClassifiedEntity>();

        public ClassificationReport Report { get; set; } = new ClassificationReport();

        public IEnumerable<ClassifiedEntity> WithRole(string role) => Items.Where(i => i.Role == role);

        public IEnumerable<SourceEntity> EntitiesWithRole(string role) => WithRole(role).Select(i => i.Entity);
    }

    /// <summary>
    /// Bộ phân loại theo hồ sơ quy ước, S2-03. Quy tắc xét theo thứ tự trong hồ sơ, quy tắc đầu tiên khớp thắng,
    /// nên kết quả không đổi giữa các lần chạy. Không giữ trạng thái giữa hai bản vẽ.
    /// </summary>
    public sealed class Classifier
    {
        private readonly CompiledRule[] _rules;

        public Classifier(ConventionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Profile = profile;
            _rules = profile.Roles.Select((r, i) => new CompiledRule(r, i)).ToArray();
        }

        public ConventionProfile Profile { get; }

        public ClassificationResult Classify(IEnumerable<SourceEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var result = new ClassificationResult();
            foreach (var e in entities)
            {
                result.Items.Add(ClassifyOne(e));
            }

            result.Report = ClassificationReport.Build(result.Items, Profile.Tolerances.UnclassifiedWarning);
            return result;
        }

        public ClassifiedEntity ClassifyOne(SourceEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            foreach (var rule in _rules)
            {
                if (rule.Matches(entity)) return new ClassifiedEntity(entity, rule.Role, rule.Index);
            }

            return new ClassifiedEntity(entity, EntityRoles.Unclassified, -1);
        }

        private sealed class CompiledRule
        {
            private readonly Wildcard[]? _layers;
            private readonly string[]? _types;
            private readonly Wildcard? _block;
            private readonly int? _color;
            private readonly Wildcard? _linetype;
            private readonly Regex? _text;

            public CompiledRule(RoleRule rule, int index)
            {
                Role = rule.Role;
                Index = index;
                var m = rule.Match;
                var layers = new List<Wildcard>();
                if (m.Layer != null) layers.Add(new Wildcard(m.Layer));
                if (m.Layers != null) layers.AddRange(m.Layers.Select(l => new Wildcard(l)));
                _layers = layers.Count > 0 ? layers.ToArray() : null;
                _types = m.Types?.Select(t => t.Trim().ToUpperInvariant()).ToArray();
                _block = m.Block != null ? new Wildcard(m.Block) : null;
                _color = m.Color;
                _linetype = m.Linetype != null ? new Wildcard(m.Linetype) : null;
                _text = m.TextPattern != null ? new Regex(m.TextPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) : null;
            }

            public string Role { get; }

            public int Index { get; }

            public bool Matches(SourceEntity e)
            {
                if (_layers != null && !_layers.Any(w => w.IsMatch(e.Layer))) return false;
                if (_types != null && !TypeMatches(e)) return false;
                if (_block != null && (e.Kind != SourceKind.Insert || !_block.IsMatch(e.Name))) return false;
                if (_color.HasValue && e.Color != _color.Value) return false;
                if (_linetype != null && !_linetype.IsMatch(e.Linetype)) return false;
                if (_text != null && (e.Text == null || !_text.IsMatch(e.Text))) return false;
                return true;
            }

            private bool TypeMatches(SourceEntity e)
            {
                var kind = e.Kind.ToString().ToUpperInvariant();
                var native = e.NativeType?.ToUpperInvariant();
                foreach (var t in _types!)
                {
                    if (t == kind || (native != null && t == native)) return true;
                    if (t == "POLYLINE" && e.Kind == SourceKind.Polyline) return true;
                    if (t == "TEXT" && e.Kind == SourceKind.Text) return true;
                    if (t == "INSERT" && e.Kind == SourceKind.Insert) return true;
                }

                return false;
            }
        }
    }

    /// <summary>Báo cáo phân loại, S2-04: tỷ lệ tổng và bảng theo layer.</summary>
    public sealed class ClassificationReport
    {
        public int Total { get; set; }

        public int Unclassified { get; set; }

        public double UnclassifiedRatio => Total == 0 ? 0 : (double)Unclassified / Total;

        /// <summary>Ngưỡng R-GEN-01 của hồ sơ.</summary>
        public double WarningThreshold { get; set; }

        public bool ExceedsThreshold => Total > 0 && UnclassifiedRatio > WarningThreshold;

        /// <summary>Số đối tượng theo vai trò, kể cả <c>unclassified</c>.</summary>
        public Dictionary<string, int> ByRole { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Bảng theo layer, xếp theo số chưa phân loại giảm dần.</summary>
        public List<LayerRow> Layers { get; } = new List<LayerRow>();

        public static ClassificationReport Build(IEnumerable<ClassifiedEntity> items, double warningThreshold)
        {
            var report = new ClassificationReport { WarningThreshold = warningThreshold };
            var byLayer = new Dictionary<string, LayerRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                report.Total++;
                if (!item.IsClassified) report.Unclassified++;
                report.ByRole[item.Role] = report.ByRole.TryGetValue(item.Role, out var n) ? n + 1 : 1;

                var layer = item.Entity.Layer ?? "0";
                if (!byLayer.TryGetValue(layer, out var row))
                {
                    row = new LayerRow { Layer = layer };
                    byLayer[layer] = row;
                }

                row.Total++;
                if (!item.IsClassified)
                {
                    row.Unclassified++;
                    var kind = item.Entity.NativeType ?? item.Entity.Kind.ToString().ToUpperInvariant();
                    row.UnclassifiedByType[kind] = row.UnclassifiedByType.TryGetValue(kind, out var k) ? k + 1 : 1;
                }
                else
                {
                    row.ByRole[item.Role] = row.ByRole.TryGetValue(item.Role, out var k) ? k + 1 : 1;
                }
            }

            report.Layers.AddRange(byLayer.Values
                .OrderByDescending(r => r.Unclassified)
                .ThenByDescending(r => r.Total)
                .ThenBy(r => r.Layer, StringComparer.OrdinalIgnoreCase));
            return report;
        }

        public sealed class LayerRow
        {
            public string Layer { get; set; } = string.Empty;

            public int Total { get; set; }

            public int Unclassified { get; set; }

            public double UnclassifiedRatio => Total == 0 ? 0 : (double)Unclassified / Total;

            public Dictionary<string, int> ByRole { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Loại gốc của các đối tượng chưa phân loại, để biết layer đó còn thiếu quy tắc nào.</summary>
            public Dictionary<string, int> UnclassifiedByType { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }
}
