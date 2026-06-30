using SqlSugar;

namespace MdViewer.Models;

/// <summary>最近打开的 Markdown 文件记录。</summary>
[SugarTable("RecentFile")]
public class RecentFile
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>文件完整路径(用于去重)。</summary>
    [SugarColumn(Length = 1024)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>文件名(显示用)。</summary>
    [SugarColumn(Length = 512)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>最后打开时间。</summary>
    public DateTime LastOpenedAt { get; set; }
}
