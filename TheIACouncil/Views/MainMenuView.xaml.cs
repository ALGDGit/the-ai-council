using System.Windows;
using System.Windows.Controls;

namespace TheIACouncil.Views;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void PlayClick(object sender, RoutedEventArgs e) => Shell?.ShowGameModes();

    private void ConfigClick(object sender, RoutedEventArgs e) => Shell?.ShowConfig();

    private void AboutClick(object sender, RoutedEventArgs e) => Shell?.ShowAbout();

    private void ExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void AchievementsClick(object sender, RoutedEventArgs e) => Shell?.ShowAchievements();
}
