using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TheIACouncil.Helpers;
using TheIACouncil.Models;
using TheIACouncil.Services;
using TheIACouncil.Windows;
using Color = System.Windows.Media.Color;

namespace TheIACouncil.Views;

public partial class PlayView : UserControl
{
    /// <summary>Transición escena al entrar en Jugar: fundido total ~2 s.</summary>
    private const int PlayTransitionTotalMs = 2000;

    private const int PlayTransitionFadeInMs = 700;
    private const int PlayTransitionFadeOutMs = 700;

    /// <summary>Ilustración del veredicto: fundido de entrada; la imagen permanece en el panel.</summary>
    private const int VerdictSceneFadeInMs = 650;

    private readonly Dictionary<string, TextBlock> _thinkingMarks = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _thinkingBlinkTimer;
    private int _thinkingDotsCount = 1;

    /// <summary>Retratos asignados en esta sesión de juego; se reutilizan entre rondas si el consejo no cambia.</summary>
    private ImageSource?[]? _sessionMonkPortraits;

    /// <summary>Miembros del consejo (sin orden): al cambiar, se reinician motes.</summary>
    private string? _sessionMemberSetKey;

    /// <summary>Orden actual del consejo: al barajar, nuevos retratos por índice (como antes).</summary>
    private string? _sessionPortraitOrderKey;

    /// <summary>Mote estable por hermano (clave BrotherName+Model+Provider) mientras no cambie el consejo.</summary>
    private readonly Dictionary<string, string> _monkMoteRegistry = new(StringComparer.Ordinal);

    private int _monkMoteAnonCounter;

    /// <summary>Caja de cada monje (fondo animado según el voto).</summary>
    private readonly Dictionary<string, Border> _monkRosterBoxes = new(StringComparer.Ordinal);

    private static readonly Color RosterBoxNeutral = Color.FromRgb(48, 42, 38);
    private static readonly Color RosterBorderNeutral = Color.FromRgb(100, 85, 58);
    /// <summary>Fondos de voto: verde translúcido = SÍ, rojo translúcido = NO (convención clara).</summary>
    private static readonly Color RosterVoteYesGreen = Color.FromArgb(168, 42, 98, 62);

    private static readonly Color RosterVoteNoRed = Color.FromArgb(168, 118, 44, 44);
    private static readonly Color RosterVoteAmbiguous = Color.FromArgb(150, 62, 54, 44);

    private const int MonkVoteStaggerMs = 110;
    private const int MonkVoteColorDurationMs = 880;

    private bool _verdictLogFinalized;
    private bool _logViewportBound;

    /// <summary>Fondos alternos por hermano (índice en el consejo) para leer el registro como log.</summary>
    private static readonly Color[] LogSpeakerBandColors =
    {
        Color.FromRgb(52, 44, 60),
        Color.FromRgb(44, 52, 58),
        Color.FromRgb(58, 50, 40),
        Color.FromRgb(40, 52, 48),
        Color.FromRgb(52, 48, 44),
        Color.FromRgb(46, 48, 58)
    };

    private static readonly Color LogEntryBorderColor = Color.FromRgb(92, 78, 52);

    public PlayView()
    {
        InitializeComponent();
        _thinkingBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _thinkingBlinkTimer.Tick += ThinkingBlinkTick;
        Unloaded += (_, _) => StopThinkingBlinkTimer();
        Loaded += OnPlayViewLoaded;
    }

    /// <summary>Transición con imagen al entrar en Jugar desde el menú principal (una vez por vista).</summary>
    private async void OnPlayViewLoaded(object sender, RoutedEventArgs e)
    {
        if (!_logViewportBound)
        {
            _logViewportBound = true;
            ReadableLogText.AttachContentWidthToViewport(DeliberationScroll, DeliberationLogPanel);
            ReadableLogText.AttachContentWidthToViewport(VerdictScroll, VerdictLogPanel);
        }

        Loaded -= OnPlayViewLoaded;
        await PlayOpeningSceneAsync();
        await RefreshCouncilRosterFromConfigAsync(forceNewFaces: true, randomizeOrder: true);
    }

    private static string CouncilMemberSetKey(IReadOnlyList<ILLMClient> council) =>
        string.Join("|",
            council
                .Select(c => $"{c.BrotherName}\u001F{c.ModelId}\u001F{c.ProviderLabel}")
                .OrderBy(s => s, StringComparer.Ordinal));

    private static string CouncilOrderKey(IReadOnlyList<ILLMClient> council) =>
        string.Join("|", council.Select(c => $"{c.BrotherName}\u001F{c.ModelId}\u001F{c.ProviderLabel}"));

    private static void ShuffleInPlace<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Rellena el concilio según la configuración actual; permite barajar y animar el reordenado.</summary>
    private async Task RefreshCouncilRosterFromConfigAsync(bool forceNewFaces, bool randomizeOrder)
    {
        var settings = App.Config.Load();
        var council = App.LlmFactory.CreateEnabledClients(settings).ToList();
        if (randomizeOrder)
            ShuffleInPlace(council);

        await RefreshCouncilRosterAsync(council, forceNewFaces);
    }

    /// <summary>Reconstruye el roster con fundido para que el cambio de orden no sea repentino.</summary>
    private async Task RefreshCouncilRosterAsync(IReadOnlyList<ILLMClient> council, bool forceNewFaces)
    {
        if (council.Count == 0)
        {
            ShowEmptyCouncilPlaceholder();
            return;
        }

        var memberSet = CouncilMemberSetKey(council);
        if (_sessionMemberSetKey != memberSet)
        {
            _monkMoteRegistry.Clear();
            _monkMoteAnonCounter = 0;
            _sessionMemberSetKey = memberSet;
        }

        var orderKey = CouncilOrderKey(council);
        ImageSource?[] faces;
        if (!forceNewFaces &&
            _sessionPortraitOrderKey == orderKey &&
            _sessionMonkPortraits != null &&
            _sessionMonkPortraits.Length == council.Count)
        {
            faces = _sessionMonkPortraits;
        }
        else
        {
            faces = AssetPack.AssignRandomMonkPortraits(council.Count);
            _sessionMonkPortraits = faces;
            _sessionPortraitOrderKey = orderKey;
        }

        MonkMotes.RegisterForCouncil(council, _monkMoteRegistry, ref _monkMoteAnonCounter);
        var motes = MonkMotes.MotesInOrder(council, _monkMoteRegistry);

        var hadRoster = CouncilRosterPanel.Children.Count > 0 && CouncilRosterHost.Visibility == Visibility.Visible;
        if (hadRoster)
            await AnimateOpacityAsync(CouncilRosterHost, 1, 0, 210, smoothStep: true);

        BuildCouncilRoster(council, faces, motes);
        CouncilRosterHost.Opacity = hadRoster ? 0 : 1;
        if (hadRoster)
            await AnimateOpacityAsync(CouncilRosterHost, 0, 1, 250, smoothStep: true);
    }

    private void ShowEmptyCouncilPlaceholder()
    {
        StopThinkingBlinkTimer();
        _thinkingMarks.Clear();
        _sessionMonkPortraits = null;
        _sessionMemberSetKey = null;
        _sessionPortraitOrderKey = null;
        _monkMoteRegistry.Clear();
        _monkMoteAnonCounter = 0;
        _monkRosterBoxes.Clear();
        CouncilRosterPanel.Children.Clear();
        CouncilRosterPanel.Children.Add(new TextBlock
        {
            Text = "No hay hermanos en el consejo. Activa al menos un proveedor en Configuración.",
            Style = (Style)FindResource("Muted"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 520,
            Margin = new Thickness(8, 4, 8, 4)
        });
        CouncilRosterHost.Visibility = Visibility.Visible;
    }

    private void ThinkingBlinkTick(object? sender, EventArgs e)
    {
        _thinkingDotsCount = _thinkingDotsCount % 3 + 1;
        var ch = new string('.', _thinkingDotsCount);
        foreach (var kv in _thinkingMarks)
        {
            if (kv.Value.Visibility == Visibility.Visible)
                kv.Value.Text = ch;
        }
    }

    private void StartThinkingBlinkTimer()
    {
        if (!_thinkingBlinkTimer.IsEnabled)
            _thinkingBlinkTimer.Start();
    }

    private void StopThinkingBlinkTimer()
    {
        _thinkingBlinkTimer.Stop();
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        StopThinkingBlinkTimer();
        OpeningSceneHost.Visibility = Visibility.Collapsed;
        OpeningSceneImage.Source = null;
        if (Window.GetWindow(this) is MainWindow w)
            w.ShowMainMenu();
    }

    private void QuestionBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        if (RunButton.IsEnabled)
            RunClick(RunButton, new RoutedEventArgs(Button.ClickEvent, RunButton));
    }

    private void BuildCouncilRoster(IReadOnlyList<ILLMClient> council, IReadOnlyList<ImageSource?> portraits,
        IReadOnlyList<string> monkMotes)
    {
        CouncilRosterPanel.Children.Clear();
        _thinkingMarks.Clear();
        _monkRosterBoxes.Clear();

        for (var i = 0; i < council.Count; i++)
        {
            var client = council[i];
            var mote = i < monkMotes.Count ? monkMotes[i] : client.BrotherName;
            var portrait = i < portraits.Count ? portraits[i] : null;

            var shell = new Grid
            {
                MinWidth = 88,
                MaxWidth = 120,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var qMark = new TextBlock
            {
                Text = ".",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(201, 162, 39)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(qMark, 0);
            _thinkingMarks[client.BrotherName] = qMark;

            var face = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = new SolidColorBrush(Color.FromRgb(42, 38, 34)),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                ClipToBounds = true
            };
            Grid.SetRow(face, 1);

            var inner = new Grid();
            if (portrait != null)
            {
                inner.Children.Add(new Image
                {
                    Source = portrait,
                    Stretch = Stretch.UniformToFill
                });
            }

            var initialsTb = new TextBlock
            {
                Text = BrotherGlyph.Initials(mote),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.WhiteSmoke
            };
            if (portrait != null)
                initialsTb.Visibility = Visibility.Collapsed;
            inner.Children.Add(initialsTb);
            face.Child = inner;

            var nameTb = new TextBlock
            {
                Text = mote,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Fg.OnDark"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                ToolTip = $"{mote}\n{client.BrotherName}\n{client.ProviderLabel} · {client.ModelId}"
            };
            Grid.SetRow(nameTb, 2);

            shell.Children.Add(qMark);
            shell.Children.Add(face);
            shell.Children.Add(nameTb);

            var box = new Border
            {
                Margin = new Thickness(10, 6, 10, 6),
                Padding = new Thickness(10, 10, 10, 10),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(RosterBorderNeutral),
                Background = new SolidColorBrush(RosterBoxNeutral),
                Child = shell,
                SnapsToDevicePixels = true
            };
            _monkRosterBoxes[client.BrotherName] = box;
            CouncilRosterPanel.Children.Add(box);
        }

        CouncilRosterHost.Visibility = Visibility.Visible;
    }

    private void ResetMonkRosterBoxColors()
    {
        foreach (var box in _monkRosterBoxes.Values)
        {
            if (box.Background is SolidColorBrush bg)
            {
                bg.BeginAnimation(SolidColorBrush.ColorProperty, null);
                bg.Color = RosterBoxNeutral;
            }
            else
                box.Background = new SolidColorBrush(RosterBoxNeutral);

            if (box.BorderBrush is SolidColorBrush bb)
            {
                bb.BeginAnimation(SolidColorBrush.ColorProperty, null);
                bb.Color = RosterBorderNeutral;
            }
            else
                box.BorderBrush = new SolidColorBrush(RosterBorderNeutral);
        }
    }

    private static Color VoteBoxColor(CouncilVote v) =>
        v.IsYes switch
        {
            true => RosterVoteYesGreen,
            false => RosterVoteNoRed,
            _ => RosterVoteAmbiguous
        };

    private async Task AnimateMonkRosterVoteColorsAsync(CouncilResult result)
    {
        var stagger = 0;
        var tasks = new List<Task>();
        foreach (var v in result.Votes)
        {
            if (!_monkRosterBoxes.TryGetValue(v.BrotherName, out var box))
                continue;
            var target = VoteBoxColor(v);
            var delay = stagger;
            stagger += MonkVoteStaggerMs;
            tasks.Add(AnimateMonkBoxToColorAsync(box, target, delay, MonkVoteColorDurationMs));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private async Task AnimateMonkBoxToColorAsync(Border box, Color targetColor, int delayMs, int durationMs)
    {
        if (delayMs > 0)
            await Task.Delay(delayMs).ConfigureAwait(true);

        var tcs = new TaskCompletionSource();
        await Dispatcher.InvokeAsync(() =>
        {
            if (box.Background is not SolidColorBrush brush)
            {
                brush = new SolidColorBrush(RosterBoxNeutral);
                box.Background = brush;
            }

            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            var from = brush.Color;
            var anim = new ColorAnimation(from, targetColor, new Duration(TimeSpan.FromMilliseconds(durationMs)))
            {
                FillBehavior = FillBehavior.HoldEnd,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            anim.Completed += (_, _) => tcs.TrySetResult();
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        });

        await tcs.Task.ConfigureAwait(true);
    }

    private void UpdateThinkingIndicator(string? brotherName)
    {
        foreach (var kv in _thinkingMarks)
        {
            var on = !string.IsNullOrEmpty(brotherName) &&
                     string.Equals(kv.Key, brotherName, StringComparison.Ordinal);
            kv.Value.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (!on)
                kv.Value.Text = ".";
        }

        if (string.IsNullOrEmpty(brotherName))
        {
            StopThinkingBlinkTimer();
            _thinkingDotsCount = 1;
        }
        else
        {
            _thinkingDotsCount = 1;
            foreach (var kv in _thinkingMarks)
            {
                if (kv.Value.Visibility == Visibility.Visible)
                    kv.Value.Text = ".";
            }

            StartThinkingBlinkTimer();
        }
    }

    /// <summary>
    /// Modalidades del veredicto según recuento: absoluta de SÍ/NO (más de la mitad de las voces),
    /// simple de SÍ/NO (gana en pluralidad pero sin superar la mitad), o mayoría ambigua (empates o sin claro vencedor).
    /// </summary>
    private enum VerdictMajorityKind
    {
        AbsoluteYes,
        SimpleYes,
        SimpleNo,
        AbsoluteNo,
        Ambiguous
    }

    private static VerdictMajorityKind ClassifyVerdictMajority(CouncilResult r)
    {
        var y = r.YesCount;
        var n = r.NoCount;
        var u = r.UnclearCount;
        var t = y + n + u;
        if (t == 0)
            return VerdictMajorityKind.Ambiguous;

        if (y * 2 > t)
            return VerdictMajorityKind.AbsoluteYes;
        if (n * 2 > t)
            return VerdictMajorityKind.AbsoluteNo;

        if (y > n && y > u)
            return VerdictMajorityKind.SimpleYes;
        if (n > y && n > u)
            return VerdictMajorityKind.SimpleNo;

        return VerdictMajorityKind.Ambiguous;
    }

    private static string BuildVerdictDrama(CouncilResult r)
    {
        if (r.Votes.Count == 0)
            return "";

        var y = r.YesCount;
        var n = r.NoCount;
        var u = r.UnclearCount;
        var t = r.Votes.Count;

        return ClassifyVerdictMajority(r) switch
        {
            VerdictMajorityKind.AbsoluteYes =>
                $"El Concilio alza el cáliz del afirmar: {y} de {t} voces han sellado un SÍ incontestable. "
                + $"No queda resquicio bajo la viga del scriptorio: el sí supera la mitad del coro y deja al NO ({n}) y a la duda ({u}) en segundo plano.",

            VerdictMajorityKind.SimpleYes =>
                $"La balanza se inclina, pero no hasta el suelo: el SÍ toma la delantera con {y} voces de {t}. "
                + $"Es una mayoría simple; el NO ({n}) y los ambiguos ({u}) todavía susurran resistencia en el claustro.",

            VerdictMajorityKind.SimpleNo =>
                $"El martillo cae del lado del NO con {n} de {t} voces, aunque sin la losa de la mayoría absoluta. "
                + $"El rechazo manda por mayoría simple; el sí ({y}) y la duda ({u}) no bastan para abrir de nuevo la puerta.",

            VerdictMajorityKind.AbsoluteNo =>
                $"El scriptorio se cubre de sombra: {n} de {t} voces han pronunciado un NO con peso de losa. "
                + $"La mayoría absoluta niega sin tregua; el sí ({y}) y la ambigüedad ({u}) quedan ahogados por el veredicto.",

            VerdictMajorityKind.Ambiguous =>
                $"Las cuerdas del Concilio vibran disonantes: no emerge una mayoría clara entre SÍ ({y}), NO ({n}) y ambiguos ({u}) sobre {t} voces. "
                + "Empates y fuerzas cruzadas envuelven el acta en niebla; el veredicto queda suspendido, escrito con tinta temblorosa."
,
            _ =>
                $"El Concilio murmura en penumbra: SÍ {y}, NO {n}, ambiguos {u} (de {t}). "
                + "No hay sentencia limpia, solo un eco dramático que pide nueva deliberación."
        };
    }

    private enum VerdictSceneKind { Approval, Rejection, Chaos }

    private static VerdictSceneKind ClassifyVerdictScene(CouncilResult r)
    {
        var y = r.YesCount;
        var n = r.NoCount;
        var u = r.UnclearCount;
        if (y > n && y > u)
            return VerdictSceneKind.Approval;
        if (n > y && n > u)
            return VerdictSceneKind.Rejection;
        return VerdictSceneKind.Chaos;
    }

    /// <param name="smoothStep">Si es true, curva suave al entrar y salir (más paulatino).</param>
    private async Task AnimateOpacityAsync(FrameworkElement element, double from, double to, int durationMs,
        bool smoothStep = false)
    {
        var frameMs = 16;
        var steps = Math.Max(1, durationMs / frameMs);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            if (smoothStep)
                t = t * t * (3 - 2 * t);
            var v = from + (to - from) * t;
            await Dispatcher.InvokeAsync(() => element.Opacity = v);
            await Task.Delay(frameMs);
        }
    }

    /// <summary>
    /// Al abrir la pantalla Jugar (desde el menú): imagen de <c>Assets/Scenes</c> ~2 s, fundido de entrada y de salida.
    /// </summary>
    private async Task PlayOpeningSceneAsync()
    {
        var bmp = AssetPack.TryRandomPlayTransitionFromScenes();
        if (bmp == null)
            return;

        OpeningSceneImage.Source = bmp;
        OpeningSceneImage.Opacity = 0;
        OpeningSceneHost.Visibility = Visibility.Visible;

        await AnimateOpacityAsync(OpeningSceneImage, 0, 1, PlayTransitionFadeInMs, smoothStep: true);

        var holdMs = PlayTransitionTotalMs - PlayTransitionFadeInMs - PlayTransitionFadeOutMs;
        if (holdMs > 0)
            await Task.Delay(holdMs);

        await AnimateOpacityAsync(OpeningSceneImage, 1, 0, PlayTransitionFadeOutMs, smoothStep: true);

        OpeningSceneHost.Visibility = Visibility.Collapsed;
        OpeningSceneImage.Source = null;
    }

    /// <summary>
    /// Imagen de <c>Assets/Verdict</c> (yes/no/fog): fundido de entrada y permanece visible bajo el título del veredicto.
    /// </summary>
    private async Task PlayVerdictSceneAsync(CouncilResult result)
    {
        VerdictSceneImage.Source = null;
        VerdictSceneImage.Opacity = 0;
        VerdictSceneHost.Visibility = Visibility.Collapsed;

        var bmp = ClassifyVerdictScene(result) switch
        {
            VerdictSceneKind.Approval => AssetPack.TryVerdictSceneApproval(),
            VerdictSceneKind.Rejection => AssetPack.TryVerdictSceneRejection(),
            _ => AssetPack.TryVerdictSceneChaos()
        };

        if (bmp == null)
            return;

        VerdictSceneImage.Source = bmp;
        VerdictSceneHost.Visibility = Visibility.Visible;

        await AnimateOpacityAsync(VerdictSceneImage, 0, 1, VerdictSceneFadeInMs, smoothStep: true);
    }

    private void ClearCouncilLogPanels()
    {
        DeliberationLogPanel.Children.Clear();
        VerdictLogPanel.Children.Clear();
    }

    private static Color LogBandColor(int speakerIndex) =>
        LogSpeakerBandColors[(speakerIndex & 0x7FFF_FFFF) % LogSpeakerBandColors.Length];

    private Border CreateLogEntryCard(Color background, string heading, string body, bool bodyIsMuted = false)
    {
        var stack = new StackPanel();
        stack.Children.Add(ReadableLogText.CreateReadOnlyMultiline(
            this,
            heading,
            (Brush)FindResource("Fg.OnDark"),
            13,
            FontWeights.SemiBold,
            new Thickness(0, 0, 0, string.IsNullOrWhiteSpace(body) ? 0 : 6)));
        if (!string.IsNullOrWhiteSpace(body))
        {
            stack.Children.Add(ReadableLogText.CreateReadOnlyMultiline(
                this,
                ReadableLogText.WithUiOverflowProbeSuffix(body),
                bodyIsMuted ? (Brush)FindResource("Fg.Muted") : (Brush)FindResource("Fg.OnDark"),
                14,
                FontWeights.Normal));
        }

        return new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(LogEntryBorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = stack
        };
    }

    private void AppendDeliberationLogEntry(CouncilTurn t, int speakerIndex)
    {
        var heading =
            $"── {t.MonkMote} · {t.PersonalityLabel} · {t.ProviderLabel} · {t.ModelId} ──";
        var body = TrimRedundantSpeakerLead(t.Paragraph.Trim(), t.BrotherName, t.MonkMote);
        var card = CreateLogEntryCard(LogBandColor(speakerIndex), heading, body);
        DeliberationLogPanel.Children.Add(card);
        Dispatcher.BeginInvoke(new Action(() => card.BringIntoView()), DispatcherPriority.Loaded);
    }

    /// <summary>Evita que la primera línea repita el nombre técnico o el mote (ya van en el título).</summary>
    private static string TrimRedundantSpeakerLead(string paragraph, string brotherName, string monkMote)
    {
        if (paragraph.Length == 0)
            return paragraph;

        var n = paragraph.IndexOfAny(new[] { '\r', '\n' });
        var first = (n < 0 ? paragraph : paragraph[..n]).Trim();
        var rest = n < 0 ? "" : paragraph[(n + 1)..].TrimStart();

        bool MatchesName(string name)
        {
            if (name.Length == 0)
                return false;
            if (first.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;
            var withColon = name + ":";
            return first.Equals(withColon, StringComparison.OrdinalIgnoreCase) ||
                   first.StartsWith(withColon + " ", StringComparison.OrdinalIgnoreCase) &&
                   first.Length <= name.Length + 80;
        }

        if (MatchesName(brotherName) || MatchesName(monkMote))
            return rest.Length > 0 ? rest : paragraph;

        return paragraph;
    }

    private void FinalizeVerdictLogPanel(CouncilResult result)
    {
        if (_verdictLogFinalized)
            return;

        _verdictLogFinalized = true;

        var drama = BuildVerdictDrama(result);
        FrameworkElement? scrollTo = null;
        if (!string.IsNullOrEmpty(drama))
        {
            scrollTo = ReadableLogText.CreateReadOnlyMultiline(
                this,
                drama,
                (Brush)FindResource("Fg.OnDark"),
                14,
                FontWeights.Normal,
                new Thickness(0, 0, 0, 14));
            VerdictLogPanel.Children.Add(scrollTo);
        }

        scrollTo = ReadableLogText.CreateReadOnlyMultiline(
            this,
            $"SÍ {result.YesCount} · NO {result.NoCount} · Ambiguos {result.UnclearCount} (entre {result.Votes.Count} voces).",
            (Brush)FindResource("Fg.OnDark"),
            14,
            FontWeights.SemiBold,
            new Thickness(0, string.IsNullOrEmpty(drama) ? 0 : 14, 0, 0));
        VerdictLogPanel.Children.Add(scrollTo);
        Dispatcher.BeginInvoke(new Action(() => scrollTo?.BringIntoView()), DispatcherPriority.Loaded);
    }

    private async Task PresentCouncilResultAsync(CouncilResult result)
    {
        VerdictSceneImage.Source = null;
        VerdictSceneImage.Opacity = 0;
        VerdictSceneHost.Visibility = Visibility.Collapsed;

        await Task.Delay(380);

        await AnimateMonkRosterVoteColorsAsync(result);
        await PlayVerdictSceneAsync(result);

        FinalizeVerdictLogPanel(result);
    }

    private async void RunClick(object sender, RoutedEventArgs e)
    {
        var q = QuestionBox.Text.Trim();
        if (q.Length == 0)
        {
            NoticeDialog.Show("Escribe una pregunta.", "Falta la pregunta");
            return;
        }

        RunButton.IsEnabled = false;
        _verdictLogFinalized = false;
        ClearCouncilLogPanels();
        StatusLine.Text = "El consejo se reúne…";
        VerdictActivityLine.Text = "";
        VerdictActivityLine.Visibility = Visibility.Collapsed;
        VerdictSceneImage.Source = null;
        VerdictSceneImage.Opacity = 0;
        VerdictSceneHost.Visibility = Visibility.Collapsed;

        try
        {
            var settings = App.Config.Load();
            var council = App.LlmFactory.CreateEnabledClients(settings);
            if (council.Count == 0)
            {
                NoticeDialog.Show(
                    "No hay proveedores activos. Abre Configuración y activa al menos una IA (clave API o modelos Ollama).",
                    "Sin miembros");
                return;
            }

            var councilList = council.ToList();
            ShuffleInPlace(councilList);
            await RefreshCouncilRosterAsync(councilList, forceNewFaces: false);
            ResetMonkRosterBoxColors();

            var progress = new Progress<CouncilProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (p.DeliberationTurnFinished is { } turn && p.DeliberationSpeakerIndex is { } di)
                        AppendDeliberationLogEntry(turn, di);

                    var line = p.ReasoningLog ?? p.VerdictLog ?? "";
                    if (!string.IsNullOrEmpty(line))
                        StatusLine.Text = line;

                    if (!string.IsNullOrEmpty(p.VerdictLog))
                    {
                        VerdictActivityLine.Text = "Las IAs están pensando su voto…";
                        VerdictActivityLine.Visibility = Visibility.Visible;
                    }
                    UpdateThinkingIndicator(p.ThinkingBrother);
                });
            });

            var monkMotes = MonkMotes.MotesInOrder(councilList, _monkMoteRegistry);
            var cfg = App.Config.Load();
            var result = await App.CouncilGame.RunAsync(
                councilList,
                monkMotes,
                q,
                cfg.MaxConcurrentLlmRequests,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            Dispatcher.Invoke(() => UpdateThinkingIndicator(null));

            App.Achievements.EvaluateAfterCouncil(result);

            await PresentCouncilResultAsync(result);

            VerdictActivityLine.Text = "Votación cerrada.";
            VerdictActivityLine.Visibility = Visibility.Visible;
            StatusLine.Text = "Listo.";
        }
        catch (Exception ex)
        {
            StatusLine.Text = "";
            StopThinkingBlinkTimer();
            UpdateThinkingIndicator(null);
            VerdictActivityLine.Text = "";
            VerdictActivityLine.Visibility = Visibility.Collapsed;
            OpeningSceneHost.Visibility = Visibility.Collapsed;
            OpeningSceneImage.Source = null;
            NoticeDialog.Show(ex.Message, "Error");
        }
        finally
        {
            RunButton.IsEnabled = true;
        }
    }
}
