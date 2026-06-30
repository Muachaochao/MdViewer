using System.Windows;
using MdViewer.Services;
using MdViewer.ViewModels;
using MdViewer.Views;
using Prism.DryIoc;
using Prism.Ioc;

namespace MdViewer;

/// <summary>Prism 应用引导：注册服务与视图，创建主窗口。</summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
        => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDbContext, DbContext>();
        containerRegistry.RegisterSingleton<IMarkdownRenderer, MarkdownRenderer>();
        containerRegistry.RegisterSingleton<IFileService, FileService>();
        containerRegistry.RegisterSingleton<IRecentFileService, RecentFileService>();
        containerRegistry.RegisterSingleton<IFileWatcherService, FileWatcherService>();
        containerRegistry.RegisterSingleton<ITranslationService, PublicTranslationService>();
        containerRegistry.RegisterSingleton<ITranslationCacheService, TranslationCacheService>();

        containerRegistry.RegisterForNavigation<SidebarView, SidebarViewModel>();
        containerRegistry.RegisterForNavigation<MarkdownView, MarkdownViewModel>();
    }
}
