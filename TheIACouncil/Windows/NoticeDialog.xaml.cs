using System.Windows;

namespace TheIACouncil.Windows;

public partial class NoticeDialog : Window
{
    public NoticeDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleLine.Text = title;
        Body.Text = message;
    }

    /// <summary>Mensaje de estilo pergamino (no MessageBox de Windows). Opcionalmente ejecuta código al cerrar.</summary>
    public static void Show(string message, string title = "Consejo", Action? afterClose = null)
    {
        var dlg = new NoticeDialog(title, message) { Owner = Application.Current.MainWindow };
        dlg.ShowDialog();
        afterClose?.Invoke();
    }

    private void OkClick(object sender, RoutedEventArgs e) => Close();
}
