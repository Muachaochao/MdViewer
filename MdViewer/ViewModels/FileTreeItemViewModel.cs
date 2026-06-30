using System.Collections.ObjectModel;
using System.IO;

namespace MdViewer.ViewModels;

public class FileTreeItemViewModel
{
    public FileTreeItemViewModel(string path, bool isDirectory)
    {
        Path = path;
        IsDirectory = isDirectory;
        Name = isDirectory
            ? new DirectoryInfo(path).Name
            : System.IO.Path.GetFileName(path);
    }

    public string Name { get; }
    public string Path { get; }
    public bool IsDirectory { get; }
    public string Icon => IsDirectory ? "📁" : "📄";
    public ObservableCollection<FileTreeItemViewModel> Children { get; } = new();
}
