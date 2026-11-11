using System;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.Core.Recognition;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Thanh tiến độ và nút hủy cho thao tác dài, S2-14. Lõi chạy đồng bộ trên luồng chính của AutoCAD và báo tiến độ
    /// qua <see cref="IProgress{T}"/>; mỗi lần báo, lớp này cập nhật thước đo của AutoCAD, bảng điều khiển nếu có,
    /// và kiểm tra người dùng bấm Esc để hủy qua <see cref="CancellationToken"/>.
    /// </summary>
    public sealed class AcadProgress : IProgress<ProgressInfo>, IDisposable
    {
        private const int Steps = 100;
        private readonly ProgressMeter _meter;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Action<ProgressInfo>? _onProgress;
        private int _shown;

        public AcadProgress(string title, Action<ProgressInfo>? onProgress = null)
        {
            _meter = new ProgressMeter();
            _meter.Start(title);
            _meter.SetLimit(Steps);
            _onProgress = onProgress;
        }

        public CancellationToken Token => _cts.Token;

        /// <summary>Gọi từ bảng điều khiển khi người dùng bấm Hủy.</summary>
        public void Cancel() => _cts.Cancel();

        public void Report(ProgressInfo value)
        {
            var target = (int)Math.Round(Math.Max(0, Math.Min(1, value.Fraction)) * Steps);
            while (_shown < target)
            {
                _meter.MeterProgress();
                _shown++;
            }

            _onProgress?.Invoke(value);
            if (HostApplicationServices.Current.UserBreak()) _cts.Cancel();
        }

        public void Dispose()
        {
            _meter.Stop();
            _meter.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>Đưa một việc về luồng chính của AutoCAD, trong ngữ cảnh lệnh, từ bất kỳ luồng nào. S2-14.</summary>
    public static class HostThread
    {
        public static void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var dm = AcApp.DocumentManager;
            if (dm.IsApplicationContext)
            {
                dm.ExecuteInCommandContextAsync(_ =>
                {
                    action();
                    return Task.CompletedTask;
                }, null);
            }
            else
            {
                action();
            }
        }
    }
}
