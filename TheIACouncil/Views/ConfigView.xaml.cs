using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using TheIACouncil.Models;
using TheIACouncil.Services;
using TheIACouncil.Windows;

namespace TheIACouncil.Views;

public partial class ConfigView : UserControl
{
    private AppSettings _settings = new();

    /// <summary>Nombres devueltos por el último GET /api/tags en esta sesión.</summary>
    private IReadOnlyList<string>? _lastProbeModelNames;

    public ConfigView()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private void Reload()
    {
        _settings = App.Config.Load();

        foreach (var cb in PersonalityComboBoxes())
            FillPersonalityCombo(cb);

        var openAi = Get(ProviderKind.OpenAI);
        OpenAiEnabled.IsChecked = openAi.Enabled;
        OpenAiKey.Text = openAi.ApiKey;
        FillModelCombo(OpenAiModelCombo, ProviderKind.OpenAI, openAi.Model);
        OpenAiBase.Text = openAi.BaseUrl;
        SelectPersonality(OpenAiPersonality, openAi.PersonalityId);

        var anth = Get(ProviderKind.Anthropic);
        AnthropicEnabled.IsChecked = anth.Enabled;
        AnthropicKey.Text = anth.ApiKey;
        FillModelCombo(AnthropicModelCombo, ProviderKind.Anthropic, anth.Model);
        AnthropicBase.Text = anth.BaseUrl;
        SelectPersonality(AnthropicPersonality, anth.PersonalityId);

        var gem = Get(ProviderKind.GoogleGemini);
        GeminiEnabled.IsChecked = gem.Enabled;
        GeminiKey.Text = gem.ApiKey;
        FillModelCombo(GeminiModelCombo, ProviderKind.GoogleGemini, gem.Model);
        GeminiBase.Text = gem.BaseUrl;
        SelectPersonality(GeminiPersonality, gem.PersonalityId);

        var grok = Get(ProviderKind.Grok);
        GrokEnabled.IsChecked = grok.Enabled;
        GrokKey.Text = grok.ApiKey;
        FillModelCombo(GrokModelCombo, ProviderKind.Grok, grok.Model);
        GrokBase.Text = grok.BaseUrl;
        SelectPersonality(GrokPersonality, grok.PersonalityId);

        var mistral = Get(ProviderKind.Mistral);
        MistralEnabled.IsChecked = mistral.Enabled;
        MistralKey.Text = mistral.ApiKey;
        FillModelCombo(MistralModelCombo, ProviderKind.Mistral, mistral.Model);
        MistralBase.Text = mistral.BaseUrl;
        SelectPersonality(MistralPersonality, mistral.PersonalityId);

        var ollama = Get(ProviderKind.Ollama);
        OllamaEnabled.IsChecked = ollama.Enabled;
        OllamaBase.Text = ollama.BaseUrl;
        SelectPersonality(OllamaPersonality, ollama.PersonalityId);
        OllamaProbeStatus.Text = "";
        MaxConcurrentLlmBox.Text = _settings.MaxConcurrentLlmRequests.ToString();
        RebuildOllamaModelPanel();
    }

    private static void FillModelCombo(ComboBox combo, ProviderKind kind, string savedModel)
    {
        combo.Items.Clear();
        var catalog = KnownModelCatalog.For(kind);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        var m = savedModel.Trim();
        if (!string.IsNullOrEmpty(m) &&
            !catalog.Any(x => string.Equals(x, m, StringComparison.OrdinalIgnoreCase)))
        {
            ordered.Add(m);
            seen.Add(m);
        }

        foreach (var id in catalog)
        {
            if (seen.Add(id))
                ordered.Add(id);
        }

        foreach (var id in ordered)
            combo.Items.Add(id);

        foreach (var item in combo.Items)
        {
            if (item is string s && string.Equals(s, m, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string ComboModelId(ComboBox combo) =>
        combo.SelectedItem as string ?? "";

    private void RebuildOllamaModelPanel()
    {
        OllamaModelsPanel.Children.Clear();

        var ollama = Get(ProviderKind.Ollama);
        var saved = new HashSet<string>(ollama.OllamaModels, StringComparer.OrdinalIgnoreCase);
        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in saved)
            union.Add(x);
        if (_lastProbeModelNames != null)
        {
            foreach (var x in _lastProbeModelNames)
                union.Add(x);
        }

        if (union.Count == 0)
        {
            var hint = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Fg.Muted"),
                Text =
                    "Marca primero «Actualizar modelos locales» para cargar los modelos que reporta Ollama. Si ya guardaste selección antes, vuelve a actualizar para ver la lista completa."
            };
            OllamaModelsPanel.Children.Add(hint);
            return;
        }

        foreach (var name in union.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var row = new Grid { Tag = name, Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            var cb = new CheckBox
            {
                Content = name,
                Tag = name,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = saved.Contains(name),
                ToolTip = name
            };
            Grid.SetColumn(cb, 0);

            var persCb = new ComboBox
            {
                IsEditable = false,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 200,
                ToolTip = "Voz y carácter de este hermano en el consejo"
            };
            FillPersonalityCombo(persCb);
            var pid = ollama.OllamaPersonalities.TryGetValue(name, out var savedP) &&
                        !string.IsNullOrWhiteSpace(savedP)
                ? savedP
                : ollama.PersonalityId;
            SelectPersonality(persCb, pid);
            Grid.SetColumn(persCb, 1);

            row.Children.Add(cb);
            row.Children.Add(persCb);
            OllamaModelsPanel.Children.Add(row);
        }

        ApplyOllamaFilter();
    }

    private void ApplyOllamaFilter()
    {
        var q = (OllamaFilter?.Text ?? "").Trim();
        foreach (var child in OllamaModelsPanel.Children)
        {
            if (child is not Grid row || row.Tag is not string name)
                continue;
            var visible = q.Length == 0 || name.Contains(q, StringComparison.OrdinalIgnoreCase);
            row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OllamaFilter_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyOllamaFilter();

    private List<string> CollectCheckedOllamaModels()
    {
        return OllamaModelsPanel.Children.OfType<Grid>()
            .Where(g => g.Tag is string)
            .Select(g => g.Children.OfType<CheckBox>().FirstOrDefault())
            .Where(cb => cb is { IsChecked: true, Tag: string })
            .Select(cb => (string)cb!.Tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Dictionary<string, string> CollectOllamaPersonalitiesByModel()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in OllamaModelsPanel.Children.OfType<Grid>())
        {
            if (row.Tag is not string modelName)
                continue;
            var check = row.Children.OfType<CheckBox>().FirstOrDefault();
            if (check?.IsChecked != true)
                continue;
            var pCombo = row.Children.OfType<ComboBox>().FirstOrDefault();
            if (pCombo != null)
                dict[modelName] = SelectedPersonality(pCombo);
        }

        return dict;
    }

    private ProviderConfig Get(ProviderKind kind) =>
        _settings.Providers.First(p => p.Kind == kind);

    private IEnumerable<ComboBox> PersonalityComboBoxes()
    {
        yield return OpenAiPersonality;
        yield return AnthropicPersonality;
        yield return GeminiPersonality;
        yield return GrokPersonality;
        yield return MistralPersonality;
        yield return OllamaPersonality;
    }

    private static void FillPersonalityCombo(ComboBox cb)
    {
        cb.Items.Clear();
        foreach (var p in BrotherPersonalityCatalog.All)
        {
            cb.Items.Add(new ComboBoxItem
            {
                Content = p.DisplayName,
                Tag = p.Id
            });
        }
    }

    private static void SelectPersonality(ComboBox cb, string id)
    {
        var norm = BrotherPersonalityCatalog.NormalizeId(id);
        foreach (ComboBoxItem item in cb.Items)
        {
            if ((string)item.Tag! == norm)
            {
                cb.SelectedItem = item;
                return;
            }
        }

        if (cb.Items.Count > 0)
            cb.SelectedIndex = 0;
    }

    private static string SelectedPersonality(ComboBox cb)
    {
        if (cb.SelectedItem is ComboBoxItem item && item.Tag is string sid)
            return BrotherPersonalityCatalog.NormalizeId(sid);
        return BrotherPersonalityCatalog.DefaultId;
    }

    private void ApplyUiToSettings()
    {
        var rawConc = MaxConcurrentLlmBox.Text.Trim();
        if (int.TryParse(rawConc, out var mc) && mc > 0)
            _settings.MaxConcurrentLlmRequests = Math.Clamp(mc, 1, 32);
        else
            _settings.MaxConcurrentLlmRequests = 3;

        var openAi = Get(ProviderKind.OpenAI);
        openAi.Enabled = OpenAiEnabled.IsChecked == true;
        openAi.ApiKey = OpenAiKey.Text.Trim();
        openAi.Model = ComboModelId(OpenAiModelCombo);
        openAi.BaseUrl = OpenAiBase.Text.Trim();
        openAi.PersonalityId = SelectedPersonality(OpenAiPersonality);

        var anth = Get(ProviderKind.Anthropic);
        anth.Enabled = AnthropicEnabled.IsChecked == true;
        anth.ApiKey = AnthropicKey.Text.Trim();
        anth.Model = ComboModelId(AnthropicModelCombo);
        anth.BaseUrl = AnthropicBase.Text.Trim();
        anth.PersonalityId = SelectedPersonality(AnthropicPersonality);

        var gem = Get(ProviderKind.GoogleGemini);
        gem.Enabled = GeminiEnabled.IsChecked == true;
        gem.ApiKey = GeminiKey.Text.Trim();
        gem.Model = ComboModelId(GeminiModelCombo);
        gem.BaseUrl = GeminiBase.Text.Trim();
        gem.PersonalityId = SelectedPersonality(GeminiPersonality);

        var grok = Get(ProviderKind.Grok);
        grok.Enabled = GrokEnabled.IsChecked == true;
        grok.ApiKey = GrokKey.Text.Trim();
        grok.Model = ComboModelId(GrokModelCombo);
        grok.BaseUrl = GrokBase.Text.Trim();
        grok.PersonalityId = SelectedPersonality(GrokPersonality);

        var mistral = Get(ProviderKind.Mistral);
        mistral.Enabled = MistralEnabled.IsChecked == true;
        mistral.ApiKey = MistralKey.Text.Trim();
        mistral.Model = ComboModelId(MistralModelCombo);
        mistral.BaseUrl = MistralBase.Text.Trim();
        mistral.PersonalityId = SelectedPersonality(MistralPersonality);

        var ollama = Get(ProviderKind.Ollama);
        ollama.Enabled = OllamaEnabled.IsChecked == true;
        ollama.BaseUrl = OllamaBase.Text.Trim();
        ollama.PersonalityId = SelectedPersonality(OllamaPersonality);
        ollama.OllamaModels = CollectCheckedOllamaModels();
        ollama.OllamaPersonalities = CollectOllamaPersonalitiesByModel();
        if (ollama.OllamaModels.Count > 0)
            ollama.Model = ollama.OllamaModels[0];
    }

    private async void OllamaProbeClick(object sender, RoutedEventArgs e)
    {
        OllamaProbeStatus.Text = "Consultando al daemon…";
        try
        {
            var baseUrl = OllamaBase.Text.Trim();
            var probe = await App.Ollama.ProbeAsync(baseUrl, CancellationToken.None).ConfigureAwait(true);
            if (!probe.DaemonReachable)
            {
                var baseMsg = string.IsNullOrEmpty(probe.Error)
                    ? "No se pudo conectar al daemon de Ollama en esa URL."
                    : probe.Error;
                if (OllamaDetector.TryDetectCliInstalled(out var ver))
                {
                    OllamaProbeStatus.Text =
                        $"{baseMsg} El ejecutable «ollama» responde ({ver}), pero el servicio HTTP no está accesible aquí — revisa la URL o ejecuta el daemon.";
                }
                else
                {
                    OllamaProbeStatus.Text =
                        $"{baseMsg} Si no tienes Ollama instalado, descárgalo en https://ollama.com e inicia un modelo.";
                }

                return;
            }

            _lastProbeModelNames = probe.ModelNames.ToList();
            RebuildOllamaModelPanel();

            OllamaProbeStatus.Text =
                $"Detectados {_lastProbeModelNames.Count} modelos en esta máquina. Marca cuáles participan.";
        }
        catch (Exception ex)
        {
            OllamaProbeStatus.Text = ex.Message;
        }
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyUiToSettings();
            App.Achievements.EvaluateAfterConfigSave(_settings);
            App.Config.Save(_settings);
            NoticeDialog.Show(
                "La configuración quedó grabada en tu scriptorio.",
                "Scriptorio",
                () =>
                {
                    if (Window.GetWindow(this) is MainWindow w)
                        w.ShowMainMenu();
                });
        }
        catch (Exception ex)
        {
            NoticeDialog.Show(ex.Message, "Error");
        }
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w)
            w.ShowMainMenu();
    }

    private void OllamaLink_Click(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
