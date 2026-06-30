using System.Windows.Controls;
using MdViewer.ViewModels;

namespace MdViewer.Views;

/// <summary>侧边栏:收藏与最近打开列表。</summary>
public partial class SidebarView : UserControl
{
    public SidebarView(SidebarViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel; // 显式注入 VM
    }
}
