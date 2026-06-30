using SqlSugar;

namespace MdViewer.Models;

/// <summary>Markdown 翻译缓存，按文件路径和源文 hash 命中。</summary>
[SugarTable("TranslationCache")]
public class TranslationCache
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 1024)]
    public string FilePath { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string SourceHash { get; set; } = string.Empty;

    public long SourceLastWriteUtcTicks { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TranslatedMarkdown { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
