using System.Windows;
using System.Windows.Controls;
using MdViewer.Events;
using Prism.Events;

namespace MdViewer.Views;

/// <summary>应用主窗口外壳。</summary>
public partial class MainWindow : Window
{
    private const double DefaultSidebarWidth = 280;
    private double _sidebarWidth = DefaultSidebarWidth;
    private bool _sidebarVisible = true;
    private readonly IEventAggregator _events;
    private readonly SubscriptionToken _resetSubscription;

    public MainWindow(IEventAggregator events)
    {
        InitializeComponent();
        _events = events;
        _resetSubscription = events.GetEvent<ResetLayoutEvent>().Subscribe(ResetSidebar, ThreadOption.UIThread);
        Closed += (_, _) => _events.GetEvent<ResetLayoutEvent>().Unsubscribe(_resetSubscription);
    }

    private void ToggleSidebar(object sender, RoutedEventArgs e)
    {
        if (_sidebarVisible)
            _sidebarWidth = SidebarColumn.ActualWidth;

        SetSidebarVisible(!_sidebarVisible);
    }

    private void ResetSidebar()
    {
        _sidebarWidth = DefaultSidebarWidth;
        SetSidebarVisible(true);
    }

    private void SetSidebarVisible(bool visible)
    {
        _sidebarVisible = visible;
        // Keep the Prism region alive so selection and scroll state survive collapse.
        SidebarHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.MinWidth = visible ? 210 : 0;
        SidebarColumn.Width = new GridLength(visible ? _sidebarWidth : 0);
        SidebarSplitterColumn.Width = new GridLength(visible ? 5 : 0);
        var label = visible ? "收起侧栏" : "展开侧栏";
        SidebarToggleButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(SidebarToggleButton, label);
    }

    private void OpenButtonContextMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;

        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
