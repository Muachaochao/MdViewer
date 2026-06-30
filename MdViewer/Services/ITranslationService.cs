namespace MdViewer.Services;

/// <summary>把文本翻译成简体中文。</summary>
public interface ITranslationService
{
    /// <summary>将文本翻译为简体中文，源语言自动检测。失败时抛出异常。</summary>
    Task<string> TranslateToChineseAsync(string text, CancellationToken ct = default);
}
