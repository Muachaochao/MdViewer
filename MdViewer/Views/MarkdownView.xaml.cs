using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using MdViewer.Events;
using MdViewer.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Prism.Events;

namespace MdViewer.Views;

/// <summary>Markdown 渲染视图，内嵌 WebView2 显示 HTML。</summary>
public partial class MarkdownView : UserControl
{
    private const string VirtualHost = "mdviewer.assets";
    private const int NavigateToStringLimit = 1_500_000;

    private bool _webViewReady;
    private string? _pendingHtml;
    private string _mappedDirectory = string.Empty;
    private CoreWebView2Environment? _environment;

    public MarkdownView(MarkdownViewModel viewModel, IEventAggregator events)
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        DataContext = viewModel;
        events.GetEvent<PrintDocumentEvent>().Subscribe(PrintCurrentDocument, ThreadOption.UIThread);
        events.GetEvent<ExportPdfEvent>().Subscribe(async () => await ExportCurrentDocumentToPdfAsync(), ThreadOption.UIThread);
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_webViewReady)
            return;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MdViewer", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            ApplyCleanPrintPreviewDefaults(userDataFolder);

            _environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await Browser.EnsureCoreWebView2Async(_environment);

            _webViewReady = true;
            if (_pendingHtml is not null)
            {
                Navigate(_pendingHtml);
                _pendingHtml = null;
            }
            else if (DataContext is MarkdownViewModel vm && !string.IsNullOrEmpty(vm.Html))
            {
                Navigate(vm.Html);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "WebView2 初始化失败，请确认已安装 WebView2 Runtime。\n\n" + ex.Message,
                "渲染组件错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is MarkdownViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            if (!string.IsNullOrEmpty(vm.Html))
                Render(vm.Html);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkdownViewModel.Html) && sender is MarkdownViewModel vm)
            Render(vm.Html);
    }

    private void Render(string html)
    {
        if (string.IsNullOrEmpty(html))
            return;

        if (_webViewReady)
            Navigate(html);
        else
            _pendingHtml = html;
    }

    private void UpdateHostMapping()
    {
        if (!_webViewReady || DataContext is not MarkdownViewModel vm)
            return;

        var dir = vm.BaseDirectory;
        if (string.Equals(dir, _mappedDirectory, StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrEmpty(_mappedDirectory))
        {
            try { Browser.CoreWebView2.ClearVirtualHostNameToFolderMapping(VirtualHost); }
            catch { }
        }

        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHost,
                dir,
                CoreWebView2HostResourceAccessKind.Allow);
            _mappedDirectory = dir;
        }
        else
        {
            _mappedDirectory = string.Empty;
        }
    }

    private void Navigate(string html)
    {
        UpdateHostMapping();

        if (html.Length < NavigateToStringLimit)
        {
            Browser.CoreWebView2.NavigateToString(html);
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "MdViewer");
        Directory.CreateDirectory(tempDir);
        var htmlPath = Path.Combine(tempDir, "preview-" + Guid.NewGuid().ToString("N") + ".html");
        File.WriteAllText(htmlPath, html, new UTF8Encoding(false));
        Browser.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
    }

    private void PrintCurrentDocument()
    {
        if (!_webViewReady)
            return;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MdViewer", "WebView2");
            ApplyCleanPrintPreviewDefaults(userDataFolder);
            Browser.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "打开打印窗口失败。\n\n" + ex.Message,
                "打印",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private static void ApplyCleanPrintPreviewDefaults(string userDataFolder)
    {
        try
        {
            var profileDir = Path.Combine(userDataFolder, "EBWebView", "Default");
            Directory.CreateDirectory(profileDir);

            var preferencesPath = Path.Combine(profileDir, "Preferences");
            JsonObject preferences;
            if (File.Exists(preferencesPath))
            {
                var json = File.ReadAllText(preferencesPath, Encoding.UTF8);
                preferences = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            else
            {
                preferences = new JsonObject();
            }

            var printing = preferences["printing"] as JsonObject;
            if (printing is null)
            {
                printing = new JsonObject();
                preferences["printing"] = printing;
            }

            var stickySettings = printing["print_preview_sticky_settings"] as JsonObject;
            if (stickySettings is null)
            {
                stickySettings = new JsonObject();
                printing["print_preview_sticky_settings"] = stickySettings;
            }

            JsonObject appState;
            var appStateText = stickySettings["appState"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(appStateText))
            {
                appState = JsonNode.Parse(appStateText) as JsonObject ?? new JsonObject();
            }
            else
            {
                appState = new JsonObject();
            }

            appState["version"] = 2;
            appState["isHeaderFooterEnabled"] = false;
            appState["isCssBackgroundEnabled"] = true;
            appState["marginsType"] ??= 0;

            stickySettings["appState"] = appState.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false
            });

            File.WriteAllText(
                preferencesPath,
                preferences.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
                new UTF8Encoding(false));
        }
        catch
        {
            // 打印预览偏好写入失败时，不影响主流程；导出 PDF 仍会强制关闭页眉页脚。
        }
    }

    private async Task ExportCurrentDocumentToPdfAsync()
    {
        if (!_webViewReady)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "导出 PDF",
            Filter = "PDF 文件 (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = "markdown-preview.pdf",
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var success = await ExportPdfWithDocumentOutlineAsync(dialog.FileName);
            var exportedWithOutline = success;
            if (!success)
                success = await Browser.CoreWebView2.PrintToPdfAsync(dialog.FileName, CreateCleanPrintSettings());

            if (success)
            {
                var message = exportedWithOutline
                    ? "PDF 导出完成。\n\n如果 PDF 阅读器支持目录视图，左侧列表会显示文档标题层级；缩略图视图由 PDF 阅读器自动生成。"
                    : "PDF 导出完成，但当前 WebView2 Runtime 不支持生成 PDF 目录书签，本次使用了兼容导出。\n\n缩略图视图仍由 PDF 阅读器自动生成，但左侧目录列表可能不会出现。";

                System.Windows.MessageBox.Show(
                    message,
                    "导出 PDF",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "PDF 导出失败，请稍后再试。",
                    "导出 PDF",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "PDF 导出失败。\n\n" + ex.Message,
                "导出 PDF",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private async Task<bool> ExportPdfWithDocumentOutlineAsync(string fileName)
    {
        try
        {
            await WaitForPageReadyAsync();

            var parameters = """
                {
                  "displayHeaderFooter": false,
                  "printBackground": true,
                  "preferCSSPageSize": true,
                  "marginTop": 0.63,
                  "marginBottom": 0.63,
                  "marginLeft": 0.63,
                  "marginRight": 0.63,
                  "generateTaggedPDF": true,
                  "generateDocumentOutline": true
                }
                """;

            var json = await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.printToPDF", parameters);
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("data", out var dataElement))
                return false;

            var data = dataElement.GetString();
            if (string.IsNullOrWhiteSpace(data))
                return false;

            var bytes = Convert.FromBase64String(data);
            await File.WriteAllBytesAsync(fileName, bytes);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private CoreWebView2PrintSettings CreateCleanPrintSettings()
    {
        if (_environment is null)
            throw new InvalidOperationException("WebView2 打印环境未初始化。");

        var settings = _environment.CreatePrintSettings();
        settings.ShouldPrintHeaderAndFooter = false;
        settings.HeaderTitle = string.Empty;
        settings.FooterUri = string.Empty;
        settings.ShouldPrintBackgrounds = true;
        settings.ShouldPrintSelectionOnly = false;
        settings.Orientation = CoreWebView2PrintOrientation.Portrait;
        settings.PageWidth = 8.27;
        settings.PageHeight = 11.69;
        settings.MarginTop = 0.63;
        settings.MarginBottom = 0.63;
        settings.MarginLeft = 0.63;
        settings.MarginRight = 0.63;
        return settings;
    }

    private async Task WaitForPageReadyAsync()
    {
        try
        {
            await Browser.ExecuteScriptAsync("""
                new Promise(resolve => {
                  const finish = () => {
                    const images = Array.from(document.images || []);
                    const imageTasks = images
                      .filter(img => !img.complete)
                      .map(img => new Promise(done => {
                        img.addEventListener('load', done, { once: true });
                        img.addEventListener('error', done, { once: true });
                      }));

                    const fontTask = document.fonts && document.fonts.ready
                      ? document.fonts.ready.catch(() => {})
                      : Promise.resolve();

                    Promise.race([
                      Promise.all([fontTask, Promise.all(imageTasks)]),
                      new Promise(done => setTimeout(done, 3000))
                    ]).then(() => resolve(true));
                  };

                  if (document.readyState === 'complete') {
                    finish();
                  } else {
                    window.addEventListener('load', finish, { once: true });
                  }
                })
                """);
        }
        catch
        {
            await Task.Delay(300);
        }
    }
}
