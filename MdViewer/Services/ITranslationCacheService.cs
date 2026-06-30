namespace MdViewer.Services;

/// <summary>管理 Markdown 翻译缓存。</summary>
public interface ITranslationCacheService
{
    string ComputeHash(string content);

    string? GetCachedTranslation(string filePath, string sourceHash, long lastWriteUtcTicks);

    void SaveTranslation(string filePath, string sourceHash, long lastWriteUtcTicks, string translatedMarkdown);
}
