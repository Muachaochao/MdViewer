using System.IO;
using Prism.Mvvm;

namespace MdViewer.ViewModels;

/// <summary>侧边栏列表项:包装文件路径,附带运行期的「是否存在」标志。</summary>
public class FileItemViewModel : BindableBase
{
    public string FilePath { get; }
    public string FileName { get; }

    /// <summary>文件是否仍存在;不存在时列表项变灰并标记「已删除」。</summary>
    public bool Exists { get; }

    public FileItemViewModel(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;
        Exists = File.Exists(filePath);
    }
}
