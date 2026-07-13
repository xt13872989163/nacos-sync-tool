using System.Runtime.CompilerServices;
using NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;
using NacosSyncTool.Windows.Services;
using NacosSyncTool.Windows.ViewModels;
using Xunit;

namespace NacosSyncTool.Windows.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void DefaultsToNacosAndRetainsModuleInstances()
    {
        var nacos = new MainViewModel(new LogService());
        var rabbit = (RabbitMqModuleViewModel)RuntimeHelpers.GetUninitializedObject(
            typeof(RabbitMqModuleViewModel));
        var shell = new ShellViewModel(nacos, rabbit);

        Assert.True(shell.IsNacosSelected);
        Assert.Same(nacos, shell.Nacos);
        Assert.Same(rabbit, shell.RabbitMq);

        shell.SelectedModule = AppModule.RabbitMq;
        Assert.True(shell.IsRabbitMqSelected);
        shell.SelectedModule = AppModule.Nacos;
        Assert.True(shell.IsNacosSelected);
    }
}
