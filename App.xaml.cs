using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MediaEnhancer.Core;
using MediaEnhancer.Data;
using MediaEnhancer.Services;
using MediaEnhancer.ViewModels;
using MediaEnhancer.Views;
using Microsoft.EntityFrameworkCore;

namespace MediaEnhancer
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 关键：阻止 Application 在 LoginWindow 关闭后自动退出
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 让父类startup，启动xaml的资源
            base.OnStartup(e);

            // 注册各类服务
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // 用于获取服务的实例
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // 注册增强方法（ONNX 加载失败时静默跳过，不影响基础功能）
            var registry = ServiceProvider.GetRequiredService<EnhancementRegistry>();
            // 纯 C# 实现，始终可用
            registry.Register(new LinearStretchMethod()); 
            // 尝试注册 ONNX 方法
            try { registry.Register(new MultinexNanoMethod()); }
            catch (Exception ex) { Debug.WriteLine($"MultinexNano 加载失败: {ex.Message}"); }

            // 确保数据库已迁移
            using (var scope = ServiceProvider.CreateScope())
            {
                // 在这个作用域里，DI 容器正常工作
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // ↑ 构造函数触发 → Database.Migrate() 跑完 → 数据库就位
            }// using 结束 → ctx 被释放 → scope 被释放 ， 节省资源

            // ─── 登录流程 ───
            // 从容器里拿出登录窗口
            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            // 用户不登录不往下走
            loginWindow.ShowDialog();
            // 创建认证服务
            var authService = ServiceProvider.GetRequiredService<IAuthService>();
            if (!loginWindow.LoginSucceeded || authService.CurrentUser == null)
            {
                Shutdown();
                return;
            }

            // 登录/注册成功提示
            System.Windows.MessageBox.Show(
                $"欢迎使用影音智增强系统，{authService.CurrentUser.DisplayName}！",
                "登录成功",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            // 触发关闭事件时，关闭软件
            mainWindow.Closed += (_, _) => Shutdown();
            mainWindow.Show();

            // 启动后台清理过期缩略图缓存
            var thumbnailService = ServiceProvider.GetRequiredService<IThumbnailService>();
            _ = Task.Run(() => thumbnailService.CleanupOrphanedThumbnailsAsync());
        }

        // 依赖注入
        private void ConfigureServices(IServiceCollection services)
        {
            // SQLite（使用 DbContextFactory 以支持 AuthService 等独立创建 context）
            var connStr = "Data Source=MediaEnhancerDb.sqlite";
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite(connStr));
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connStr));

            // 注册增强方法注册中心（单例，全局共享）
            services.AddSingleton<EnhancementRegistry>();
            services.AddSingleton<AiService>();

            // 认证服务（单例，保持登录会话）
            services.AddSingleton<IAuthService, AuthService>();

            // 注册应用服务（Scoped，针对每个窗口实例）
            services.AddScoped<IDataService, DataService>();
            services.AddScoped<IFileScanService, FileScanService>();
            services.AddScoped<IPlaybackService, PlaybackService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();

            // 注册 ViewModel（Transient，每次请求新实例）
            services.AddTransient<MainViewModel>();
            services.AddTransient<LoginViewModel>();

            // 注册 Window（Transient，每次请求新实例） - 注意：MainWindow 依赖 MainViewModel，LoginWindow 依赖 LoginViewModel
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();
        }
    }
}
