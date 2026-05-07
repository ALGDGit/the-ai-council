namespace TheIACouncil;

public static class BrotherGlyph
{
    public static string Initials(string brotherName)
    {
        var parts = brotherName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var a = char.ToUpperInvariant(parts[0][0]);
            var b = char.ToUpperInvariant(parts[1][0]);
            return $"{a}{b}";
        }

        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0][..2].ToUpperInvariant();
        if (parts.Length == 1 && parts[0].Length == 1)
            return parts[0].ToUpperInvariant();
        return "?";
    }
}
