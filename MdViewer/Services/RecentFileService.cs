using System.IO;
using MdViewer.Models;

namespace MdViewer.Services;

/// <inheritdoc />
public class RecentFileService : IRecentFileService
{
    private const int MaxRecent = 20;
    private readonly IDbContext _ctx;

    public RecentFileService(IDbContext ctx) => _ctx = ctx;

    public void TrackOpen(string filePath)
    {
        var db = _ctx.Db;
        var name = Path.GetFileName(filePath);
        var now = DateTime.Now;

        var existing = db.Queryable<RecentFile>().First(x => x.FilePath == filePath);
        if (existing is null)
        {
            db.Insertable(new RecentFile
            {
                FilePath = filePath,
                FileName = name,
                LastOpenedAt = now,
            }).ExecuteCommand();
        }
        else
        {
            existing.LastOpenedAt = now;
            existing.FileName = name;
            db.Updateable(existing).ExecuteCommand();
        }

        // 裁剪:只保留最近 MaxRecent 条。
        var stale = db.Queryable<RecentFile>()
            .OrderBy(x => x.LastOpenedAt, SqlSugar.OrderByType.Desc)
            .Skip(MaxRecent)
            .Take(int.MaxValue)
            .ToList();
        if (stale.Count > 0)
            db.Deleteable(stale).ExecuteCommand();
    }

    public List<RecentFile> GetRecent() =>
        _ctx.Db.Queryable<RecentFile>()
            .OrderBy(x => x.LastOpenedAt, SqlSugar.OrderByType.Desc)
            .ToList();

    public List<FavoriteFile> GetFavorites() =>
        _ctx.Db.Queryable<FavoriteFile>()
            .OrderBy(x => x.AddedAt, SqlSugar.OrderByType.Desc)
            .ToList();

    public bool IsFavorite(string filePath) =>
        _ctx.Db.Queryable<FavoriteFile>().Any(x => x.FilePath == filePath);

    public bool ToggleFavorite(string filePath)
    {
        var db = _ctx.Db;
        var existing = db.Queryable<FavoriteFile>().First(x => x.FilePath == filePath);
        if (existing is not null)
        {
            db.Deleteable(existing).ExecuteCommand();
            return false;
        }

        db.Insertable(new FavoriteFile
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            AddedAt = DateTime.Now,
        }).ExecuteCommand();
        return true;
    }

    public void RemoveRecent(string filePath) =>
        _ctx.Db.Deleteable<RecentFile>().Where(x => x.FilePath == filePath).ExecuteCommand();

    public void RemoveFavorite(string filePath) =>
        _ctx.Db.Deleteable<FavoriteFile>().Where(x => x.FilePath == filePath).ExecuteCommand();

    public void ClearRecent() =>
        _ctx.Db.Deleteable<RecentFile>().Where(_ => true).ExecuteCommand();

    public int ClearMissingRecentFiles()
    {
        var db = _ctx.Db;
        var removed = 0;

        foreach (var item in GetRecent().Where(x => !File.Exists(x.FilePath)).ToList())
            removed += db.Deleteable<RecentFile>().Where(x => x.Id == item.Id).ExecuteCommand();

        return removed;
    }

    public int ClearMissingFavoriteFiles()
    {
        var db = _ctx.Db;
        var removed = 0;

        foreach (var item in GetFavorites().Where(x => !File.Exists(x.FilePath)).ToList())
            removed += db.Deleteable<FavoriteFile>().Where(x => x.Id == item.Id).ExecuteCommand();

        return removed;
    }

    public int ClearMissingFiles()
    {
        return ClearMissingRecentFiles() + ClearMissingFavoriteFiles();
    }
}
