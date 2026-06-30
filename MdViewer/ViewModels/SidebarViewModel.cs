using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using MdViewer.Events;
using MdViewer.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace MdViewer.ViewModels;

public class SidebarViewModel : BindableBase
{
    private readonly IRecentFileService _recentService;
    private readonly IFileService _fileService;
    private readonly IEventAggregator _events;

    private readonly ObservableCollection<FileItemViewModel> _recent = new();
    private readonly ObservableCollection<FileItemViewModel> _favorites = new();

    public ObservableCollection<FileTreeItemViewModel> WorkspaceItems { get; } = new();

    private string _workspaceTitle = "未打开文件夹";
    public string WorkspaceTitle
    {
        get => _workspaceTitle;
        private set => SetProperty(ref _workspaceTitle, value);
    }

    public ICollectionView Recent { get; }
    public ICollectionView Favorites { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Recent.Refresh();
                Favorites.Refresh();
            }
        }
    }

    public DelegateCommand<string> OpenCommand { get; }
    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand<FileItemViewModel> RemoveCommand { get; }
    public DelegateCommand<FileItemViewModel> OpenFolderCommand { get; }
    public DelegateCommand<FileItemViewModel> CopyPathCommand { get; }
    public DelegateCommand<FileItemViewModel> ToggleFavCommand { get; }
    public DelegateCommand ClearRecentCommand { get; }
    public DelegateCommand ClearMissingRecentCommand { get; }
    public DelegateCommand ClearMissingFavoritesCommand { get; }
    public DelegateCommand PickFolderCommand { get; }
    public DelegateCommand ClearWorkspaceCommand { get; }
    public DelegateCommand<FileTreeItemViewModel> OpenTreeItemCommand { get; }

    public SidebarViewModel(
        IRecentFileService recentService,
        IFileService fileService,
        IEventAggregator events)
    {
        _recentService = recentService;
        _fileService = fileService;
        _events = events;

        Recent = CreateFilteredView(_recent);
        Favorites = CreateFilteredView(_favorites);

        OpenCommand = new DelegateCommand<string>(Open);
        RefreshCommand = new DelegateCommand(Reload);
        RemoveCommand = new DelegateCommand<FileItemViewModel>(Remove);
        OpenFolderCommand = new DelegateCommand<FileItemViewModel>(OpenFolder);
        CopyPathCommand = new DelegateCommand<FileItemViewModel>(CopyPath);
        ToggleFavCommand = new DelegateCommand<FileItemViewModel>(ToggleFav);
        ClearRecentCommand = new DelegateCommand(ClearRecent);
        ClearMissingRecentCommand = new DelegateCommand(ClearMissingRecent);
        ClearMissingFavoritesCommand = new DelegateCommand(ClearMissingFavorites);
        PickFolderCommand = new DelegateCommand(PickFolder);
        ClearWorkspaceCommand = new DelegateCommand(ClearWorkspace);
        OpenTreeItemCommand = new DelegateCommand<FileTreeItemViewModel>(OpenTreeItem);

        _events.GetEvent<FileListChangedEvent>().Subscribe(Reload, ThreadOption.UIThread);

        Reload();
    }

    private ICollectionView CreateFilteredView(ObservableCollection<FileItemViewModel> source)
    {
        var view = CollectionViewSource.GetDefaultView(source);
        view.Filter = o =>
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            var item = (FileItemViewModel)o;
            return item.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };
        return view;
    }

    private void Open(string? filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
            _events.GetEvent<OpenMarkdownEvent>().Publish(filePath);
    }

    private void Remove(FileItemViewModel? item)
    {
        if (item is null)
            return;

        _recentService.RemoveRecent(item.FilePath);
        _recentService.RemoveFavorite(item.FilePath);
        Reload();
    }

    private void OpenFolder(FileItemViewModel? item)
    {
        if (item is not null)
            _fileService.OpenInExplorer(item.FilePath);
    }

    private void CopyPath(FileItemViewModel? item)
    {
        if (item is not null)
            _fileService.CopyToClipboard(item.FilePath);
    }

    private void ToggleFav(FileItemViewModel? item)
    {
        if (item is null)
            return;

        _recentService.ToggleFavorite(item.FilePath);
        Reload();
    }

    private void ClearRecent()
    {
        var result = MessageBox.Show(
            "确定要清空所有“最近打开”记录吗？收藏不会受影响。",
            "清空确认",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        _recentService.ClearRecent();
        Reload();
    }

    private void ClearMissingRecent()
    {
        var result = MessageBox.Show(
            "只清理“最近打开”中已删除/失效的文件记录，收藏不会受影响。确定继续吗？",
            "清理最近失效记录",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        var removed = _recentService.ClearMissingRecentFiles();
        Reload();
        MessageBox.Show(
            removed > 0 ? $"已从最近打开清理 {removed} 条失效记录。" : "最近打开里没有发现失效记录。",
            "清理完成",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ClearMissingFavorites()
    {
        var result = MessageBox.Show(
            "只清理“收藏”中已删除/失效的文件记录，最近打开不会受影响。确定继续吗？",
            "清理收藏失效记录",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        var removed = _recentService.ClearMissingFavoriteFiles();
        Reload();
        MessageBox.Show(
            removed > 0 ? $"已从收藏清理 {removed} 条失效记录。" : "收藏里没有发现失效记录。",
            "清理完成",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PickFolder()
    {
        var folder = _fileService.PickFolder();
        if (string.IsNullOrEmpty(folder))
            return;

        WorkspaceTitle = new DirectoryInfo(folder).Name;
        WorkspaceItems.Clear();
        foreach (var item in BuildTree(folder))
            WorkspaceItems.Add(item);
    }

    private void ClearWorkspace()
    {
        WorkspaceTitle = "未打开文件夹";
        WorkspaceItems.Clear();
    }

    private void OpenTreeItem(FileTreeItemViewModel? item)
    {
        if (item is null || item.IsDirectory)
            return;

        _events.GetEvent<OpenMarkdownEvent>().Publish(item.Path);
    }

    private static List<FileTreeItemViewModel> BuildTree(string folder)
    {
        var result = new List<FileTreeItemViewModel>();

        foreach (var dir in SafeEnumerateDirectories(folder).OrderBy(x => x))
        {
            var dirNode = new FileTreeItemViewModel(dir, isDirectory: true);
            foreach (var child in BuildTree(dir))
                dirNode.Children.Add(child);

            if (dirNode.Children.Count > 0)
                result.Add(dirNode);
        }

        foreach (var file in SafeEnumerateMarkdownFiles(folder).OrderBy(x => x))
            result.Add(new FileTreeItemViewModel(file, isDirectory: false));

        return result;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string folder)
    {
        try
        {
            return Directory.EnumerateDirectories(folder);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateMarkdownFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder)
                .Where(file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                            || file.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return [];
        }
    }

    private void Reload()
    {
        _recent.Clear();
        foreach (var item in _recentService.GetRecent())
            _recent.Add(new FileItemViewModel(item.FilePath, item.FileName));

        _favorites.Clear();
        foreach (var item in _recentService.GetFavorites())
            _favorites.Add(new FileItemViewModel(item.FilePath, item.FileName));
    }
}
