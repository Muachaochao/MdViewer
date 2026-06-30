using System.IO;
using MdViewer.Models;
using SqlSugar;

namespace MdViewer.Services;

/// <summary>
/// SqlSugar + SQLite 数据库上下文。数据库文件放在
/// %LocalAppData%/MdViewer/mdviewer.db,首次启动时自动用 CodeFirst 建表。
/// </summary>
public class DbContext : IDbContext
{
    public ISqlSugarClient Db { get; }

    public DbContext()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MdViewer");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "mdviewer.db");

        Db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath};",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
        });

        // CodeFirst:不存在则建表,已存在则按需补列。
        Db.CodeFirst.InitTables<RecentFile, FavoriteFile, TranslationCache>();
    }
}
