using System.IO;
using System.Windows;
using MdViewer.Events;
using MdViewer.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace MdViewer.ViewModels;

public class MarkdownViewModel : BindableBase
{
    private enum TranslationDisplayMode
    {
        Original,
        Translated,
        Bilingual,
    }

    private readonly IFileService _fileService;
    private readonly IMarkdownRenderer _renderer;
    private readonly IRecentFileService _recentService;
    private readonly IFileWatcherService _watcher;
    private readonly ITranslationService _translation;
    private readonly ITranslationCacheService _translationCache;
    private readonly IEventAggregator _events;

    private string? _currentFilePath;
    private string? _translatedMarkdown;
    private TranslationDisplayMode _translationMode = TranslationDisplayMode.Original;
    private bool _darkTheme;
    private double _fontScale = 1.0;
    private double _lineHeight = 1.6;
    private bool _resetLayoutOnNextRender;

    private string _html = string.Empty;
    public string Html
    {
        get => _html;
        private set
        {
            if (SetProperty(ref _html, value))
                RaisePropertyChanged(nameof(EmptyHintVisibility));
        }
    }

    private string _baseDirectory = string.Empty;
    public string BaseDirectory
    {
        get => _baseDirectory;
        private set => SetProperty(ref _baseDirectory, value);
    }

    public Visibility EmptyHintVisibility =>
        string.IsNullOrEmpty(Html) ? Visibility.Visible : Visibility.Collapsed;

    private bool _isTranslating;
    public bool IsTranslating
    {
        get => _isTranslating;
        private set
        {
            if (SetProperty(ref _isTranslating, value))
            {
                TranslateCommand.RaiseCanExecuteChanged();
                PublishTranslationState();
            }
        }
    }

    public DelegateCommand TranslateCommand { get; }

    public MarkdownViewModel(
        IFileService fileService,
        IMarkdownRenderer renderer,
        IRecentFileService recentService,
        IFileWatcherService watcher,
        ITranslationService translation,
        ITranslationCacheService translationCache,
        IEventAggregator events)
    {
        _fileService = fileService;
        _renderer = renderer;
        _recentService = recentService;
        _watcher = watcher;
        _translation = translation;
        _translationCache = translationCache;
        _events = events;

        TranslateCommand = new DelegateCommand(async () => await ToggleTranslateAsync(),
            () => !IsTranslating && !string.IsNullOrEmpty(_currentFilePath));

        _events.GetEvent<OpenMarkdownEvent>().Subscribe(Load, ThreadOption.UIThread);
        _events.GetEvent<ThemeChangedEvent>().Subscribe(OnThemeChanged, ThreadOption.UIThread);
        _events.GetEvent<ReadingSettingsChangedEvent>().Subscribe(OnReadingSettingsChanged, ThreadOption.UIThread);
        _events.GetEvent<ResetLayoutEvent>().Subscribe(OnResetLayout, ThreadOption.UIThread);
        _events.GetEvent<TranslateRequestEvent>().Subscribe(
            async () => await ToggleTranslateAsync(), ThreadOption.UIThread);
        _events.GetEvent<BilingualRequestEvent>().Subscribe(
            async () => await ToggleBilingualAsync(), ThreadOption.UIThread);
        _watcher.FileChanged += path =>
        {
            Application.Current.Dispatcher.Invoke(() => OnFileChanged(path));
        };
    }

    private void Load(string filePath)
    {
        _currentFilePath = filePath;
        _translatedMarkdown = null;
        _translationMode = TranslationDisplayMode.Original;
        PublishTranslationState();

        BaseDirectory = _fileService.Exists(filePath)
            ? Path.GetDirectoryName(filePath) ?? string.Empty
            : string.Empty;

        _watcher.Watch(filePath);
        RenderCurrent();
        TranslateCommand.RaiseCanExecuteChanged();

        if (_fileService.Exists(filePath))
        {
            _recentService.TrackOpen(filePath);
            _events.GetEvent<FileListChangedEvent>().Publish();
        }
    }

    private void OnFileChanged(string filePath)
    {
        if (!string.Equals(filePath, _currentFilePath, StringComparison.OrdinalIgnoreCase))
            return;

        _translatedMarkdown = null;
        _translationMode = TranslationDisplayMode.Original;
        PublishTranslationState();
        RenderCurrent();
    }

    private void OnThemeChanged(bool dark)
    {
        _darkTheme = dark;
        RenderCurrent();
    }

    private void OnReadingSettingsChanged(ReadingSettings settings)
    {
        _fontScale = settings.FontScale;
        _lineHeight = settings.LineHeight;
        RenderCurrent();
    }

    private void OnResetLayout()
    {
        _darkTheme = false;
        _fontScale = 1.0;
        _lineHeight = 1.6;
        _resetLayoutOnNextRender = true;
        RenderCurrent();
    }

    private async Task ToggleTranslateAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !_fileService.Exists(_currentFilePath))
            return;

        if (_translationMode == TranslationDisplayMode.Translated)
        {
            _translationMode = TranslationDisplayMode.Original;
            PublishTranslationState();
            RenderCurrent();
            return;
        }

        try
        {
            IsTranslating = true;
            await EnsureTranslatedAsync();
            _translationMode = TranslationDisplayMode.Translated;
            PublishTranslationState();
            RenderCurrent();
        }
        catch (Exception ex)
        {
            ShowTranslationError(ex);
        }
        finally
        {
            IsTranslating = false;
        }
    }

    private async Task ToggleBilingualAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !_fileService.Exists(_currentFilePath))
            return;

        if (_translationMode == TranslationDisplayMode.Bilingual)
        {
            _translationMode = TranslationDisplayMode.Original;
            PublishTranslationState();
            RenderCurrent();
            return;
        }

        try
        {
            IsTranslating = true;
            await EnsureTranslatedAsync();
            _translationMode = TranslationDisplayMode.Bilingual;
            PublishTranslationState();
            RenderCurrent();
        }
        catch (Exception ex)
        {
            ShowTranslationError(ex);
        }
        finally
        {
            IsTranslating = false;
        }
    }

    private async Task EnsureTranslatedAsync()
    {
        if (_translatedMarkdown is not null || string.IsNullOrEmpty(_currentFilePath))
            return;

        var source = _fileService.ReadAllText(_currentFilePath);
        var hash = _translationCache.ComputeHash(source);
        var lastWriteTicks = File.GetLastWriteTimeUtc(_currentFilePath).Ticks;
        var cached = _translationCache.GetCachedTranslation(_currentFilePath, hash, lastWriteTicks);

        if (!string.IsNullOrEmpty(cached))
        {
            _translatedMarkdown = cached;
            return;
        }

        _translatedMarkdown = await _translation.TranslateToChineseAsync(source);
        _translationCache.SaveTranslation(_currentFilePath, hash, lastWriteTicks, _translatedMarkdown);
    }

    private void RenderCurrent()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
            return;

        if (!_fileService.Exists(_currentFilePath))
        {
            Html = RenderMarkdown($"# 文件不存在\n\n`{_currentFilePath}`");
            return;
        }

        var source = _fileService.ReadAllText(_currentFilePath);
        var markdown = _translationMode switch
        {
            TranslationDisplayMode.Translated when _translatedMarkdown is not null => _translatedMarkdown,
            TranslationDisplayMode.Bilingual when _translatedMarkdown is not null => BuildBilingualMarkdown(source, _translatedMarkdown),
            _ => source,
        };

        Html = RenderMarkdown(markdown);
    }

    private static string BuildBilingualMarkdown(string sourceMarkdown, string translatedMarkdown)
    {
        var sourceLines = sourceMarkdown.Replace("\r\n", "\n").Split('\n');
        var translatedLines = translatedMarkdown.Replace("\r\n", "\n").Split('\n');
        var count = Math.Max(sourceLines.Length, translatedLines.Length);
        var result = new List<string>(count * 3);
        var inCodeBlock = false;

        for (var i = 0; i < count; i++)
        {
            var source = i < sourceLines.Length ? sourceLines[i] : string.Empty;
            var translated = i < translatedLines.Length ? translatedLines[i] : string.Empty;
            var trimmed = source.TrimStart();

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                inCodeBlock = !inCodeBlock;
                result.Add(source);
                continue;
            }

            if (inCodeBlock || string.IsNullOrWhiteSpace(source) || ShouldKeepSingleLine(source, translated))
            {
                result.Add(source);
                continue;
            }

            result.Add(source);
            result.Add($"> 译文：{translated}");
            result.Add(string.Empty);
        }

        return string.Join('\n', result);
    }

    private static bool ShouldKeepSingleLine(string source, string translated)
    {
        if (string.Equals(source.Trim(), translated.Trim(), StringComparison.Ordinal))
            return true;

        var trimmed = source.TrimStart();
        if (trimmed.StartsWith("![") || trimmed.StartsWith("[") || trimmed.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
            return true;

        return !HasAsciiLetter(source);
    }

    private static bool HasAsciiLetter(string s)
    {
        foreach (var c in s)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                return true;
        }

        return false;
    }

    private string RenderMarkdown(string markdown)
    {
        var html = _renderer.RenderToHtml(markdown, _darkTheme, _fontScale, _lineHeight, _resetLayoutOnNextRender);
        _resetLayoutOnNextRender = false;
        return html;
    }

    private void PublishTranslationState()
    {
        _events.GetEvent<TranslationStateChangedEvent>()
            .Publish(new TranslationState(
                _translationMode == TranslationDisplayMode.Translated,
                IsTranslating,
                _translationMode == TranslationDisplayMode.Bilingual));
    }

    private static void ShowTranslationError(Exception ex)
    {
        MessageBox.Show(
            "翻译失败，请检查网络连接，或稍后再试。\n\n" + ex.Message,
            "翻译",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
