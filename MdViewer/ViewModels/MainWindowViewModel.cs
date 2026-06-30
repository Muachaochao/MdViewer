using System.IO;
using System.Windows;
using System.Windows.Media;
using MdViewer.Events;
using MdViewer.Services;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace MdViewer.ViewModels;

public enum AppThemeMode
{
    Light,
    Dark,
    System,
}

public class MainWindowViewModel : BindableBase
{
    private readonly IFileService _fileService;
    private readonly IRecentFileService _recentService;
    private readonly IEventAggregator _events;

    private double _fontScale = 1.0;
    private double _lineHeight = 1.6;

    private string _title = "Markdown 查看器";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    private string _currentFileName = "未打开文件";
    public string CurrentFileName { get => _currentFileName; set => SetProperty(ref _currentFileName, value); }

    private string? _currentFilePath;

    private AppThemeMode _themeMode = AppThemeMode.Light;
    public AppThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (SetProperty(ref _themeMode, value))
            {
                RaisePropertyChanged(nameof(ThemeButtonText));
                RaisePropertyChanged(nameof(IsLightThemeSelected));
                RaisePropertyChanged(nameof(IsDarkThemeSelected));
                RaisePropertyChanged(nameof(IsSystemThemeSelected));
                ApplyCurrentTheme();
            }
        }
    }

    public string ThemeButtonText => ThemeMode switch
    {
        AppThemeMode.Dark => "主题: 深色",
        AppThemeMode.System => "主题: 跟随",
        _ => "主题: 浅色",
    };

    public bool IsLightThemeSelected => ThemeMode == AppThemeMode.Light;
    public bool IsDarkThemeSelected => ThemeMode == AppThemeMode.Dark;
    public bool IsSystemThemeSelected => ThemeMode == AppThemeMode.System;

    private string _translateButtonText = "译中文";
    public string TranslateButtonText
    {
        get => _translateButtonText;
        private set => SetProperty(ref _translateButtonText, value);
    }

    private string _bilingualButtonText = "双语对照";
    public string BilingualButtonText
    {
        get => _bilingualButtonText;
        private set => SetProperty(ref _bilingualButtonText, value);
    }

    public string LineHeightText => $"行距 {_lineHeight:0.0}";
    public bool IsCompactLineHeightSelected => Math.Abs(_lineHeight - 1.4) < 0.01;
    public bool IsStandardLineHeightSelected => Math.Abs(_lineHeight - 1.6) < 0.01;
    public bool IsRelaxedLineHeightSelected => Math.Abs(_lineHeight - 1.8) < 0.01;

    public DelegateCommand OpenFileCommand { get; }
    public DelegateCommand ToggleFavoriteCommand { get; }
    public DelegateCommand ToggleThemeCommand { get; }
    public DelegateCommand UseLightThemeCommand { get; }
    public DelegateCommand UseDarkThemeCommand { get; }
    public DelegateCommand UseSystemThemeCommand { get; }
    public DelegateCommand TranslateCommand { get; }
    public DelegateCommand BilingualCommand { get; }
    public DelegateCommand IncreaseFontCommand { get; }
    public DelegateCommand DecreaseFontCommand { get; }
    public DelegateCommand ToggleLineHeightCommand { get; }
    public DelegateCommand UseCompactLineHeightCommand { get; }
    public DelegateCommand UseStandardLineHeightCommand { get; }
    public DelegateCommand UseRelaxedLineHeightCommand { get; }
    public DelegateCommand ResetLayoutCommand { get; }
    public DelegateCommand PrintCommand { get; }
    public DelegateCommand ExportPdfCommand { get; }

    public MainWindowViewModel(
        IRegionManager regionManager,
        IFileService fileService,
        IRecentFileService recentService,
        IEventAggregator events)
    {
        _fileService = fileService;
        _recentService = recentService;
        _events = events;

        OpenFileCommand = new DelegateCommand(OpenFile);
        ToggleFavoriteCommand = new DelegateCommand(ToggleFavorite, () => _currentFilePath is not null)
            .ObservesProperty(() => CurrentFileName);
        ToggleThemeCommand = new DelegateCommand(() =>
            ThemeMode = ResolveCurrentDarkTheme() ? AppThemeMode.Light : AppThemeMode.Dark);
        UseLightThemeCommand = new DelegateCommand(() => ThemeMode = AppThemeMode.Light);
        UseDarkThemeCommand = new DelegateCommand(() => ThemeMode = AppThemeMode.Dark);
        UseSystemThemeCommand = new DelegateCommand(() => ThemeMode = AppThemeMode.System);
        TranslateCommand = new DelegateCommand(
            () => _events.GetEvent<TranslateRequestEvent>().Publish(),
            () => _currentFilePath is not null).ObservesProperty(() => CurrentFileName);
        BilingualCommand = new DelegateCommand(
            () => _events.GetEvent<BilingualRequestEvent>().Publish(),
            () => _currentFilePath is not null).ObservesProperty(() => CurrentFileName);
        IncreaseFontCommand = new DelegateCommand(() => ChangeFontScale(0.1));
        DecreaseFontCommand = new DelegateCommand(() => ChangeFontScale(-0.1));
        ToggleLineHeightCommand = new DelegateCommand(ToggleLineHeight);
        UseCompactLineHeightCommand = new DelegateCommand(() => SetLineHeight(1.4));
        UseStandardLineHeightCommand = new DelegateCommand(() => SetLineHeight(1.6));
        UseRelaxedLineHeightCommand = new DelegateCommand(() => SetLineHeight(1.8));
        ResetLayoutCommand = new DelegateCommand(ResetLayout);
        PrintCommand = new DelegateCommand(
            () => _events.GetEvent<PrintDocumentEvent>().Publish(),
            () => _currentFilePath is not null).ObservesProperty(() => CurrentFileName);
        ExportPdfCommand = new DelegateCommand(
            () => _events.GetEvent<ExportPdfEvent>().Publish(),
            () => _currentFilePath is not null).ObservesProperty(() => CurrentFileName);

        regionManager.RegisterViewWithRegion<Views.SidebarView>("SidebarRegion");
        regionManager.RegisterViewWithRegion<Views.MarkdownView>("ContentRegion");

        _events.GetEvent<OpenMarkdownEvent>().Subscribe(OnFileOpened, ThreadOption.UIThread);
        _events.GetEvent<TranslationStateChangedEvent>().Subscribe(OnTranslationStateChanged, ThreadOption.UIThread);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        ApplyCurrentTheme();
        PublishReadingSettings();
        OpenStartupFileIfProvided();
    }

    private void OpenFile()
    {
        var path = _fileService.PickMarkdownFile();
        if (path is null)
            return;

        _events.GetEvent<OpenMarkdownEvent>().Publish(path);
    }

    private void OnFileOpened(string filePath)
    {
        _currentFilePath = filePath;
        CurrentFileName = Path.GetFileName(filePath);
        TranslateButtonText = "译中文";
        BilingualButtonText = "双语对照";
    }

    private void OnTranslationStateChanged(TranslationState state)
    {
        TranslateButtonText = state.IsTranslating
            ? "翻译中..."
            : state.IsTranslated
                ? "查看原文"
                : "译中文";
        BilingualButtonText = state.IsTranslating
            ? "翻译中..."
            : state.IsBilingual
                ? "关闭双语"
                : "双语对照";
    }

    private void ToggleFavorite()
    {
        if (_currentFilePath is null)
            return;

        _recentService.ToggleFavorite(_currentFilePath);
        _events.GetEvent<FileListChangedEvent>().Publish();
    }

    private void ChangeFontScale(double delta)
    {
        _fontScale = Math.Clamp(Math.Round(_fontScale + delta, 1), 0.8, 1.6);
        PublishReadingSettings();
    }

    private void ToggleLineHeight()
    {
        _lineHeight = _lineHeight switch
        {
            < 1.55 => 1.6,
            < 1.75 => 1.8,
            _ => 1.4,
        };
        NotifyLineHeightChanged();
    }

    private void SetLineHeight(double value)
    {
        _lineHeight = value;
        NotifyLineHeightChanged();
    }

    private void NotifyLineHeightChanged()
    {
        RaisePropertyChanged(nameof(LineHeightText));
        RaisePropertyChanged(nameof(IsCompactLineHeightSelected));
        RaisePropertyChanged(nameof(IsStandardLineHeightSelected));
        RaisePropertyChanged(nameof(IsRelaxedLineHeightSelected));
        PublishReadingSettings();
    }

    private void PublishReadingSettings()
    {
        _events.GetEvent<ReadingSettingsChangedEvent>()
            .Publish(new ReadingSettings(_fontScale, _lineHeight));
    }

    private void ResetLayout()
    {
        _fontScale = 1.0;
        _lineHeight = 1.6;
        ThemeMode = AppThemeMode.Light;
        RaisePropertyChanged(nameof(LineHeightText));
        RaisePropertyChanged(nameof(IsCompactLineHeightSelected));
        RaisePropertyChanged(nameof(IsStandardLineHeightSelected));
        RaisePropertyChanged(nameof(IsRelaxedLineHeightSelected));
        PublishReadingSettings();
        _events.GetEvent<ResetLayoutEvent>().Publish();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (ThemeMode != AppThemeMode.System)
            return;

        Application.Current.Dispatcher.BeginInvoke(ApplyCurrentTheme);
    }

    private void ApplyCurrentTheme()
    {
        var dark = ResolveCurrentDarkTheme();
        ApplyShellTheme(dark);
        _events.GetEvent<ThemeChangedEvent>().Publish(dark);
    }

    private bool ResolveCurrentDarkTheme() =>
        ThemeMode == AppThemeMode.Dark
        || (ThemeMode == AppThemeMode.System && IsSystemDarkTheme());

    private static bool IsSystemDarkTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyShellTheme(bool dark)
    {
        if (dark)
        {
            SetBrush("AppBackgroundBrush", "#1C1C1E");
            SetBrush("SurfaceBrush", "#2C2C2E");
            SetBrush("SidebarBrush", "#252528");
            SetBrush("BorderBrushSoft", "#3A3A3C");
            SetBrush("TextPrimaryBrush", "#F5F5F7");
            SetBrush("TextSecondaryBrush", "#A1A1A6");
            SetBrush("AccentBrush", "#0A84FF");
            SetBrush("ContentBackgroundBrush", "#1E1E20");
            SetBrush("ButtonBackgroundBrush", "#2C2C2E");
            SetBrush("ButtonHoverBrush", "#3A3A3C");
            SetBrush("ButtonPressedBrush", "#48484A");
            SetBrush("ButtonBorderBrush", "#48484A");
            SetBrush("InputBackgroundBrush", "#1C1C1E");
            SetBrush("SidebarItemHoverBrush", "#313136");
            SetBrush("SidebarItemPressedBrush", "#3A3A3C");
            SetBrush("TreePanelBrush", "#202124");
            SetBrush("SelectionBrush", "#123A63");
            return;
        }

        SetBrush("AppBackgroundBrush", "#F5F5F7");
        SetBrush("SurfaceBrush", "#FBFBFD");
        SetBrush("SidebarBrush", "#F2F2F7");
        SetBrush("BorderBrushSoft", "#D9D9DE");
        SetBrush("TextPrimaryBrush", "#1D1D1F");
        SetBrush("TextSecondaryBrush", "#6E6E73");
        SetBrush("AccentBrush", "#007AFF");
        SetBrush("ContentBackgroundBrush", "#FFFFFF");
        SetBrush("ButtonBackgroundBrush", "#FFFFFF");
        SetBrush("ButtonHoverBrush", "#F4F4F6");
        SetBrush("ButtonPressedBrush", "#E8E8ED");
        SetBrush("ButtonBorderBrush", "#D7D7DC");
        SetBrush("InputBackgroundBrush", "#FFFFFF");
        SetBrush("SidebarItemHoverBrush", "#E9E9EE");
        SetBrush("SidebarItemPressedBrush", "#DCDCE3");
        SetBrush("TreePanelBrush", "#F8F8FA");
        SetBrush("SelectionBrush", "#DDEBFF");
    }

    private static void SetBrush(string key, string color)
    {
        Application.Current.Resources[key] = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }

    private void OpenStartupFileIfProvided()
    {
        var filePath = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(File.Exists);

        if (string.IsNullOrEmpty(filePath))
            return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _events.GetEvent<OpenMarkdownEvent>().Publish(filePath);
        });
    }
}
