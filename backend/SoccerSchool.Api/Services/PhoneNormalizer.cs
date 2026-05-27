namespace SoccerSchool.Api.Services;

/// <summary>
/// Normalizes US phone numbers to E.164 (<c>+1XXXXXXXXXX</c>) for consistent storage and lookup.
/// Strips punctuation (spaces, dashes, parens) before reasoning about length. International numbers
/// or anything we can't safely classify are returned trimmed but otherwise unchanged so they don't
/// get mangled — validation should catch them upstream.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>Normalize to E.164 if the input is a recognizable US number; otherwise return the
    /// trimmed input. Returns the input verbatim when null or empty.</summary>
    public static string? Normalize(string? raw)
    {
        if (raw is null) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return trimmed;

        // Strip everything except + and digits. Keep a leading + only if it's at position 0.
        var hadPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return trimmed;

        if (hadPlus)
        {
            // Already E.164-shaped (or close to it). Trust the user's country code.
            return "+" + digits;
        }
        // No plus sign — try US heuristics. 10 digits is a 10-digit US number; 11 digits starting
        // with 1 is the same with the country code already on. Longer/shorter unknown forms get
        // returned as-is so we don't accidentally convert e.g. a 9-digit typo into something valid.
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits[0] == '1') return "+" + digits;
        return trimmed;
    }

    /// <summary>Common form variants used to look up a phone against records that may have been
    /// stored in different formats historically. Returns the normalized form first.</summary>
    public static IReadOnlyList<string> Variants(string? raw)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        var trimmed = raw.Trim();
        set.Add(trimmed);

        var normalized = Normalize(trimmed);
        if (!string.IsNullOrEmpty(normalized)) set.Add(normalized!);

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
        {
            set.Add(digits);
            if (digits.Length == 11 && digits[0] == '1') set.Add(digits[1..]); // bare 10-digit form
        }
        return set.ToList();
    }
}
