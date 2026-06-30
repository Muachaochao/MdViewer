namespace MdViewer.Services;

/// <summary>监听当前正在查看的文件，外部修改时通知重新渲染。</summary>
public interface IFileWatcherService
{
    /// <summary>开始监听指定文件；传 null 停止监听。</summary>
    void Watch(string? filePath);

    /// <summary>文件被外部修改时触发，参数为文件路径。已做去抖。</summary>
    event Action<string>? FileChanged;
}
