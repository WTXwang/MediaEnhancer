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
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // 确保数据库被创建，如果没有则自动创建（供开发使用）
            Database.EnsureCreated();
        }

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
        /// 缩略图记录数据集。
        /// </summary>
        public DbSet<Thumbnail> Thumbnails { get; set; }

        /// <summary>
        /// 配置模型关系与约束。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MediaFile：FilePath 唯一，防止重复导入
            modelBuilder.Entity<MediaFile>()
                .HasIndex(m => m.FilePath)
                .IsUnique();

            // PlayHistory → MediaFile 多对一关系
            modelBuilder.Entity<PlayHistory>()
                .HasOne(p => p.MediaFile)
                .WithMany()
                .HasForeignKey(p => p.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            // EnhancementLog → MediaFile 多对一关系
            modelBuilder.Entity<EnhancementLog>()
                .HasOne(e => e.MediaFile)
                .WithMany()
                .HasForeignKey(e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Recording → MediaFile 一对一关系
            modelBuilder.Entity<Recording>()
                .HasOne(r => r.MediaFile)
                .WithMany()
                .HasForeignKey(r => r.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Favorite → MediaFile 多对一关系
            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.MediaFile)
                .WithMany()
                .HasForeignKey(f => f.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Favorite 中 MediaFileId 唯一，一个文件只能收藏一次
            modelBuilder.Entity<Favorite>()
                .HasIndex(f => f.MediaFileId)
                .IsUnique();

            // Thumbnail → MediaFile 多对一关系
            modelBuilder.Entity<Thumbnail>()
                .HasOne(t => t.MediaFile)
                .WithMany()
                .HasForeignKey(t => t.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Thumbnail 中 MediaFileId 唯一，一个文件只有一个缩略图记录
            modelBuilder.Entity<Thumbnail>()
                .HasIndex(t => t.MediaFileId)
                .IsUnique();
        }
    }
}
