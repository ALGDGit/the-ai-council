using System.Windows;
using TheIACouncil.Views;

namespace TheIACouncil;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowMainMenu();
    }

    public void ShowMainMenu() => Shell.Content = new MainMenuView();

    public void ShowGameModes() => Shell.Content = new GameModesView();

    public void ShowPlay() => Shell.Content = new PlayView();

    public void ShowPrisonerRiddle() => Shell.Content = new PrisonerRiddleView();

    public void ShowImpostorMode() => Shell.Content = new ImpostorView();

    public void ShowConfig() => Shell.Content = new ConfigView();

    public void ShowAbout() => Shell.Content = new AboutView();

    public void ShowAchievements() => Shell.Content = new AchievementsView();
}
