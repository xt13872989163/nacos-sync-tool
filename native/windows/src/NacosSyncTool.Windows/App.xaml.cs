using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace NacosSyncTool.Windows;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 注册服务
        services.AddSingleton<Services.LogService>();
        services.AddSingleton<Services.NacosApiService>();
        services.AddSingleton<Modules.RabbitMq.Services.RabbitMqLogService>();
        services.AddSingleton<Modules.RabbitMq.Services.RabbitMqSettingsStore>();
        services.AddSingleton<Modules.RabbitMq.Services.RabbitMqPlanBuilder>();
        services.AddSingleton<Modules.RabbitMq.Services.IRabbitMqClientFactory, Modules.RabbitMq.Services.RabbitMqClientFactory>();
        services.AddSingleton<Modules.RabbitMq.Services.IRabbitMqConfirmationService, Modules.RabbitMq.Services.RabbitMqConfirmationService>();

        // 注册 ViewModels
        services.AddSingleton<ViewModels.MainViewModel>();
        services.AddSingleton<Modules.RabbitMq.ViewModels.RabbitMqModuleViewModel>();
        services.AddSingleton<ViewModels.ShellViewModel>();
    }

    public static void SwitchTheme(string themeName)
    {
        var resources = Current.Resources.MergedDictionaries;
        var themeDict = resources.FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/") == true
                                                      && !d.Source.OriginalString.Contains("Shared"));

        if (themeDict != null)
        {
            resources.Remove(themeDict);
        }

        var newTheme = new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
        };
        resources.Add(newTheme);
    }
}
