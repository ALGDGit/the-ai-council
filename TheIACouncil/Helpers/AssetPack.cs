using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TheIACouncil;

/// <summary>
/// PNG embebidos vía <c>&lt;Resource Include="Assets\**\*" /&gt;</c>.
/// URI correcta en WPF: <c>pack://application:,,,/NombreEnsamblado;component/Assets/ruta</c>.
/// </summary>
public static class AssetPack
{
    private static readonly string AssemblyName =
        typeof(AssetPack).Assembly.GetName().Name ?? "TheIACouncil";

    public static BitmapImage? TryLoad(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').TrimStart('/');
        var packWithComponent = $"pack://application:,,,/{AssemblyName};component/Assets/{path}";
        var packShort = $"pack://application:,,,/Assets/{path}";

        foreach (var pack in new[] { packWithComponent, packShort })
        {
            var img = TryLoadFromPackUri(pack);
            if (img != null)
                return img;
        }

        return null;
    }

    private static BitmapImage? TryLoadFromPackUri(string packUri)
    {
        try
        {
            var uri = new Uri(packUri, UriKind.Absolute);
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = uri;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapImage? TryPickRandomExisting(params string[] relativePaths)
    {
        var found = new List<BitmapImage>();
        foreach (var p in relativePaths)
        {
            var img = TryLoad(p);
            if (img != null)
                found.Add(img);
        }

        if (found.Count == 0)
            return null;
        return found[Random.Shared.Next(found.Count)];
    }

    /// <summary>
    /// Transición al pulsar «Jugar»: una PNG al azar que exista bajo <c>Assets/Scenes</c>.
    /// </summary>
    public static BitmapImage? TryRandomPlayTransitionFromScenes()
    {
        var candidates = new List<string>();
        for (var i = 1; i <= 99; i++)
        {
            candidates.Add($"Scenes/council-{i:D2}.png");
            candidates.Add($"Scenes/council-{i}.png");
        }

        foreach (var extra in new[]
                 {
                     "Scenes/opening_1.png",
                     "Scenes/opening_2.png",
                     "Scenes/council_opening.png",
                     "Scenes/council_mesa.png"
                 })
            candidates.Add(extra);

        var imgs = new List<BitmapImage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var img = TryLoad(rel);
            if (img == null) continue;
            var key = img.UriSource?.AbsoluteUri ?? rel;
            if (seen.Add(key))
                imgs.Add(img);
        }

        if (imgs.Count == 0)
            return null;
        return imgs[Random.Shared.Next(imgs.Count)];
    }

    private static BitmapImage? TryRandomVerdictAsset(string filePrefix)
    {
        var paths = new string[5];
        for (var i = 0; i < 5; i++)
            paths[i] = $"Verdict/{filePrefix}-{i + 1:D2}.png";
        return TryPickRandomExisting(paths);
    }

    /// <summary>Mayoría SÍ: <c>Verdict/yes-01.png</c> … <c>yes-05.png</c> al azar.</summary>
    public static BitmapImage? TryVerdictSceneApproval() => TryRandomVerdictAsset("yes");

    /// <summary>Mayoría NO: <c>Verdict/no-01.png</c> … <c>no-05.png</c> al azar.</summary>
    public static BitmapImage? TryVerdictSceneRejection() => TryRandomVerdictAsset("no");

    /// <summary>Sin mayoría clara: <c>Verdict/fog-01.png</c> … <c>fog-05.png</c> al azar.</summary>
    public static BitmapImage? TryVerdictSceneChaos() => TryRandomVerdictAsset("fog");

    public static BitmapImage? TryMonkPortrait(int seatIndex1Based)
    {
        var i = seatIndex1Based;
        string[] candidates =
        [
            $"Monks/monk-{i:D2}.png",
            $"Monks/monk-{i}.png",
            $"Monks/{i}.png",
            $"Monks/monk_{i:D2}.png",
            $"Monks/monk{i}.png",
            $"Monks/{i:D2}.png"
        ];

        foreach (var c in candidates)
        {
            var img = TryLoad(c);
            if (img != null)
                return img;
        }

        return null;
    }

    /// <summary>
    /// Todas las imágenes que existen bajo convenciones monk-01… (hasta 99).
    /// </summary>
    public static IReadOnlyList<BitmapImage> EnumerateKnownMonkPortraits()
    {
        var list = new List<BitmapImage>();
        for (var i = 1; i <= 99; i++)
        {
            var img = TryMonkPortrait(i);
            if (img != null)
                list.Add(img);
        }

        return list;
    }

    /// <summary>
    /// Un retrato aleatorio por cada asiento del consejo; baraja el pool de monjes.
    /// Si hay menos PNGs que hermanos, se reutilizan en orden aleatorio.
    /// </summary>
    public static ImageSource?[] AssignRandomMonkPortraits(int councilSize)
    {
        var result = new ImageSource?[councilSize];
        if (councilSize <= 0)
            return result;

        var pool = EnumerateKnownMonkPortraits().ToList();
        if (pool.Count == 0)
            return result;

        var shuffled = pool.OrderBy(_ => Random.Shared.Next()).ToArray();
        for (var i = 0; i < councilSize; i++)
            result[i] = shuffled[i % shuffled.Length];

        return result;
    }

    public static BitmapImage? TryAvatar(int councilIndex1Based) =>
        TryMonkPortrait(councilIndex1Based);
}
