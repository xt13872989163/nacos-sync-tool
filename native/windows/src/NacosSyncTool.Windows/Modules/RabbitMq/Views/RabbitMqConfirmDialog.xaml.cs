using System.Windows;
using System.Windows.Media;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Views;

public partial class RabbitMqConfirmDialog : Window
{
    public RabbitMqConfirmDialog(RabbitMqConfirmationRequest request)
    {
        InitializeComponent();
        Title = request.Title;
        TitleText.Text = request.Title;
        BodyText.Text = request.Body;
        ConfirmButton.Content = request.ConfirmText;
        if (request.IsDangerous)
        {
            ConfirmButton.Background = new SolidColorBrush(Color.FromRgb(180, 35, 24));
            ConfirmButton.BorderBrush = ConfirmButton.Background;
        }
        Loaded += (_, _) => CancelButton.Focus();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
