using System.Text;
using TheIACouncil.Helpers;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class CouncilGameService
{
    /// <summary>
    /// Cadena deliberativa:
    /// - 1º: personalidad + pregunta.
    /// - 2º en adelante: personalidad + pregunta + opiniones previas acumuladas.
    /// </summary>
    private static void AppendDeliberationPrompt(
        StringBuilder sb,
        string question,
        IReadOnlyList<CouncilTurn> priorTurns,
        ILLMClient client,
        string monkMote)
    {
        var personality = BrotherPersonalityCatalog.GetPromptInstruction(client.PersonalityId);
        var q = question.Trim();

        sb.AppendLine("Responde en ESPANOL.");
        sb.AppendLine(
            $"Tu mote en el consejo es \"{monkMote}\" (habla como monje; no menciones proveedores ni nombres tecnicos de modelo).");
        sb.AppendLine(
            $"Imagina que eres este perfil: {personality}.");
        sb.AppendLine(
            $"Te lanzan esta pregunta: {q}");
        sb.AppendLine(
            "Razona si estas a favor o en contra en 100 palabras o menos.");

        if (priorTurns.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Esto es lo que opinan otros antes que tu (usa sus motes, no nombres de modelo):");
            foreach (var t in priorTurns)
                sb.AppendLine($"- {t.MonkMote}: {t.Paragraph.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("Entrega SOLO un parrafo final, sin listas ni encabezados.");
    }

    public async Task<CouncilResult> RunAsync(
        IReadOnlyList<ILLMClient> council,
        IReadOnlyList<string> monkMotes,
        string question,
        int maxConcurrentLlmRequests,
        IProgress<CouncilProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (council.Count == 0)
            throw new InvalidOperationException("No hay hermanos del consejo activados. Ve a Configuración.");
        if (monkMotes.Count != council.Count)
            throw new ArgumentException("Debe haber un mote por cada hermano del consejo.", nameof(monkMotes));

        var turns = new List<CouncilTurn>();
        var q = question.Trim();

        for (var i = 0; i < council.Count; i++)
        {
            var client = council[i];
            var mote = monkMotes[i];
            progress?.Report(new CouncilProgress(client.BrotherName,
                ReasoningLog: $"{mote} reflexiona…"));

            var sb = new StringBuilder();
            AppendDeliberationPrompt(sb, q, turns, client, mote);

            var answer = await client.CompleteAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);
            var turn = new CouncilTurn
            {
                MonkMote = mote,
                BrotherName = client.BrotherName,
                PersonalityLabel = BrotherPersonalityCatalog.GetDisplayName(client.PersonalityId),
                ProviderLabel = client.ProviderLabel,
                ModelId = client.ModelId,
                Paragraph = answer
            };
            turns.Add(turn);
            progress?.Report(new CouncilProgress(null,
                DeliberationTurnFinished: turn,
                DeliberationSpeakerIndex: i));
        }

        var voteContext = BuildVotePhasePromptBody(q, turns);

        var voteFactories = new Func<Task<(int Index, CouncilVote Vote)>>[council.Count];
        for (var vi = 0; vi < council.Count; vi++)
        {
            var idx = vi;
            var client = council[idx];
            voteFactories[idx] = () => CastVoteAsync(client, idx);
        }

        var orderedVotes = await LlmConcurrency
            .RunParallelLimitedAsync(maxConcurrentLlmRequests, voteFactories, cancellationToken)
            .ConfigureAwait(false);
        Array.Sort(orderedVotes, static (a, b) => a.Index.CompareTo(b.Index));
        var votes = orderedVotes.Select(v => v.Vote).ToList();

        var yes = votes.Count(v => v.IsYes == true);
        var no = votes.Count(v => v.IsYes == false);
        var unclear = votes.Count(v => v.IsYes is null);

        return new CouncilResult
        {
            Turns = turns,
            Votes = votes,
            YesCount = yes,
            NoCount = no,
            UnclearCount = unclear
        };

        async Task<(int Index, CouncilVote Vote)> CastVoteAsync(ILLMClient client, int idx)
        {
            var mote = monkMotes[idx];
            progress?.Report(new CouncilProgress(client.BrotherName,
                VerdictLog: $"{mote} vota SI/NO…"));

            var votePrompt = new StringBuilder();
            votePrompt.AppendLine(voteContext);
            votePrompt.AppendLine();
            votePrompt.AppendLine("¿Habiendo visto estos argumentos, te inclinas más por el SI o por el NO?");
            votePrompt.AppendLine("No hace falta rotundidad absoluta: elige la tendencia que veas más probable.");
            votePrompt.AppendLine("Responde solo SI o NO en una palabra.");

            var raw = await client.CompleteAsync(votePrompt.ToString(), cancellationToken).ConfigureAwait(false);
            var vote = VoteParser.ToVote(client.BrotherName, mote, raw);
            progress?.Report(new CouncilProgress(null,
                VoteFinished: vote,
                VoteSpeakerIndex: idx));
            return (idx, vote);
        }
    }

    /// <summary>
    /// Contexto para la votación: cada miembro recibe la pregunta y el resumen literal de todas las intervenciones
    /// antes de la pregunta rotunda SI/NO.
    /// </summary>
    private static string BuildVotePhasePromptBody(string question, IReadOnlyList<CouncilTurn> turns)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Tu voto debe basarse en lo siguiente: primero la pregunta del peregrino; después, el resumen completo de lo que cada IA del consejo ha dicho.");
        sb.AppendLine(
            "Lee todo el bloque antes de responder. No votes hasta haber leído cada intervención.");
        sb.AppendLine();
        sb.AppendLine("PREGUNTA:");
        sb.AppendLine(question.Trim());
        sb.AppendLine();
        sb.AppendLine("RESUMEN DE LAS INTERVENCIONES DEL CONSEJO (todas las IAs ya han hablado):");
        sb.AppendLine();
        foreach (var t in turns)
        {
            sb.AppendLine($"{t.MonkMote} (IA: {t.ProviderLabel} · {t.ModelId}) piensa esto:");
            sb.AppendLine(t.Paragraph.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("─── Fin del resumen — ahora emite tu veredicto rotundo ───");
        return sb.ToString().TrimEnd();
    }

}
