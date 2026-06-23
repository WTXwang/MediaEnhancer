using MediaEnhancer.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaEnhancer.Data
{
    /// <summary>
    /// 应用程序数据库上下文，用于和 SQLite 数据库进行交互。
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// 提供给外部注入数据库连接配置的构造函数。
        /// </summary>
        private readonly bool _autoMigrate;

        /// <summary>
        /// 提供给外部注入数据库连接配置的构造函数。
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options, bool autoMigrate = true) : base(options)
        {
            _autoMigrate = autoMigrate;
            if (!autoMigrate) return;

            // 优先使用 EF Core 迁移（支持增量 schema 变更）。
            // 如果迁移文件尚未创建（如首次运行），回退到 EnsureCreated。
            // 如果数据库已由 EnsureCreated 创建，则补录迁移历史使后续 Migrate() 正常工作。
            try
            {
                Database.Migrate();
            }
            catch (InvalidOperationException)
            {
                Database.EnsureCreated();
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("already exists"))
            {
                // EnsureCreated 遗留数据库：表已存在但无迁移历史。
                // 补录 InitialCreate 迁移记录，使后续 Migrate() 能正常增量更新。
                try
                {
                    Database.ExecuteSqlRaw(
                        "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                        "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY," +
                        "\"ProductVersion\" TEXT NOT NULL);");

                    Database.ExecuteSqlRaw(
                        "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                        "VALUES ('20260610125846_InitialCreate', '10.0.0');");
                }
                catch { }
            }
        }

        /// <summary>
        /// 用户数据集。
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// 媒体文件数据集。
        /// </summary>
        public DbSet<MediaFile> MediaFiles { get; set; }

        /// <summary>
        /// 播放记录数据集。
        /// </summary>
        public DbSet<PlayHistory> PlayHistories { get; set; }

        /// <summary>
        /// 增强日志数据集。
        /// </summary>
        public DbSet<EnhancementLog> EnhancementLogs { get; set; }

        /// <summary>
        /// 录屏记录数据集。
        /// </summary>
        public DbSet<Recording> Recordings { get; set; }

        /// <summary>
        /// 收藏记录数据集。
        /// </summary>
        public DbSet<Favorite> Favorites { get; set; }

        /// <summary>
        /// 实时增强会话记录数据集。
        /// </summary>
        public DbSet<RealtimeSession> RealtimeSessions { get; set; }

        /// <summary>
        /// 配置模型关系与约束。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // MediaFile：FilePath 唯一，防止重复导入
            modelBuilder.Entity<MediaFile>()
                .HasIndex(m => m.FilePath)          // 唯一索引
                .IsUnique();                        // 唯一

            // MediaFile 自引用：增强文件 → 源文件（SourceFileId 可为 null）
            modelBuilder.Entity<MediaFile>()
                .HasOne(m => m.SourceFile)          // 对应一个外部文件
                .WithMany()                         // 多对一
                .HasForeignKey(m => m.SourceFileId) // 外键
                .OnDelete(DeleteBehavior.SetNull);  // 来源删除时置空，保留增强文件

            // MediaFile -> PlayHistory 一对多关系
            modelBuilder.Entity<PlayHistory>()
                .HasOne(p => p.MediaFile)           // 对应一个媒体文件
                .WithMany()                         // 一对多 
                .HasForeignKey(p => p.MediaFileId)  // 外键
                .OnDelete(DeleteBehavior.Cascade);  // 级联删除

            // MediaFile -> EnhancementLog 一对多关系
            modelBuilder.Entity<EnhancementLog>()
                .HasOne(e => e.MediaFile)           // 对应一个媒体文件
                .WithMany()                         // 一对多
                .HasForeignKey(e => e.MediaFileId)  // 外键
                .OnDelete(DeleteBehavior.Cascade);  // 级联删除

            // MediaFile -> Recording 一对一关系
            modelBuilder.Entity<Recording>()
                .HasOne(r => r.MediaFile)                       // 对应一个媒体文件
                .WithOne()                                      // 一对一
                .HasForeignKey<Recording>(r => r.MediaFileId)   // 外键
                .OnDelete(DeleteBehavior.Cascade);              // 级联删除

            // Recoding 中 MediaFileId 唯一
            modelBuilder.Entity<Recording>()
                .HasIndex(r => r.MediaFileId)
                .IsUnique();
            
            // MediaFile -> Favorite 一对一关系
            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.MediaFile)                   // 对应一个媒体文件
                .WithOne()                                  // 一对一
                .HasForeignKey<Favorite>(f => f.MediaFileId)// 外键
                .OnDelete(DeleteBehavior.Cascade);          // 级联删除

            // Favorite 中 MediaFileId 唯一，一个文件只能收藏一次
            modelBuilder.Entity<Favorite>()
                .HasIndex(f => f.MediaFileId)
                .IsUnique();

            // ─── User 配置 ───

            // Username 唯一
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // User → MediaFile：一对多
            modelBuilder.Entity<MediaFile>()
                .HasOne(m => m.User)
                .WithMany()                         // 一对一
                .HasForeignKey(m => m.UserId)       // 外键
                .OnDelete(DeleteBehavior.Cascade);  // 级联删除用户记录

            // 各关联表加 UserId 索引（加速按用户过滤）
            modelBuilder.Entity<MediaFile>()
                .HasIndex(m => m.UserId);
            modelBuilder.Entity<PlayHistory>()
                .HasIndex(p => p.UserId);
            modelBuilder.Entity<EnhancementLog>()
                .HasIndex(e => e.UserId);
            modelBuilder.Entity<Recording>()
                .HasIndex(r => r.UserId);
            modelBuilder.Entity<Favorite>()
                .HasIndex(f => f.UserId);

            // User -> RealtimeSession
            modelBuilder.Entity<RealtimeSession>()
                .HasOne(r => r.User)                
                .WithMany()                         // 一对多
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);  // 级联删除
            modelBuilder.Entity<RealtimeSession>()
                .HasIndex(r => r.UserId);      
        }
    }
}
