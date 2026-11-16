using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Agilent66332A.Desktop.Features.Service;
using Agilent66332A.Desktop.Shell;

namespace Agilent66332A.Desktop.Verification;

internal static partial class DesktopVerification
{
    private static async Task VerifyServiceHelpAsync(MainWindow window, ServiceViewModel service)
    {
        double width = window.Width;
        double height = window.Height;
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);

        var tabs = (TabControl)window.FindName("WorkspaceTabs");
        var view = (ServiceView)((TabItem)tabs.SelectedItem).Content;
        var scroll = (ScrollViewer)view.Content;
        var memory = ServiceGuide(view, "Review memory and startup actions");
        var compatibility = ServiceGuide(view, "Review language and Compatibility actions");
        OpenGuideWithAutomation(memory);
        OpenGuideWithAutomation(compatibility);
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        Require(memory.IsExpanded && compatibility.IsExpanded, "Service guidance did not expand before acknowledgment.");

        await VerifyGuideLinkAsync(view, "Read the Keysight calibration guide",
            "https://www.keysight.com/gr/en/assets/9018-01126/user-manuals/9018-01126.pdf#page=54");
        await VerifyGuideLinkAsync(compatibility, "Keysight fault mask bit assignments (Table B-4)",
            "https://www.keysight.com/mt/en/assets/9018-01225/programming-guides/9018-01225.pdf#page=88");

        var lastExplanation = VisualDescendants<TextBlock>(compatibility).Last(text => text.IsVisible);
        lastExplanation.BringIntoView();
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        Rect bounds = lastExplanation.TransformToAncestor(scroll).TransformBounds(new Rect(lastExplanation.RenderSize));
        Require(lastExplanation.TextWrapping == TextWrapping.Wrap && bounds.Height > lastExplanation.FontSize * 2 &&
                scroll.VerticalOffset > 0 && bounds.Top >= -1 && bounds.Bottom <= scroll.ViewportHeight + 1 &&
                bounds.Right <= scroll.ViewportWidth + 1,
            "The end of the Service guide did not wrap and scroll into view at the minimum window size.");
        Require(!service.FixtureIsolated && !service.DestructiveActionConfirmed && !service.CalibrationWiringConfirmed &&
                FindVisibleButton(view, "Run self-test") is { IsEnabled: false } &&
                FindVisibleButton(view, "Recall isolated") is { IsEnabled: false } &&
                FindVisibleButton(view, "Enter Compatibility") is { IsEnabled: false } &&
                FindVisibleButton(view, "Start procedure") is { IsEnabled: false },
            "Reviewing the Service guidance changed an acknowledgment or enabled an action.");

        memory.IsExpanded = compatibility.IsExpanded = false;
        scroll.ScrollToTop();
        window.Width = width;
        window.Height = height;
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
    }

    private static Expander ServiceGuide(ServiceView view, string heading) =>
        VisualDescendants<Expander>(view).Single(guide => Equals(guide.Header, heading));

    private static void OpenGuideWithAutomation(Expander guide)
    {
        var header = (ToggleButton)guide.Template.FindName("HeaderSite", guide);
        Require(header is { IsEnabled: true, Focusable: true, IsTabStop: true }, $"{guide.Header} is unavailable from the keyboard.");
        ((IToggleProvider)new ToggleButtonAutomationPeer(header!).GetPattern(PatternInterface.Toggle)).Toggle();
    }

    private static async Task VerifyGuideLinkAsync(DependencyObject root, string label, string destination)
    {
        var link = VisualDescendants<TextBlock>(root).Where(text => text.IsVisible)
            .SelectMany(text => text.Inlines.OfType<Hyperlink>())
            .Single(candidate => candidate.Inlines.OfType<Run>().Single().Text == label);
        Require(link is { IsEnabled: true, Focusable: true } && KeyboardNavigation.GetIsTabStop(link),
            $"{label} is unavailable from the keyboard.");

        // Verify that activating the WPF link requests the right page without launching a browser during the smoke run.
        Uri? requested = null;
        void RecordNavigation(object sender, RequestNavigateEventArgs args)
        {
            requested = args.Uri;
            args.Handled = true;
        }
        link.RequestNavigate += RecordNavigation;
        try
        {
            ((IInvokeProvider)new HyperlinkAutomationPeer(link).GetPattern(PatternInterface.Invoke)).Invoke();
            await link.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(requested?.AbsoluteUri == destination, $"{label} did not request the expected Keysight page.");
        }
        finally
        {
            link.RequestNavigate -= RecordNavigation;
        }
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in VisualDescendants<T>(child)) yield return descendant;
        }
    }
}
