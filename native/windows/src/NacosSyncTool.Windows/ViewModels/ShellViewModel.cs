using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;

namespace NacosSyncTool.Windows.ViewModels;

public enum AppModule
{
    Nacos,
    RabbitMq
}

public partial class ShellViewModel : ObservableObject
{
    public MainViewModel Nacos { get; }
    public RabbitMqModuleViewModel RabbitMq { get; }

    [ObservableProperty]
    private AppModule _selectedModule = AppModule.Nacos;

    public bool IsNacosSelected => SelectedModule == AppModule.Nacos;
    public bool IsRabbitMqSelected => SelectedModule == AppModule.RabbitMq;

    public ShellViewModel(MainViewModel nacos, RabbitMqModuleViewModel rabbitMq)
    {
        Nacos = nacos;
        RabbitMq = rabbitMq;
    }

    [RelayCommand]
    private void SelectNacos() => SelectedModule = AppModule.Nacos;

    [RelayCommand]
    private void SelectRabbitMq() => SelectedModule = AppModule.RabbitMq;

    partial void OnSelectedModuleChanged(AppModule value)
    {
        OnPropertyChanged(nameof(IsNacosSelected));
        OnPropertyChanged(nameof(IsRabbitMqSelected));
    }
}
