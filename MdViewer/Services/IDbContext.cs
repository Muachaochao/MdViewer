using SqlSugar;

namespace MdViewer.Services;

/// <summary>对外暴露 SqlSugar 客户端,集中管理数据库连接与建表。</summary>
public interface IDbContext
{
    ISqlSugarClient Db { get; }
}
