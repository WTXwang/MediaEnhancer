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
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 关键：阻止 Application 在 LoginWindow 关闭后自动退出
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // 注册增强方法（ONNX 加载失败时静默跳过，不影响基础功能）
            var registry = ServiceProvider.GetRequiredService<EnhancementRegistry>();
            registry.Register(new LinearStretchMethod()); // 纯 C# 实现，始终可用

            try { registry.Register(new MultinexNanoMethod()); }
            catch (Exception ex) { Debug.WriteLine($"MultinexNano 加载失败: {ex.Message}"); }

            // ─── 确保数据库已迁移 ───
            using (var scope = ServiceProvider.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            }

            // ─── 登录流程 ───
            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            loginWindow.ShowDialog();

            var authService = ServiceProvider.GetRequiredService<IAuthService>();
            if (!loginWindow.LoginSucceeded || authService.CurrentUser == null)
            {
                Shutdown();
                return;
            }

            // 登录/注册成功提示
            System.Windows.MessageBox.Show(
                $"欢迎使用影音智增强管理系统，{authService.CurrentUser.DisplayName}！",
                "登录成功",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Closed += (_, _) => Shutdown();
            mainWindow.Show();

            // 启动后台清理过期缩略图缓存
            var thumbnailService = ServiceProvider.GetRequiredService<IThumbnailService>();
            _ = Task.Run(() => thumbnailService.CleanupOrphanedThumbnailsAsync());
        }

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

            // 注册应用服务
            services.AddScoped<IDataService, DataService>();
            services.AddScoped<IFileScanService, FileScanService>();
            services.AddScoped<IPlaybackService, PlaybackService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();

            // 注册 ViewModel
            services.AddTransient<MainViewModel>();
            services.AddTransient<LoginViewModel>();

            // 注册 Window
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();
        }
    }
}
