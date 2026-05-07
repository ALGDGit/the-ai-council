using System.Collections.Generic;
using System.Linq;
using TheIACouncil.Services;

namespace TheIACouncil.Helpers;

/// <summary>Motes graciosos para hermanos de IA en todos los modos. Lista larga para muchos proveedores.</summary>
public static class MonkMotes
{
    /// <summary>Pool principal (sin repetir en una misma partida hasta agotarlo).</summary>
    public static readonly string[] Pool =
    [
        "Potatus", "Fray Byte", "Fray Cachete", "San Tokenio", "Fray Pipote", "Abad Ping",
        "Fray Latencio", "Fray Query", "San Buffero", "Fray Canelón", "Fray Scriptus", "Fray Chispas",
        "Hermano Parchmento", "Fray Promptín", "Fray Catódico", "Fray Troncho", "San Latigón",
        "Fray Tostado", "Fray Cifron", "Fray Macro", "Fray Nullpointer", "San Overfito",
        "Fray Depurín", "Abad Stacktrace", "Fray Kernel", "Hermano Segfault", "Fray Regex",
        "San Deploy", "Fray Dockerino", "Fray Kubernetes", "Hermano YAML", "Fray JSONata",
        "Fray Markdown", "San API", "Fray Endpoint", "Abad Webhook", "Fray Middleware",
        "Hermano Latencia", "Fray Timeout", "San Retry", "Fray Backoff", "Fray Throttle",
        "Fray Quota", "Hermano RateLimit", "Fray Cache", "San Redis", "Fray Memoria",
        "Fray Garbage", "Hermano Heap", "Fray Stack", "San Mutex", "Fray Deadlock",
        "Fray Race", "Hermano Thread", "Fray Async", "San Await", "Fray Promise",
        "Fray Callback", "Hermano Closure", "Fray Lambda", "San Functor", "Fray Monad",
        "Fray Tensor", "Hermano Neurona", "Fray Embedding", "San Attention", "Fray LoRA",
        "Fray Quantus", "Hermano FP16", "Fray FP8", "San GPU", "Fray CUDA",
        "Fray Triton", "Hermano Kernelito", "Fray Shader", "San Vertex", "Fray Fragment",
        "Fray Parchment", "Hermano Wax", "Fray Seal", "San Ink", "Fray Quill",
        "Fray Illumina", "Hermano Scriptor", "Fray Codex", "San Palimpsesto", "Fray Marginalia",
        "Fray Glossa", "Hermano Rubrica", "Fray Miniatum", "San Capitular", "Fray Lombard",
        "Fray Bastón", "Hermano Claustral", "Fray Refectorio", "San Celda", "Fray Claustro",
        "Fray Campanero", "Hermano Misa", "Fray Vísperas", "San Completas", "Fray Salmodia",
        "Fray Incensario", "Hermano Turíbulo", "Fray Naveta", "San Acólito", "Fray Diácono",
        "Fray Presbítero", "Hermano Abacial", "Fray Prior", "San Subprior", "Fray Cillerario",
        "Fray Hospitalero", "Hermano Ostiario", "Fray Lector", "San Exorcista", "Fray Confesor",
        "Fray Penitente", "Hermano Flagelante", "Fray Disciplinante", "San Mortificación", "Fray Ayuno",
        "Fray Vigilia", "Hermano Insomne", "Fray Ronquido", "San Siesta", "Fray Jetlag",
        "Fray ZonaHoraria", "Hermano UTC", "Fray GMT", "San DST", "Fray Cronómetro",
        "Fray RelojArena", "Hermano Cuco", "Fray Pendulo", "San Esfera", "Fray Engranaje",
        "Fray Muelle", "Hermano Resorte", "Fray Tornillo", "San Tuerca", "Fray LlaveInglesa",
        "Fray Destornillador", "Hermano Martillo", "Fray Yunque", "San Bigornia", "Fray Yunquito",
        "Fray Pergamino2", "Hermano PDF", "Fray EPUB", "San MOBI", "Fray Kindle",
        "Fray Audiolibro", "Hermano Podcast", "Fray RSS", "San Atom", "Fray XML",
        "Fray SOAP", "Hermano REST", "Fray GraphQL", "San gRPC", "Fray WebSocket",
        "Fray MQTT", "Hermano CoAP", "Fray FTP", "San SFTP", "Fray SCP",
        "Fray SSH", "Hermano GPG", "Fray TLS", "San Certificado", "Fray CA",
        "Fray CSR", "Hermano PEM", "Fray DER", "San PKCS", "Fray OIDC",
        "Fray OAuth", "Hermano JWT", "Fray JWE", "San JWS", "Fray SAML",
        "Fray LDAP", "Hermano AD", "Fray Kerberos", "San NTLM", "Fray BasicAuth",
        "Fray Digest", "Hermano Bearer", "Fray Cookie", "San Session", "Fray LocalStorage",
        "Fray IndexedDB", "Hermano WebSQL", "Fray SQLite", "San Postgres", "Fray MySQL",
        "Fray MariaDB", "Hermano Mongo", "Fray Redisito", "San Cassandra", "Fray Dynamo",
        "Fray Cosmos", "Hermano Bigtable", "Fray Spanner", "San Cockroach", "Fray TiDB",
        "Fray ClickHouse", "Hermano DuckDB", "Fray Polars", "San Pandas", "Fray NumPy",
        "Fray SciPy", "Hermano Matplotlib", "Fray Seaborn", "San Plotly", "Fray D3",
        "Fray Vega", "Hermano ggplot", "Fray Tableau", "San PowerBI", "Fray Looker",
        "Fray Metabase", "Hermano Superset", "Fray Grafana", "San Prometheus", "Fray Loki",
        "Fray Tempo", "Hermano Jaeger", "Fray Zipkin", "San OpenTelemetry", "Fray OTel",
        "Fray SLO", "Hermano SLI", "Fray SLA", "San ErrorBudget", "Fray BurnRate",
        "Fray Toil", "Hermano Runbook", "Fray Playbook", "San Postmortem", "Fray Blameless",
        "Fray Retro", "Hermano Kaizen", "Fray PDCA", "San Deming", "Fray SixSigma",
        "Fray Lean", "Hermano Agile", "Fray Scrum", "San Kanban", "Fray XP",
        "Fray Crystal", "Hermano DSDM", "Fray SAFe", "San LeSS", "Fray Nexus",
        "Fray Spotify", "Hermano Tribu", "Fray Chapter", "San Guild", "Fray Squad",
        "Fray Tribe", "Hermano COE", "Fray PMO", "San Steering", "Fray CAB",
        "Fray ITIL", "Hermano COBIT", "Fray ISO27k", "San SOC2", "Fray HIPAA",
        "Fray GDPR", "Hermano CCPA", "Fray LGPD", "San PIPEDA", "Fray PDPA",
        "Fray Schrems", "Hermano Privacy", "Fray Consent", "San LegitimateInterest", "Fray DPIA",
        "Fray ROPA", "Hermano DSR", "Fray Breach", "San Notification", "Fray Fine",
        "Fray DPA", "Hermano SCC", "Fray BCR", "San TIA", "Fray SCCs",
        "Fray Adequacy", "Hermano Shield", "Fray PrivacyShield", "San MaxSchrems", "Fray CloudAct",
        "Fray FISA", "Hermano EO12333", "Fray PATRIOT", "San CALEA", "Fray ECPA",
        "Fray SCA", "Hermano CFAA", "Fray DMCA", "San GDPRAgain", "Fray EnoughLaw",
        "Fray VolverMonje", "Hermano Silencio", "Fray Eco", "San EcoEco", "Fray Eco³",
        "Fray Sopapa", "San Nocilla", "Hermano Torrezno", "Fray Migas", "Abad Fabada",
        "Fray Chorizo", "San Morcilla", "Hermano Butifarra", "Fray Chuletón", "Fray Lenteja",
        "San Garbanzo", "Hermano Cocido", "Fray Puchero", "Abad Olla", "Fray Caldo",
        "Fray Galleta", "San Mosto", "Hermano Sidrón", "Fray Orujo", "Fray Anís",
        "San Resoli", "Hermano Licor", "Fray Digestivo", "Abad Brandy", "Fray Cognac",
        "Fray Hipérbole", "San Metáfora", "Hermano Símil", "Fray Parábola", "Fray Alegoría",
        "San Sínodo", "Hermano Concilio", "Fray Cónclave", "Abad Capítulo", "Fray Cabildo",
        "Fray Votación", "San Balota", "Hermano Papeleta", "Fray Abstención", "Fray Veto",
        "San Veto²", "Hermano Filiberto", "Fray Filibuster", "Abad Quórum", "Fray Quorumín",
        "Fray Matica", "San Semántica", "Hermano Sintaxis", "Fray Morfema", "Fray Fonema",
        "San Dígrafo", "Hermano Trígrafo", "Fray Umlaut", "Abad Cedilla", "Fray Tilde",
        "Fray Diéresis", "San Eñe", "Hermano Virgulilla", "Fray Ortografía", "Fray Prosodia",
        "San Métrica", "Hermano Pentámetro", "Fray Hexámetro", "Abad Alejandrino", "Fray Sáfico",
        "Fray Cateto", "San Hipotenusa", "Hermano Coseno", "Fray Tangente", "Fray Arcotangente",
        "San Logaritmo", "Hermano Exponencial", "Fray Factorial", "Abad Combinatoria", "Fray Permuta",
        "Fray Deriva", "San Integral", "Hermano Límite", "Fray Epsilon", "Fray Delta",
        "San Sigma", "Hermano Omega", "Fray Alfa", "Abad Beta", "Fray Gamma",
        "Fray Nabla", "San Gradiente", "Hermano Divergencia", "Fray Rotacional", "Fray Laplaciano",
        "San Fourier", "Hermano Laplace", "Fray Gauss", "Abad Euler", "Fray Newton",
        "Fray Leibniz", "San Cantor", "Hermano Dedekind", "Fray Hilbert", "Fray Gödel",
        "San Turing", "Hermano Church", "Fray Lambda²", "Abad Curry", "Fray Howard",
        "Fray Wombat", "San Quokka", "Hermano Capybara", "Fray Nutria", "Fray Ganso",
        "San Ganso²", "Hermano Pato", "Fray Cisne", "Abad Flamenco", "Fray Pelícano",
        "Fray Murciélago", "San Buho", "Hermano Mochuelo", "Fray Lechuza", "Fray Autillo",
        "San Grillo", "Hermano Saltamontes", "Fray Chapulín", "Abad Abejorro", "Fray Zangano",
        "Fray Hormiga", "San Termita", "Hermano Ciempiés", "Fray Milpiés", "Fray Cochinilla",
        "San Caracol", "Hermano Babosa", "Fray Nudibranquio", "Abad Cefalópodo", "Fray Krakenito",
        "Fray Sirenio", "San Tritón", "Hermano Poseidón", "Fray Neptuno", "Fray Atlante",
        "San Columba", "Hermano Perseo", "Fray Orión", "Abad Cassiopea", "Fray Andrómeda",
        "Fray Quasar", "San Púlsar", "Hermano Agujero", "Fray Singularidad", "Fray MateriaOscura",
        "San EnergíaOscura", "Hermano Planck", "Fray Bohr", "Abad Heisenberg", "Fray Schrödinger",
        "Fray Gato", "San Caja", "Hermano Veneno", "Fray Antídoto", "Fray Placebo",
        "San DobleCiego", "Hermano Control", "Fray Placebicina", "Abad RCT", "Fray pValor",
        "Fray Significancia", "San Intervalo", "Hermano Bootstrap", "Fray Jackknife", "Fray Bayes",
        "San Apriori", "Hermano Posteriori", "Fray Verosimilitud", "Abad MCMC", "Fray HMC",
        "Fray Viñeta", "San Meme", "Hermano GIF", "Fray Sticker", "Fray Emoji",
        "San Unicode", "Hermano UTF8", "Fray UTF16", "Abad Codepoint", "Fray Grapheme",
        "Fray Normalización", "San NFC", "Hermano NFD", "Fray NFKC", "Fray NFKD"
    ];

    public static string MemberKey(ILLMClient c) =>
        $"{c.BrotherName}\u001F{c.ModelId}\u001F{c.ProviderLabel}";

    /// <summary>Asigna motes únicos a hermanos nuevos del consejo; conserva los ya registrados.</summary>
    public static void RegisterForCouncil(
        IReadOnlyList<ILLMClient> council,
        Dictionary<string, string> registry,
        ref int anonCounter)
    {
        var used = new HashSet<string>(registry.Values, StringComparer.Ordinal);
        var deck = Pool.OrderBy(_ => Random.Shared.Next()).ToList();
        var deckIdx = 0;

        foreach (var client in council)
        {
            var k = MemberKey(client);
            if (registry.ContainsKey(k))
                continue;

            while (deckIdx < deck.Count && used.Contains(deck[deckIdx]))
                deckIdx++;

            string m;
            if (deckIdx < deck.Count)
            {
                m = deck[deckIdx++];
                used.Add(m);
            }
            else
                m = $"Fray Soporte {++anonCounter}";

            registry[k] = m;
        }
    }

    public static string[] MotesInOrder(IReadOnlyList<ILLMClient> council, Dictionary<string, string> registry) =>
        council.Select(c => registry[MemberKey(c)]).ToArray();

    /// <summary>Motes aleatorios únicos (impostor, acertijo nueva ronda, etc.).</summary>
    public static string[] AssignUniqueRandom(int count)
    {
        var deck = Pool.OrderBy(_ => Random.Shared.Next()).ToList();
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = i < deck.Count ? deck[i] : $"Fray Numerario {i + 1}";
        return result;
    }
}
