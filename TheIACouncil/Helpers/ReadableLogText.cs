using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TheIACouncil.Helpers;

/// <summary>Registros de IA legibles: ancho acotado al viewport, envoltorio fiable y palabras muy largas partibles.</summary>
public static class ReadableLogText
{
    /// <summary>Añade "..." al final del texto mostrado. Si los tres puntos no aparecen, el recorte suele ser del layout; si aparecen, el texto llegó completo hasta ese marcador (p. ej. límite de la IA).</summary>
    public static string WithUiOverflowProbeSuffix(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";
        return text + "...";
    }

    /// <summary>Inserta puntos de ruptura invisibles para que el texto pueda partirse sin overflow horizontal.</summary>
    public static string SoftenLongRunsForWrap(string text, int maxRun = 44)
    {
        if (string.IsNullOrEmpty(text) || text.Length < maxRun)
            return text;

        var sb = new StringBuilder(text.Length + Math.Max(8, text.Length / maxRun));
        var run = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            sb.Append(c);
            if (char.IsWhiteSpace(c))
            {
                run = 0;
                continue;
            }

            run++;
            if (run < maxRun || i + 1 >= text.Length)
                continue;
            var next = text[i + 1];
            if (char.IsWhiteSpace(next))
                continue;
            sb.Append('\u200B');
            run = 0;
        }

        return sb.ToString();
    }

    public static TextBox CreateReadOnlyMultiline(
        FrameworkElement resourceScope,
        string text,
        Brush foreground,
        double fontSize,
        FontWeight fontWeight,
        Thickness margin = default)
    {
        var tb = new TextBox
        {
            Text = SoftenLongRunsForWrap(text),
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Margin = margin
        };

        if (resourceScope.TryFindResource("ReadOnlyLogTextBox") is Style st)
            tb.Style = st;

        return tb;
    }

    public static void AttachContentWidthToViewport(ScrollViewer scroll, FrameworkElement panel)
    {
        void Sync()
        {
            var w = scroll.ViewportWidth;
            if (w <= 1 || double.IsNaN(w) || double.IsInfinity(w))
                return;
            panel.Width = w;
        }

        scroll.SizeChanged += (_, _) => Sync();
        scroll.ScrollChanged += (_, _) => Sync();
        scroll.Loaded += (_, _) => Sync();
        if (scroll.IsLoaded)
            scroll.Dispatcher.BeginInvoke(new Action(Sync), DispatcherPriority.Loaded);
    }
}
