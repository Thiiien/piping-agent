using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using PipingAgent.Core.Diagnostics;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>Ngữ cảnh một lần chạy lệnh: tài liệu đã khoá, giao dịch chỉ đọc đang mở.</summary>
    public sealed class CommandContext
    {
        public CommandContext(Document document, Transaction transaction, JsonLineLogger? log)
        {
            Document = document;
            Transaction = transaction;
            Log = log;
        }

        public Document Document { get; }

        public Database Database => Document.Database;

        public Editor Editor => Document.Editor;

        public Transaction Transaction { get; }

        public JsonLineLogger? Log { get; }

        public void Write(string message) => Editor.WriteMessage("\n" + message);
    }

    /// <summary>
    /// Lớp cơ sở của mọi lệnh. Việc S1-10: bắt ngoại lệ để AutoCAD không thoát, NFR-REL-01;
    /// khoá tài liệu; mở giao dịch chỉ đọc; ghi nhật ký có thời gian từng lệnh; báo lỗi rõ ràng cho người dùng, NFR-USE-02.
    /// Lệnh con chỉ cài <see cref="Run"/>.
    /// </summary>
    public abstract class CommandBase
    {
        protected abstract string Name { get; }

        /// <summary>Lệnh sửa bản vẽ thì mở giao dịch ghi. Mặc định chỉ đọc theo ADR về bản vẽ nguồn chỉ đọc.</summary>
        protected virtual bool ReadOnly => true;

        protected abstract void Run(CommandContext context);

        protected void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var log = PluginApp.Log;
            var watch = Stopwatch.StartNew();
            log?.Info("command_start", new Dictionary<string, object?> { ["command"] = Name, ["drawing"] = System.IO.Path.GetFileName(doc.Name) });

            try
            {
                using (doc.LockDocument())
                using (var transaction = doc.Database.TransactionManager.StartTransaction())
                {
                    var context = new CommandContext(doc, transaction, log);
                    Run(context);
                    if (ReadOnly) transaction.Abort(); else transaction.Commit();
                }

                log?.Info("command_end", new Dictionary<string, object?> { ["command"] = Name, ["elapsedMs"] = watch.ElapsedMilliseconds, ["ok"] = true });
            }
            catch (System.Exception exception)
            {
                log?.Error("command_failed", exception, new Dictionary<string, object?> { ["command"] = Name, ["elapsedMs"] = watch.ElapsedMilliseconds });
                var where = PluginApp.LogPath != null ? $" Chi tiết trong nhật ký: {PluginApp.LogPath}" : string.Empty;
                doc.Editor.WriteMessage($"\nPiping Agent: lệnh {Name} gặp lỗi và đã dừng an toàn. {exception.GetType().Name}: {exception.Message}.{where}\n");
            }
        }
    }
}
