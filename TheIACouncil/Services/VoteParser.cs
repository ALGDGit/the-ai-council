using System.Text.RegularExpressions;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public static class VoteParser
{
    private static readonly Regex RxVoteOnlyLine =
        new(@"^\s*(SÍ|SI|NO|YES|S|N|Y)\s*[\.\!\?…]*\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RxExplicitPhrase =
        new(
            @"(?:(?:mi\s+)?(?:voto|respuesta|veredicto|digo|decido)\s*(?:es|sería|seria)?|(?:me\s+inclino\s+(?:más\s+)?por))\s*[:\s]+\s*(SÍ|SI|NO)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool? ParseYesNo(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        var lines = t.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Líneas que son solo SÍ/NO: la última gana (p. ej. "Sí." y luego "NO" en otra línea).
        bool? lastVoteOnly = null;
        foreach (var line in lines)
        {
            var ln = line.Trim();
            var m = RxVoteOnlyLine.Match(ln);
            if (m.Success)
                lastVoteOnly = MapVoteWord(m.Groups[1].Value);
        }

        if (lastVoteOnly != null)
            return lastVoteOnly;

        var ph = RxExplicitPhrase.Match(t);
        if (ph.Success)
            return MapVoteWord(ph.Groups[1].Value);

        var firstLine = lines.Length > 0 ? lines[0].Trim() : t;
        var head = ParseFirstTokenVote(firstLine);
        if (head != null)
            return head;

        if (lines.Length > 1)
        {
            var last = lines[^1].Trim();
            if (last.Length <= 40)
            {
                var tail = ParseFirstTokenVote(last);
                if (tail != null)
                    return tail;
            }
        }

        return null;
    }

    private static bool? ParseFirstTokenVote(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var s = line.Trim();
        s = Regex.Replace(s, @"^[^\p{L}]+", "");
        if (s.Length == 0)
            return null;

        var m = Regex.Match(s, @"^(?<t>\p{L}+)", RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        var token = m.Groups["t"].Value;
        var tl = token.ToLowerInvariant();
        var after = s.AsSpan(m.Index + m.Length).TrimStart();

        if (tl is "sí" or "yes" || tl == "y" && token.Length == 1)
            return true;

        if (tl == "si")
        {
            if (token == "SÍ")
                return true;
            if (after.Length > 0 && after[0] is ',' or '.' or ';' or ':')
                return true;
            if (after.Length == 0)
                return true;
            return null;
        }

        if (tl == "s" && token.Length == 1)
            return true;

        if (tl == "no")
        {
            if (after.Length == 0)
                return false;
            if (char.IsPunctuation(after[0]))
                return false;
            return null;
        }

        if (tl.StartsWith("affirm", StringComparison.Ordinal))
            return true;
        if (tl.StartsWith("neg", StringComparison.Ordinal))
            return false;

        return null;
    }

    private static bool? MapVoteWord(string word)
    {
        var w = word.Trim().ToLowerInvariant();
        return w switch
        {
            "sí" or "si" or "yes" or "s" or "y" => true,
            "no" or "n" => false,
            _ => null
        };
    }

    public static CouncilVote ToVote(string brotherName, string monkMote, string raw) =>
        new()
        {
            BrotherName = brotherName,
            MonkMote = monkMote,
            RawAnswer = raw.Trim(),
            IsYes = ParseYesNo(raw)
        };
}
