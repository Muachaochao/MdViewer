using MdViewer.Models;

namespace MdViewer.Services;

/// <summary>管理「最近打开」与「收藏」记录的增删查。</summary>
public interface IRecentFileService
{
    /// <summary>记录一次打开(按路径去重 + 更新时间),并裁剪到上限条数。</summary>
    void TrackOpen(string filePath);

    /// <summary>获取最近打开列表(按时间倒序)。</summary>
    List<RecentFile> GetRecent();

    /// <summary>获取收藏列表(按加入时间倒序)。</summary>
    List<FavoriteFile> GetFavorites();

    /// <summary>该路径是否已收藏。</summary>
    bool IsFavorite(string filePath);

    /// <summary>切换收藏状态;返回切换后的状态(true=已收藏)。</summary>
    bool ToggleFavorite(string filePath);

    /// <summary>从最近列表移除一条。</summary>
    void RemoveRecent(string filePath);

    /// <summary>从收藏移除一条。</summary>
    void RemoveFavorite(string filePath);

    /// <summary>清空所有最近打开记录。</summary>
    void ClearRecent();

    /// <summary>清理最近列表中已不存在的文件记录，返回清理数量。</summary>
    int ClearMissingRecentFiles();

    /// <summary>清理收藏列表中已不存在的文件记录，返回清理数量。</summary>
    int ClearMissingFavoriteFiles();

    /// <summary>清理最近和收藏列表中已不存在的文件记录，返回清理数量。</summary>
    int ClearMissingFiles();
}
