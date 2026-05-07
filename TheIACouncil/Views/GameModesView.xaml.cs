using System.Windows;
using System.Windows.Controls;

namespace TheIACouncil.Views;

public partial class GameModesView : UserControl
{
    public GameModesView()
    {
        InitializeComponent();
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void ClassicModeClick(object sender, RoutedEventArgs e) => Shell?.ShowPlay();

    private void PrisonerModeClick(object sender, RoutedEventArgs e) => Shell?.ShowPrisonerRiddle();

    private void ImpostorModeClick(object sender, RoutedEventArgs e) => Shell?.ShowImpostorMode();

    private void BackClick(object sender, RoutedEventArgs e) => Shell?.ShowMainMenu();
}
