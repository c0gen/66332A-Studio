using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Agilent66332A.Desktop.Controls;

public sealed class WorkspaceTabControl : TabControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new WorkspacePeer(this);

    // The navigation list exposes selection. The workspace exposes the selected view as a pane.
    private sealed class WorkspacePeer(WorkspaceTabControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(WorkspaceTabControl);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    }
}
