using System.IO;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MdViewer.Services;

/// <summary>
/// 基于 FileSystemWatcher 监听单个文件。
/// 编辑器保存时常触发多次事件，这里用 300ms 去抖合并成一次通知。
/// </summary>
public class FileWatcherService : IFileWatcherService, IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly Timer _debounce;
    private string? _currentPath;

    public event Action<string>? FileChanged;

    public FileWatcherService()
    {
        _debounce = new Timer(300) { AutoReset = false };
        _debounce.Elapsed += OnDebounceElapsed;
    }

    public void Watch(string? filePath)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _currentPath = filePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        var dir = Path.GetDirectoryName(filePath);
        var name = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(dir))
            return;

        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFsEvent;
        _watcher.Created += OnFsEvent;
        _watcher.Renamed += OnFsEvent;
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceElapsed(object? sender, ElapsedEventArgs e)
    {
        var path = _currentPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            FileChanged?.Invoke(path);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
