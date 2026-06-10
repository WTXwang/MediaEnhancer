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

            // 注册默认增强方法（新增算法只需在此处加一行 registry.Register(...)）
            var registry = ServiceProvider.GetRequiredService<EnhancementRegistry>();
            registry.Register(new LinearStretchMethod());

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
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
