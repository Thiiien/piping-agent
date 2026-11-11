using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.Core;
using PipingAgent.Core.Diagnostics;
// Dùng Core.Application thay vì Application để plug-in nạp được cả trong accoreconsole,
// nơi không có AcMgd.dll. Xem NFR-MNT-07.
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(PipingAgent.AutoCAD.PluginApp))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.HelloCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.ScanCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.ScanWindowCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.PanelCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.BuildCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.CheckCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.FixCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.WaiveCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.RerunCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.BuildIrCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.IsoCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.PortCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.RibbonCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.AskCommand))]
[assembly: CommandClass(typeof(PipingAgent.AutoCAD.Commands.AgentConfigCommand))]

namespace PipingAgent.AutoCAD
{
    /// <summary>Điểm vào của plug-in. AutoCAD gọi khi nạp tệp DLL. Mở nhật ký phiên và đóng khi thoát.</summary>
    public sealed class PluginApp : IExtensionApplication
    {
        public static string Version => typeof(PluginApp).Assembly.GetName().Version?.ToString() ?? "0";

        /// <summary>Nhật ký của phiên hiện tại. Null nếu không mở được tệp nhật ký, khi đó lệnh vẫn chạy.</summary>
        public static JsonLineLogger? Log { get; private set; }

        public static string? LogPath { get; private set; }

        /// <summary>Phiên theo từng bản vẽ đang mở, S2-15. Null trước khi khởi tạo xong.</summary>
        public static Session.SessionManager? Sessions { get; private set; }

        public void Initialize()
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipingAgent", "logs");
                Directory.CreateDirectory(folder);
                LogPath = Path.Combine(folder, "pipingagent-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
                var writer = new StreamWriter(new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                Log = new JsonLineLogger(writer, "AutoCAD " + AcApp.Version, RuntimeInformation.FrameworkDescription);
            }
            catch (System.Exception)
            {
                Log = null;
            }

            Sessions = new Session.SessionManager();
            if (Commands.PanelHost.IsGuiAvailable())
            {
                try { InstallRibbon(); }
                catch (System.Exception ex) { Log?.Error("ribbon_failed", ex, null); }
            }

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage($"\nPiping Agent {Version} đã nạp. Lệnh: PA_ISO, PA_BUILD, PA_BUILDIR, PA_PORT, PA_CONVERT, PA_SCAN, PA_SCANWINDOW, PA_CHECK, PA_FIX, PA_WAIVE, PA_RERUN, PA_PANEL, PA_ASK, PA_AGENTCONFIG, PA_RIBBON, PA_HELLO.\n");
        }

        /// <summary>Tách riêng để JIT không nạp AdWindows trong accoreconsole.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void InstallRibbon() => Ribbon.PipingRibbon.Install();

        public void Terminate()
        {
            Sessions?.Dispose();
            Sessions = null;
            Log?.Dispose();
            Log = null;
        }
    }
}
