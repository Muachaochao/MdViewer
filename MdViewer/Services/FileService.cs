using System.IO;
using System.Text;
using Microsoft.Win32;

namespace MdViewer.Services;

/// <inheritdoc />
public class FileService : IFileService
{
    static FileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public string? PickMarkdownFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开 Markdown 文件",
            Filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "打开 Markdown 文件夹",
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string ReadAllText(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        if (TryReadWithBom(bytes, out var byBom))
            return byBom;

        try
        {
            var strictUtf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            return strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return Encoding.GetEncoding("GB18030").GetString(bytes);
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }
    }

    private static bool TryReadWithBom(byte[] bytes, out string text)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            return true;
        }

        text = string.Empty;
        return false;
    }

    public bool Exists(string filePath) => File.Exists(filePath);

    public void OpenInExplorer(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
    }

    public void CopyToClipboard(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            System.Windows.Clipboard.SetText(text);
    }
}
