using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Admin-managed phrase substitution. Not a real translator — it walks the input text and replaces
/// known phrases (longest-match-first, case-insensitive, with word-boundary respect for single
/// words). Anything not in the dictionary stays as the source text, and the admin edits it in the
/// side-by-side preview. The dictionary grows organically as the admin adds new phrases.
/// </summary>
public interface IPhraseTranslator
{
    Task<TranslationOutcome> TranslateAsync(string text, Language from, Language to, CancellationToken ct);
}

public record TranslationOutcome(string Translated, IReadOnlyList<string> MatchedPhrases, bool FullyTranslated);

public class PhraseTranslator : IPhraseTranslator
{
    private readonly AppDbContext _db;

    public PhraseTranslator(AppDbContext db) { _db = db; }

    public async Task<TranslationOutcome> TranslateAsync(string text, Language from, Language to, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text) || from == to)
            return new TranslationOutcome(text, Array.Empty<string>(), true);

        var phrases = await _db.PhraseTranslations.ToListAsync(ct);
        // Longest-match-first so "soccer practice" wins over "practice" alone.
        var ordered = from == Language.English
            ? phrases.OrderByDescending(p => p.English.Length).Select(p => (Src: p.English, Dst: p.Spanish))
            : phrases.OrderByDescending(p => p.Spanish.Length).Select(p => (Src: p.Spanish, Dst: p.English));

        var working = text;
        var matched = new List<string>();
        foreach (var (src, dst) in ordered)
        {
            if (string.IsNullOrWhiteSpace(src)) continue;
            // Word-boundary match for single-word phrases avoids replacing inside larger words
            // ("game" inside "games"). Multi-word phrases match literally, ignoring case.
            var pattern = HasWordChars(src)
                ? $@"\b{Regex.Escape(src)}\b"
                : Regex.Escape(src);
            var rx = new Regex(pattern, RegexOptions.IgnoreCase);
            var replaced = rx.Replace(working, _ => dst);
            if (!ReferenceEquals(replaced, working) && replaced != working)
            {
                matched.Add(src);
                working = replaced;
            }
        }

        // "Fully translated" is approximate: true when nothing alphabetic remains from the source
        // language vocabulary. We approximate by comparing word count: if the result is identical
        // to the source and we found nothing, mark not fully translated.
        var fully = matched.Count > 0 && !string.Equals(working, text, StringComparison.Ordinal);
        return new TranslationOutcome(working, matched, fully);
    }

    private static bool HasWordChars(string s) => Regex.IsMatch(s, @"\w");
}
