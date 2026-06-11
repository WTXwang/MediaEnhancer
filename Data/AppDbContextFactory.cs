using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediaEnhancer.Data;

/// <summary>
/// EF Core 设计时工厂——供 dotnet ef migrations 命令行使用。
/// 运行时通过依赖注入提供 DbContextOptions，不存在此问题。
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=MediaEnhancerDb.sqlite");
        return new AppDbContext(optionsBuilder.Options, autoMigrate: false);
    }
}
