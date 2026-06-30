namespace MdViewer.Services;

/// <summary>文件相关操作：选择文件、读取内容、定位文件和复制路径。</summary>
public interface IFileService
{
    /// <summary>弹出打开文件对话框，返回所选 .md 文件路径；取消返回 null。</summary>
    string? PickMarkdownFile();

    /// <summary>弹出选择文件夹对话框，返回所选文件夹路径；取消返回 null。</summary>
    string? PickFolder();

    /// <summary>读取文件全部文本，并自动识别 UTF-8、UTF-16、GB18030 等常见编码。</summary>
    string ReadAllText(string filePath);

    /// <summary>文件是否存在。</summary>
    bool Exists(string filePath);

    /// <summary>在资源管理器中定位该文件。</summary>
    void OpenInExplorer(string filePath);

    /// <summary>把文本复制到剪贴板。</summary>
    void CopyToClipboard(string text);
}
