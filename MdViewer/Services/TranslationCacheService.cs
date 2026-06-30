using System.Security.Cryptography;
using System.Text;
using MdViewer.Models;

namespace MdViewer.Services;

/// <inheritdoc />
public class TranslationCacheService : ITranslationCacheService
{
    private readonly IDbContext _ctx;

    public TranslationCacheService(IDbContext ctx) => _ctx = ctx;

    public string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    public string? GetCachedTranslation(string filePath, string sourceHash, long lastWriteUtcTicks)
    {
        var item = _ctx.Db.Queryable<TranslationCache>()
            .Where(x => x.FilePath == filePath
                     && x.SourceHash == sourceHash
                     && x.SourceLastWriteUtcTicks == lastWriteUtcTicks)
            .OrderBy(x => x.UpdatedAt, SqlSugar.OrderByType.Desc)
            .First();

        return item?.TranslatedMarkdown;
    }

    public void SaveTranslation(string filePath, string sourceHash, long lastWriteUtcTicks, string translatedMarkdown)
    {
        var db = _ctx.Db;
        var now = DateTime.Now;
        var existing = db.Queryable<TranslationCache>()
            .First(x => x.FilePath == filePath && x.SourceHash == sourceHash);

        if (existing is null)
        {
            db.Insertable(new TranslationCache
            {
                FilePath = filePath,
                SourceHash = sourceHash,
                SourceLastWriteUtcTicks = lastWriteUtcTicks,
                TranslatedMarkdown = translatedMarkdown,
                CreatedAt = now,
                UpdatedAt = now,
            }).ExecuteCommand();
            return;
        }

        existing.SourceLastWriteUtcTicks = lastWriteUtcTicks;
        existing.TranslatedMarkdown = translatedMarkdown;
        existing.UpdatedAt = now;
        db.Updateable(existing).ExecuteCommand();
    }
}
