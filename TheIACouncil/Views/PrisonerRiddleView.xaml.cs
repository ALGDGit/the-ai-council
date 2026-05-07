using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TheIACouncil.Helpers;
using TheIACouncil.Services;
using TheIACouncil.Windows;

namespace TheIACouncil.Views;

public partial class PrisonerRiddleView : UserControl
{
    private ILLMClient? _guardA;
    private ILLMClient? _guardB;
    private string _guardAMote = "";
    private string _guardBMote = "";
    private bool _asked;
    private bool _leftDoorFreedom;
    private bool _guardATruthful;
    private ImageSource?[] _guardPortraits = Array.Empty<ImageSource?>();
    private bool _prisonerViewportBound;

    public PrisonerRiddleView()
    {
        InitializeComponent();
        Loaded += PrisonerOnLoaded;
    }

    private void PrisonerOnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_prisonerViewportBound)
        {
            _prisonerViewportBound = true;
            ReadableLogText.AttachContentWidthToViewport(GuardAReplyScroll, GuardAReply);
            ReadableLogText.AttachContentWidthToViewport(GuardBReplyScroll, GuardBReply);
        }

        SetupRound();
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void SetupRound()
    {
        var settings = App.Config.Load();
        var pool = App.LlmFactory.CreateEnabledClients(settings).ToList();
        if (pool.Count < 2)
        {
            NoticeDialog.Show(
                "Necesitas al menos dos IAs activas para este modo. Abre Configuracion y activa mas miembros.",
                "Faltan guardianes");
            Shell?.ShowConfig();
            return;
        }

        ShuffleInPlace(pool);
        _guardA = pool[0];
        _guardB = pool[1];
        _guardATruthful = Random.Shared.Next(2) == 0;
        _leftDoorFreedom = Random.Shared.Next(2) == 0;
        _asked = false;
        _guardPortraits = AssetPack.AssignRandomMonkPortraits(2);
        var motes = MonkMotes.AssignUniqueRandom(2);
        _guardAMote = motes[0];
        _guardBMote = motes[1];

        QuestionBox.Text = "";
        GuardALabel.Text = $"{_guardAMote} · {_guardA.ProviderLabel} · {_guardA.ModelId}";
        GuardBLabel.Text = $"{_guardBMote} · {_guardB.ProviderLabel} · {_guardB.ModelId}";
        GuardAPortrait.Source = _guardPortraits.Length > 0 ? _guardPortraits[0] : null;
        GuardBPortrait.Source = _guardPortraits.Length > 1 ? _guardPortraits[1] : null;
        GuardAReply.Text = "";
        GuardBReply.Text = "";
        ResultLine.Text = "";
        StatusLine.Text = "Formula una sola pregunta para ambos guardianes.";
        AskButton.IsEnabled = true;
        LeftDoorButton.IsEnabled = false;
        RightDoorButton.IsEnabled = false;
        QuestionBox.IsEnabled = true;
    }

    private async void AskClick(object sender, RoutedEventArgs e)
    {
        if (_asked || _guardA == null || _guardB == null)
            return;

        var q = QuestionBox.Text.Trim();
        if (q.Length == 0)
        {
            NoticeDialog.Show("Escribe tu pregunta antes de consultar a los guardianes.", "Falta la pregunta");
            return;
        }

        _asked = true;
        AskButton.IsEnabled = false;
        QuestionBox.IsEnabled = false;
        StatusLine.Text = "Los guardianes deliberan...";

        try
        {
            var promptA = BuildGuardPrompt(q, _guardATruthful, _guardAMote);
            var promptB = BuildGuardPrompt(q, !_guardATruthful, _guardBMote);

            var tA = _guardA.CompleteAsync(promptA, CancellationToken.None);
            var tB = _guardB.CompleteAsync(promptB, CancellationToken.None);
            await Task.WhenAll(tA, tB);

            GuardAReply.Text = ReadableLogText.WithUiOverflowProbeSuffix(
                ReadableLogText.SoftenLongRunsForWrap(tA.Result.Trim()));
            GuardBReply.Text = ReadableLogText.WithUiOverflowProbeSuffix(
                ReadableLogText.SoftenLongRunsForWrap(tB.Result.Trim()));

            LeftDoorButton.IsEnabled = true;
            RightDoorButton.IsEnabled = true;
            StatusLine.Text = "Ya tienes ambas respuestas. Elige puerta izquierda o derecha.";
        }
        catch (Exception ex)
        {
            _asked = false;
            AskButton.IsEnabled = true;
            QuestionBox.IsEnabled = true;
            StatusLine.Text = "";
            NoticeDialog.Show(ex.Message, "Error al consultar a los guardianes");
        }
    }

    private string BuildGuardPrompt(string playerQuestion, bool truthful, string guardianMote)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Juego del prisionero. Responde SIEMPRE en espanol.");
        sb.AppendLine(
            $"Tu nombre como guardian es \"{guardianMote}\" (monje del acertijo; no menciones proveedor ni modelo).");
        sb.AppendLine();
        sb.AppendLine(
            _leftDoorFreedom
                ? "Hecho real del mundo: la puerta IZQUIERDA lleva a la libertad y la DERECHA a la muerte."
                : "Hecho real del mundo: la puerta DERECHA lleva a la libertad y la IZQUIERDA a la muerte.");
        sb.AppendLine(truthful
            ? "Tu rol secreto: eres el guardia que siempre dice la VERDAD."
            : "Tu rol secreto: eres el guardia que siempre MIENTE. Debes responder lo contrario de la verdad.");
        sb.AppendLine(
            truthful
                ? "Contesta la pregunta del prisionero de forma directa y breve."
                : "Para mentir bien: primero piensa la respuesta verdadera y luego responde lo opuesto.");
        sb.AppendLine("No reveles tu rol ni expliques reglas internas.");
        sb.AppendLine();
        sb.AppendLine("Pregunta del prisionero:");
        sb.AppendLine(playerQuestion.Trim());
        sb.AppendLine();
        sb.AppendLine("Da solo la respuesta del guardia en 1-2 frases como maximo.");
        return sb.ToString();
    }

    private void ResolveDoorChoice(bool chooseLeft)
    {
        LeftDoorButton.IsEnabled = false;
        RightDoorButton.IsEnabled = false;
        var win = chooseLeft == _leftDoorFreedom;
        ResultLine.Text = win
            ? "El cerrojo cede y respiras aire libre: elegiste la puerta correcta. Has sobrevivido."
            : "Cruje la puerta equivocada y el consejo guarda silencio: has caido en la condena.";
        StatusLine.Text = "Ronda resuelta. Puedes iniciar una nueva ronda.";
    }

    private void LeftDoorClick(object sender, RoutedEventArgs e) => ResolveDoorChoice(chooseLeft: true);

    private void RightDoorClick(object sender, RoutedEventArgs e) => ResolveDoorChoice(chooseLeft: false);

    private void NewRoundClick(object sender, RoutedEventArgs e) => SetupRound();

    private void BackClick(object sender, RoutedEventArgs e) => Shell?.ShowGameModes();

    private static void ShuffleInPlace<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
