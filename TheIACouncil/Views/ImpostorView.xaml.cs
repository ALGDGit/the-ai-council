using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheIACouncil.Helpers;
using TheIACouncil.Services;
using TheIACouncil.Windows;

namespace TheIACouncil.Views;

public partial class ImpostorView : UserControl
{
    private sealed class BrotherCase
    {
        public required ILLMClient Client { get; init; }
        public required string Alias { get; init; }
        public required ImageSource? Portrait { get; init; }
        public required bool IsImpostor { get; init; }
        public required bool SawVictimWithImpostor { get; init; }
        /// <summary>Para inocentes: otro inocente con quien comparten coartada, o null si estaba solo.</summary>
        public required string? AlibiAlias { get; init; }
        /// <summary>Bloque "Recuerda:" unificado para prompts iniciales y repreguntas.</summary>
        public string RecuerdaBlock { get; set; } = "";
        public required string InitialStatement { get; set; }
        /// <summary>Índice de color compartido entre la ficha del hermano y sus entradas en el acta.</summary>
        public int PaletteIndex { get; init; }
    }

    /// <summary>Sustitución cuando el modelo repite el prompt en lugar de interpretar el rol.</summary>
    private const string PromptEchoPlaceholder = "*está demasiado aterrado como para testificar ahora*";

    private const int PromptEchoPrefixWordCount = 10;
    private const int PromptEchoMinMatchedWords = 5;

    private const int BrotherUtteranceMaxWords = 100;

    /// <summary>Fondo, borde y color de título por monje (ficha + registros a su nombre).</summary>
    private static readonly (Color Back, Color Border, Color Title)[] MonkPalette =
    [
        (Color.FromRgb(52, 45, 38), Color.FromRgb(205, 168, 92), Color.FromRgb(238, 220, 185)),
        (Color.FromRgb(36, 46, 54), Color.FromRgb(118, 168, 208), Color.FromRgb(188, 216, 240)),
        (Color.FromRgb(48, 40, 52), Color.FromRgb(180, 140, 200), Color.FromRgb(228, 205, 238)),
        (Color.FromRgb(40, 50, 44), Color.FromRgb(130, 188, 150), Color.FromRgb(200, 232, 210)),
        (Color.FromRgb(54, 44, 38), Color.FromRgb(210, 145, 118), Color.FromRgb(242, 210, 195)),
        (Color.FromRgb(42, 48, 50), Color.FromRgb(120, 195, 195), Color.FromRgb(195, 236, 236)),
        (Color.FromRgb(50, 42, 48), Color.FromRgb(200, 150, 175), Color.FromRgb(240, 205, 218)),
        (Color.FromRgb(44, 48, 42), Color.FromRgb(165, 185, 115), Color.FromRgb(220, 232, 185))
    ];

    private static (Color Back, Color Border, Color Title) MonkColors(int paletteIndex) =>
        MonkPalette[paletteIndex % MonkPalette.Length];

    private static readonly string[] MundaneActivities =
    [
        "repasando salmos en el coro",
        "limpiando candelabros en la sacristia",
        "regando el huerto del claustro",
        "encuadernando un libro en la biblioteca",
        "llevando la cuenta de la despensa",
        "arreglando un manto en el taller de costura",
        "tocando la campana menor para el rezo",
        "sacando hierbas secas de la bodega",
        "copiando un texto en el scriptorium",
        "barriendo el pasillo del refectorio"
    ];

    private List<BrotherCase> _brothers = [];
    private readonly Dictionary<string, Border> _brotherChips = new(StringComparer.Ordinal);
    private string _impostorAlias = "";
    private string? _selectedBrotherAlias;
    private bool _finished;
    private bool _impostorViewportBound;

    public ImpostorView()
    {
        InitializeComponent();
        Loaded += ImpostorOnLoaded;
    }

    private void ImpostorOnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_impostorViewportBound)
        {
            _impostorViewportBound = true;
            ReadableLogText.AttachContentWidthToViewport(ImpostorLogScroll, LogPanel);
        }

        StartNewRound();
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private async void StartNewRound()
    {
        var settings = App.Config.Load();
        var pool = App.LlmFactory.CreateEnabledClients(settings).ToList();
        if (pool.Count < 3)
        {
            NoticeDialog.Show(
                "Este modo necesita al menos 3 IAs activas. Ve a Configuracion y activa mas hermanos.",
                "Faltan hermanos");
            Shell?.ShowConfig();
            return;
        }

        ShuffleInPlace(pool);
        var portraits = AssetPack.AssignRandomMonkPortraits(pool.Count);
        var aliases = MonkMotes.AssignUniqueRandom(pool.Count);

        var impostorIdx = Random.Shared.Next(pool.Count);
        _impostorAlias = aliases[impostorIdx];
        var innocentAliases = aliases
            .Select((a, idx) => (a, idx))
            .Where(t => t.idx != impostorIdx)
            .Select(t => t.a)
            .ToList();
        var innocentPartnerByAlias = BuildInnocentPartnerMap(innocentAliases);

        _brothers = [];
        for (var i = 0; i < pool.Count; i++)
        {
            var c = pool[i];
            var isImpostor = i == impostorIdx;
            innocentPartnerByAlias.TryGetValue(aliases[i], out var partner);
            _brothers.Add(new BrotherCase
            {
                Client = c,
                Alias = aliases[i],
                Portrait = i < portraits.Length ? portraits[i] : null,
                IsImpostor = isImpostor,
                SawVictimWithImpostor = !isImpostor && Random.Shared.NextDouble() < 0.52,
                AlibiAlias = isImpostor ? null : partner,
                InitialStatement = "",
                PaletteIndex = i
            });
        }

        FillRecuerdaBlocks();

        _finished = false;
        SetupUiForRound();
        StatusLine.Text = "Se oyen campanas funebres en el claustro. Recogiendo testimonios iniciales...";

        try
        {
            await CollectInitialStatementsAsync(settings.MaxConcurrentLlmRequests);
        }
        finally
        {
            if (!_finished)
                SetInterrogationControlsEnabled(true);
        }

        StatusLine.Text = "Ya puedes interrogar a cualquier hermano y luego acusar.";
    }

    private void SetupUiForRound()
    {
        LogPanel.Children.Clear();
        BrothersPanel.Children.Clear();
        _brotherChips.Clear();
        InterrogateQuestionBox.Text = "";
        SetInterrogationControlsEnabled(false);
        _selectedBrotherAlias = _brothers.Count > 0 ? _brothers[0].Alias : null;
        SelectedBrotherLine.Text = FormatSelectedBrotherLine(_selectedBrotherAlias);

        foreach (var b in _brothers)
        {
            var (back, border, _) = MonkColors(b.PaletteIndex);
            var chip = new Border
            {
                Background = new SolidColorBrush(back),
                BorderBrush = new SolidColorBrush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(6, 4, 6, 4),
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = Cursors.Hand,
                Tag = b.Alias
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var faceBack = Color.FromRgb(
                (byte)Math.Max(0, back.R - 14),
                (byte)Math.Max(0, back.G - 14),
                (byte)Math.Max(0, back.B - 14));
            var face = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(faceBack),
                Child = new Image { Source = b.Portrait, Stretch = Stretch.UniformToFill }
            };
            row.Children.Add(face);
            var labels = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock
            {
                Text = b.Alias,
                Foreground = (Brush)FindResource("Fg.OnDark"),
                FontFamily = new FontFamily("Georgia"),
                FontSize = 13
            });
            labels.Children.Add(new TextBlock
            {
                Text = $"{b.Client.ProviderLabel} · {b.Client.ModelId}",
                Foreground = (Brush)FindResource("Fg.Muted"),
                FontFamily = new FontFamily("Georgia"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            row.Children.Add(labels);
            chip.Child = row;
            chip.MouseLeftButtonUp += BrotherChipClick;
            _brotherChips[b.Alias] = chip;
            BrothersPanel.Children.Add(chip);
        }

        RefreshBrotherSelectionVisuals();
    }

    private void SetInterrogationControlsEnabled(bool enabled)
    {
        InterrogateQuestionBox.IsEnabled = enabled;
        AskButton.IsEnabled = enabled;
        AccuseButton.IsEnabled = enabled;
    }

    /// <summary>Recoge las declaraciones iniciales con el mismo tope de paralelismo que el concilio (evita CUDA/OOM en Ollama).</summary>
    private async Task CollectInitialStatementsAsync(int maxConcurrentLlmRequests)
    {
        var factories = new List<Func<Task<(int Index, string Text)>>>();
        for (var i = 0; i < _brothers.Count; i++)
        {
            var idx = i;
            var br = _brothers[idx];
            factories.Add(() => GetInitialStatementAsync(br, idx));
        }

        var statements = await LlmConcurrency.RunParallelLimitedAsync(maxConcurrentLlmRequests, factories, CancellationToken.None);
        Array.Sort(statements, static (a, b) => a.Index.CompareTo(b.Index));

        AppendLog("ACTA INICIAL", "Un hermano ha sido asesinado. El asesino esta entre ellos.");
        foreach (var s in statements)
        {
            _brothers[s.Index].InitialStatement = s.Text;
            AppendBrotherStatement(_brothers[s.Index].Alias, s.Text);
        }
    }

    private async Task<(int Index, string Text)> GetInitialStatementAsync(BrotherCase b, int idx)
    {
        var prompt = BuildInitialPrompt(b);
        var raw = await b.Client.CompleteAsync(prompt, CancellationToken.None);
        var text = FinalizeBrotherUtterance(raw, prompt, BrotherUtteranceMaxWords);
        return (idx, text);
    }

    private string BuildInitialPrompt(BrotherCase b)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Juego de rol: asesinato entre hermanos del monasterio. Responde siempre en espanol.");
        if (b.IsImpostor)
        {
            sb.AppendLine("Ha habido un asesinato. Tu eres el CULPABLE (traidor).");
            sb.AppendLine("No reveles tu culpa. Puedes mentir como quieras; tu coartada es inventada.");
            sb.AppendLine();
            sb.AppendLine(b.RecuerdaBlock);
        }
        else
        {
            sb.AppendLine("Ha habido un asesinato. Tu eres INOCENTE.");
            sb.AppendLine("Di la verdad y no contradigas estos hechos que te asignan:");
            sb.AppendLine();
            sb.AppendLine(b.RecuerdaBlock);
        }

        sb.AppendLine();
        sb.AppendLine(
            "Salida: un solo parrafo breve, como declaracion oral. Maximo 100 palabras. Sin listas ni comillas.");
        return sb.ToString();
    }

    private async void AskClick(object sender, RoutedEventArgs e)
    {
        if (_finished)
            return;
        if (_selectedBrotherAlias is not string targetAlias)
            return;

        var q = InterrogateQuestionBox.Text.Trim();
        if (q.Length == 0)
        {
            NoticeDialog.Show("Escribe una pregunta para interrogar.", "Falta pregunta");
            return;
        }

        var target = _brothers.FirstOrDefault(b => b.Alias == targetAlias);
        if (target == null)
            return;

        AskButton.IsEnabled = false;
        StatusLine.Text = $"Interrogando a {targetAlias}...";
        try
        {
            var interPrompt = BuildInterrogationPrompt(target, q);
            var answer = await target.Client.CompleteAsync(interPrompt, CancellationToken.None);
            var clean = FinalizeBrotherUtterance(answer, interPrompt, BrotherUtteranceMaxWords);
            AppendLog($"INTERROGAS A {targetAlias}", clean, target.PaletteIndex);

            var mentioned = FindMentionedBrother(clean, targetAlias);
            if (mentioned != null)
            {
                var followPrompt = BuildFollowUpPrompt(mentioned, targetAlias, q, clean);
                var followRaw = await mentioned.Client.CompleteAsync(followPrompt, CancellationToken.None);
                var follow = FinalizeBrotherUtterance(followRaw, followPrompt, BrotherUtteranceMaxWords);
                AppendLog($"REPLICA {mentioned.Alias}", follow, mentioned.PaletteIndex);
            }

            StatusLine.Text = "Interrogatorio registrado. Puedes seguir preguntando o acusar.";
        }
        catch (Exception ex)
        {
            StatusLine.Text = "";
            NoticeDialog.Show(ex.Message, "Error en interrogatorio");
        }
        finally
        {
            AskButton.IsEnabled = true;
        }
    }

    private string BuildInterrogationPrompt(BrotherCase b, string question)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Juego de rol: asesinato en el monasterio. Responde siempre en espanol.");
        sb.AppendLine(b.IsImpostor
            ? "Tu rol: CULPABLE. Puedes mentir; tu coartada es inventada."
            : "Tu rol: INOCENTE. Di la verdad y respeta tus hechos:");
        sb.AppendLine();
        sb.AppendLine(b.RecuerdaBlock);
        sb.AppendLine();
        sb.AppendLine("Pregunta del jugador:");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("Responde en un solo parrafo breve, maximo 100 palabras.");
        return sb.ToString();
    }

    private string BuildFollowUpPrompt(BrotherCase b, string sourceAlias, string playerQuestion, string sourceAnswer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Juego de rol: asesinato en el monasterio. Responde siempre en espanol.");
        sb.AppendLine(b.IsImpostor
            ? "Tu rol: CULPABLE. Puedes mentir; coartada inventada."
            : "Tu rol: INOCENTE. Verdad y coherencia con tus hechos:");
        sb.AppendLine();
        sb.AppendLine(b.RecuerdaBlock);
        sb.AppendLine();
        sb.AppendLine($"El jugador pregunto: {playerQuestion}");
        sb.AppendLine($"{sourceAlias} respondio: {sourceAnswer}");
        sb.AppendLine("Te mencionan directa o indirectamente. Responde a eso.");
        sb.AppendLine("Un solo parrafo breve. Maximo 100 palabras.");
        return sb.ToString();
    }

    private BrotherCase? FindMentionedBrother(string text, string excludeAlias)
    {
        var normalizedText = Normalize(text);
        foreach (var b in _brothers)
        {
            if (string.Equals(b.Alias, excludeAlias, StringComparison.OrdinalIgnoreCase))
                continue;
            var n = Normalize(b.Alias);
            if (n.Length > 2 && normalizedText.Contains(n, StringComparison.Ordinal))
                return b;
        }

        return null;
    }

    private void AccuseClick(object sender, RoutedEventArgs e)
    {
        if (_finished || _selectedBrotherAlias is not string accused)
            return;

        _finished = true;
        AskButton.IsEnabled = false;
        AccuseButton.IsEnabled = false;
        InterrogateQuestionBox.IsEnabled = false;

        var win = string.Equals(accused, _impostorAlias, StringComparison.OrdinalIgnoreCase);
        if (win)
        {
            AppendLog("VEREDICTO", $"Acusaste a {accused}. Era el impostor. Justicia en el claustro.");
            StatusLine.Text = "Has ganado. Puedes iniciar otra partida.";
        }
        else
        {
            AppendLog("VEREDICTO", $"Acusaste a {accused}, pero el impostor era {_impostorAlias}.");
            StatusLine.Text = "Has perdido. Puedes iniciar otra partida.";
        }
    }

    private void AppendBrotherStatement(string brotherAlias, string text)
    {
        var b = _brothers.FirstOrDefault(x => x.Alias == brotherAlias);
        AppendLog($"{brotherAlias} DECLARA", text, b?.PaletteIndex);
    }

    private void AppendLog(string title, string body, int? monkPaletteIndex = null)
    {
        Color cardBack;
        Brush borderBrush;
        Brush titleBrush;
        if (monkPaletteIndex is int pi)
        {
            var c = MonkColors(pi);
            cardBack = Color.FromRgb(
                (byte)Math.Min(255, c.Back.R + 5),
                (byte)Math.Min(255, c.Back.G + 5),
                (byte)Math.Min(255, c.Back.B + 5));
            borderBrush = new SolidColorBrush(c.Border);
            titleBrush = new SolidColorBrush(c.Title);
        }
        else
        {
            cardBack = Color.FromRgb(45, 39, 33);
            borderBrush = (Brush)FindResource("Trim.Gold");
            titleBrush = (Brush)FindResource("Accent");
        }

        var card = new Border
        {
            Background = new SolidColorBrush(cardBack),
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();
        stack.Children.Add(ReadableLogText.CreateReadOnlyMultiline(
            this,
            title,
            titleBrush,
            13,
            FontWeights.SemiBold,
            new Thickness(0, 0, 0, 4)));
        stack.Children.Add(ReadableLogText.CreateReadOnlyMultiline(
            this,
            body,
            (Brush)FindResource("Fg.OnDark"),
            14,
            FontWeights.Normal));
        card.Child = stack;
        LogPanel.Children.Add(card);
        Dispatcher.BeginInvoke(new Action(() => card.BringIntoView()));
    }

    /// <summary>Solo inocentes: parejas mutuas o uno solo si sobra. El impostor no entra aqui.</summary>
    private static Dictionary<string, string?> BuildInnocentPartnerMap(List<string> innocentAliases)
    {
        var map = innocentAliases.ToDictionary(a => a, _ => (string?)null, StringComparer.Ordinal);
        if (innocentAliases.Count < 2)
            return map;

        var shuffled = innocentAliases.OrderBy(_ => Random.Shared.Next()).ToList();
        while (shuffled.Count >= 2)
        {
            var a = shuffled[0];
            var b = shuffled[1];
            shuffled.RemoveRange(0, 2);
            map[a] = b;
            map[b] = a;
        }

        return map;
    }

    private void FillRecuerdaBlocks()
    {
        var acts = MundaneActivities.OrderBy(_ => Random.Shared.Next()).ToArray();
        var k = 0;
        foreach (var b in _brothers)
        {
            if (b.IsImpostor)
            {
                b.RecuerdaBlock = BuildImpostorInventedAlibiBlock(b.Alias);
                continue;
            }

            var alone = b.AlibiAlias is null;
            var activity = acts[k++ % acts.Length];
            var sighting = b.SawVictimWithImpostor
                ? $"Viste a la victima con {_impostorAlias} poco antes del crimen (verdad)."
                : "No viste a la victima acompanada de nadie que recuerdes; puede que la vieras sola o de lejos (verdad).";
            b.RecuerdaBlock = BuildInnocentRecuerdaBlock(alone, b.AlibiAlias, activity, sighting);
        }
    }

    private static string BuildInnocentRecuerdaBlock(bool alone, string? partner, string activity, string sightingLine)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Recuerda:");
        if (alone)
            sb.AppendLine("- Estabas solo.");
        else
            sb.AppendLine($"- Estabas con {partner} (eso debe coincidir con lo que diga esa persona; ambos sois inocentes).");
        sb.AppendLine($"- Estabas {activity}.");
        sb.AppendLine($"- {sightingLine}");
        return sb.ToString().TrimEnd();
    }

    private string BuildImpostorInventedAlibiBlock(string selfAlias)
    {
        var others = _brothers.Where(x => x.Alias != selfAlias).Select(x => x.Alias).ToList();
        var alone = Random.Shared.NextDouble() < 0.42;
        var act = MundaneActivities[Random.Shared.Next(MundaneActivities.Length)];
        var sb = new StringBuilder();
        sb.AppendLine("Recuerda (TODO ES MENTIRA / COARTADA INVENTADA; puedes mentir como quieras, pero suele ayudar inventar una historia):");
        if (alone)
            sb.AppendLine("- Inventa que estabas solo.");
        else
        {
            var decoy = others[Random.Shared.Next(others.Count)];
            sb.AppendLine($"- Inventa que estabas con {decoy} (falso; el inocente dira otra cosa).");
        }

        sb.AppendLine($"- Inventa que estabas {act} (puede ser otra cosa cotidiana).");
        if (Random.Shared.Next(2) == 0)
            sb.AppendLine("- Inventa que viste a la victima sola (falso).");
        else
        {
            var decoy = others[Random.Shared.Next(others.Count)];
            sb.AppendLine($"- Inventa que viste a la victima con {decoy} (falso, para despistar).");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FinalizeBrotherUtterance(string raw, string prompt, int maxWords)
    {
        var oneLine = CleanOneLine(raw);
        if (LooksLikePromptEcho(oneLine, prompt))
            return PromptEchoPlaceholder;
        return TrimToMaxWords(oneLine, maxWords);
    }

    /// <summary>
    /// Algunos modelos locales devuelven el propio prompt. Si las primeras palabras coinciden con las del prompt
    /// (o el texto es prefijo sustancial del prompt), se considera eco y se usa un marcador breve.
    /// </summary>
    private static bool LooksLikePromptEcho(string responseOneLine, string prompt)
    {
        if (string.IsNullOrWhiteSpace(responseOneLine))
            return true;

        var rw = TokenizeForEcho(responseOneLine);
        var pw = TokenizeForEcho(prompt);
        var k = Math.Min(PromptEchoPrefixWordCount, Math.Min(rw.Count, pw.Count));
        if (k >= PromptEchoMinMatchedWords)
        {
            var allMatch = true;
            for (var i = 0; i < k; i++)
            {
                if (!string.Equals(rw[i], pw[i], StringComparison.Ordinal))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return true;
        }

        var rc = CompactAlnumLetters(Normalize(responseOneLine));
        var pc = CompactAlnumLetters(Normalize(prompt));
        if (rc.Length >= 28 && pc.Length >= rc.Length && pc.StartsWith(rc, StringComparison.Ordinal))
            return true;

        return false;
    }

    private static List<string> TokenizeForEcho(string text)
    {
        var n = Normalize(text);
        var sb = new StringBuilder(n.Length);
        foreach (var ch in n)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else
                sb.Append(' ');
        }

        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string CompactAlnumLetters(string normalizedText)
    {
        var sb = new StringBuilder(normalizedText.Length);
        foreach (var ch in normalizedText)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string TrimToMaxWords(string text, int maxWords)
    {
        if (maxWords <= 0 || string.IsNullOrWhiteSpace(text))
            return text.Trim();

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= maxWords)
            return string.Join(" ", parts);

        return string.Join(" ", parts.Take(maxWords));
    }

    private static string CleanOneLine(string raw) =>
        string.Join(" ", raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string Normalize(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private void NewRoundClick(object sender, RoutedEventArgs e) => StartNewRound();

    private void BackClick(object sender, RoutedEventArgs e) => Shell?.ShowGameModes();

    private void BrotherChipClick(object sender, MouseButtonEventArgs e)
    {
        if (_finished || sender is not Border chip || chip.Tag is not string brotherAlias)
            return;
        SelectBrother(brotherAlias);
    }

    private void SelectBrother(string brotherAlias)
    {
        _selectedBrotherAlias = brotherAlias;
        SelectedBrotherLine.Text = FormatSelectedBrotherLine(brotherAlias);
        RefreshBrotherSelectionVisuals();
    }

    private string FormatSelectedBrotherLine(string? brotherAlias)
    {
        if (brotherAlias is null)
            return "Seleccionado: —";
        var b = _brothers.FirstOrDefault(x => x.Alias == brotherAlias);
        if (b is null)
            return $"Seleccionado: {brotherAlias}";
        return $"Seleccionado: {b.Alias} ({b.Client.ProviderLabel} · {b.Client.ModelId})";
    }

    private void RefreshBrotherSelectionVisuals()
    {
        foreach (var (alias, chip) in _brotherChips)
        {
            var b = _brothers.FirstOrDefault(x => x.Alias == alias);
            var (_, border, _) = MonkColors(b?.PaletteIndex ?? 0);
            var selected = string.Equals(alias, _selectedBrotherAlias, StringComparison.Ordinal);
            chip.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            if (selected)
            {
                chip.BorderBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, border.R + 40),
                    (byte)Math.Min(255, border.G + 35),
                    (byte)Math.Min(255, border.B + 25)));
                chip.Opacity = 1.0;
            }
            else
            {
                chip.BorderBrush = new SolidColorBrush(border);
                chip.Opacity = 0.94;
            }
        }
    }

    private static void ShuffleInPlace<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

}
