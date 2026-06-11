using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MediaEnhancer.Core;
using MediaEnhancer.Data;
using MediaEnhancer.Services;
using MediaEnhancer.ViewModels;
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
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // 注册增强方法
            // - Register：实时增强可用（SupportsRealTime = true）
            // - RegisterOffline：仅离线增强（SupportsRealTime = false）
            var registry = ServiceProvider.GetRequiredService<EnhancementRegistry>();
            registry.Register(new LinearStretchMethod());
            registry.Register(new MultinexNanoMethod());
            registry.RegisterOffline(new MultinexMethod());
            registry.RegisterOffline(new ZeroDceMethod());

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // 启动后台清理过期缩略图缓存（30 天未访问的）
            var thumbnailService = ServiceProvider.GetRequiredService<IThumbnailService>();
            _ = Task.Run(() => thumbnailService.CleanupOrphanedThumbnailsAsync());
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 配置 SQLite 数据库环境
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=MediaEnhancerDb.sqlite"));

            // 注册增强方法注册中心（单例，全局共享）
            services.AddSingleton<EnhancementRegistry>();
            services.AddSingleton<AiService>();

            // 注册应用服务
            services.AddScoped<IDataService, DataService>();
            services.AddScoped<IFileScanService, FileScanService>();
            services.AddScoped<IPlaybackService, PlaybackService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();

            // 注册 ViewModel
            services.AddTransient<MainViewModel>();

            // 注册 Window
            services.AddTransient<MainWindow>();
        }
    }
}
