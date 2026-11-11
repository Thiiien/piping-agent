using Autodesk.AutoCAD.Runtime;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>PA_RIBBON: hiện lại thẻ ribbon Piping Agent nếu bị mất, ví dụ sau khi đổi workspace hoặc tắt ribbon rồi bật lại.</summary>
    public sealed class RibbonCommand : CommandBase
    {
        protected override string Name => "PA_RIBBON";

        [CommandMethod("PA_RIBBON")]
        public void Ribbon() => Execute();

        protected override void Run(CommandContext context)
        {
            if (!PanelHost.IsGuiAvailable())
            {
                context.Write("Piping Agent: không có giao diện, không có ribbon.");
                return;
            }

            Show();
            context.Write("Piping Agent: thẻ ribbon Piping Agent đã sẵn sàng. Nếu chưa thấy, gõ RIBBON để bật ribbon.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void Show()
        {
            AutoCAD.Ribbon.PipingRibbon.Install();
            var ribbon = Autodesk.Windows.ComponentManager.Ribbon;
            var tab = ribbon?.FindTab(AutoCAD.Ribbon.PipingRibbon.TabId);
            if (tab != null) tab.IsActive = true;
        }
    }
}
