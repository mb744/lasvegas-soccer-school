using System.Text.RegularExpressions;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Best-effort interpretation of a parent's free-text reply as an attendance answer. Handles
/// English + Spanish keywords and a wide net of positive emoji parents commonly send to a
/// confirmation request (checkmarks, thumbs up, soccer ball, peace/two-finger, OK hand, flex,
/// crossed fingers, fire, party, hearts, sparkles, claps, etc.). Same conservative tie-break:
/// returns <c>null</c> when there's no clear signal or the reply contains both yes and no cues,
/// so the admin's manual call is never overridden by a guess.
/// </summary>
public static class AttendanceReplyParser
{
    // A positive emoji of any kind reads as "we'll be there" on a game/practice/tournament
    // confirmation reply. Parents often skip text entirely and send just a single emoji —
    // most often the soccer ball, but also thumbs up, peace, OK hand, fire, hearts, etc.
    // The U+FE0F variation-selector forms are listed alongside the bare codepoints because
    // many keyboards substitute one for the other.
    private static readonly string[] YesEmoji =
    {
        // Checkmarks / confirms
        "✅", "✔", "✔️", "☑", "☑️",
        // Hands & gestures
        "👍", "👍🏻", "👍🏼", "👍🏽", "👍🏾", "👍🏿",
        "🙌", "🙌🏻", "🙌🏼", "🙌🏽", "🙌🏾", "🙌🏿",
        "👏", "👏🏻", "👏🏼", "👏🏽", "👏🏾", "👏🏿",
        "✌", "✌️",                       // peace / two-finger
        "👌", "👌🏻", "👌🏼", "👌🏽", "👌🏾", "👌🏿", // OK hand
        "💪", "💪🏻", "💪🏼", "💪🏽", "💪🏾", "💪🏿", // flex
        "🤙", "🤙🏻", "🤙🏼", "🤙🏽", "🤙🏾", "🤙🏿", // shaka
        "🤞", "🤞🏻", "🤞🏼", "🤞🏽", "🤞🏾", "🤞🏿", // crossed fingers
        "🙏", "🙏🏻", "🙏🏼", "🙏🏽", "🙏🏾", "🙏🏿", // folded hands
        // Sports / energy
        "⚽", "⚽️", "🏆", "🥅", "🥇",
        "🔥", "⚡", "⚡️", "💯", "🎉", "🎊", "🚀", "💥",
        // Affection / approval
        "❤", "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎",
        "💖", "💝", "💗", "💓", "💞", "💕", "😍", "🥰", "😘", "🤗",
        "😀", "😁", "😃", "😄", "😊", "🙂",
        // Sparkles / stars
        "⭐", "⭐️", "🌟", "✨",
    };
    private static readonly string[] NoEmoji = { "❌", "✖", "✖️", "✗", "👎", "👎🏻", "👎🏼", "👎🏽", "👎🏾", "👎🏿", "🚫", "⛔", "🛑", "😢", "😭", "😞", "😔" };
    private static readonly string[] MaybeEmoji = { "🤔", "❓", "❔", "🤷", "🤷‍♀️", "🤷‍♂️" };

    // Whole-word tokens (matched between spaces after stripping punctuation/emoji).
    private static readonly string[] YesWords = { "yes", "yep", "yeah", "yup", "si", "sí", "ok", "okay", "claro", "voy", "vamos", "asisto", "asistimos" };
    private static readonly string[] NoWords = { "no", "nope", "cant", "cannot", "wont" };
    private static readonly string[] MaybeWords = { "maybe", "posible" };

    // Substring phrases (multi-word or apostrophe forms; "confirm" also covers confirmed/confirmado).
    private static readonly string[] YesPhrases = { "confirm", "asistir", "we'll be there", "see you there", "count us in" };
    private static readonly string[] NoPhrases = { "can't", "won't", "can not", "not coming", "no puedo", "no podemos", "no vamos", "no asist" };
    private static readonly string[] MaybePhrases = { "tal vez", "quiz", "not sure", "puede ser", "no estoy seguro" };

    public static AttendanceStatus? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        var lower = body.ToLowerInvariant();
        // Letters + spaces only, padded, so whole-word checks don't match inside larger words
        // ("no" must not match "now"/"know"). \p{L} keeps accented letters (sí, quizás).
        var words = " " + Regex.Replace(lower, @"[^\p{L}\s]", " ") + " ";

        bool HasWord(string[] list) => list.Any(w => words.Contains($" {w} "));
        bool HasPhrase(string[] list) => list.Any(p => lower.Contains(p));
        bool HasEmoji(string[] list) => list.Any(e => body.Contains(e, StringComparison.Ordinal));

        var yes = HasEmoji(YesEmoji) || HasWord(YesWords) || HasPhrase(YesPhrases);
        var no = HasEmoji(NoEmoji) || HasWord(NoWords) || HasPhrase(NoPhrases);
        var maybe = HasEmoji(MaybeEmoji) || HasWord(MaybeWords) || HasPhrase(MaybePhrases);

        if (yes && no) return null;          // conflicting — leave it for the admin
        if (yes) return AttendanceStatus.Confirmed;
        if (no) return AttendanceStatus.Declined;
        if (maybe) return AttendanceStatus.Maybe;
        return null;
    }
}
