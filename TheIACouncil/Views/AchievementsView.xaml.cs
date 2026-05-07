using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TheIACouncil.Models;

namespace TheIACouncil.Views;

public partial class AchievementsView : UserControl
{
    private static readonly (string Id, string Title, string Desc)[] Catalog =
    [
        (AchievementIds.FirstSteps, "Primeros pasos",
            "Añade una IA correctamente."),
        (AchievementIds.CouncilSpoke, "El Consejo ha hablado",
            "Haz una pregunta por primera vez."),
        (AchievementIds.AbsoluteMajority, "Mayoría absoluta",
            "Consigue que todas las IAs digan que SI a algo con al menos 5 IAs."),
        (AchievementIds.Sentenced, "Sentenciado",
            "Consigue que todas las IAs digan No a algo con al menos 5 IAs."),
        (AchievementIds.ArtificialStupidity, "Estupidez Artificial",
            "Consigue que todas las IAs den respuestas confusas. Al menos 5 IAs.")
    ];

    public AchievementsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        AchievementRows.Children.Clear();
        var unlocked = App.Achievements.Unlocked;

        foreach (var row in Catalog)
        {
            var ok = unlocked.Contains(row.Id);
            var border = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(6),
                Background = ok
                    ? new SolidColorBrush(Color.FromRgb(34, 48, 40))
                    : new SolidColorBrush(Color.FromRgb(42, 38, 34)),
                BorderBrush = ok
                    ? new SolidColorBrush(Color.FromRgb(90, 130, 88))
                    : new SolidColorBrush(Color.FromRgb(90, 78, 58)),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var mark = new TextBlock
            {
                Text = ok ? "✓" : "○",
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = ok
                    ? new SolidColorBrush(Color.FromRgb(160, 210, 165))
                    : new SolidColorBrush(Color.FromRgb(140, 130, 118))
            };
            Grid.SetColumn(mark, 0);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = row.Title,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Fg.OnDark"),
                FontSize = 15
            });
            stack.Children.Add(new TextBlock
            {
                Text = row.Desc,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Fg.Muted"),
                Opacity = 0.88,
                FontSize = 13
            });
            Grid.SetColumn(stack, 1);

            grid.Children.Add(mark);
            grid.Children.Add(stack);
            border.Child = grid;
            AchievementRows.Children.Add(border);
        }
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w)
            w.ShowMainMenu();
    }
}
