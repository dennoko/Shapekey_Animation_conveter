using System;

public partial class Shapekey_animation_converter
{
    // Helper methods for AND search
    static string[] BuildSearchTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        var tokens = text.Split(new char[] { ' ', '\t', '\u3000' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens;
    }

    static bool MatchesAllTokens(string name, string[] tokens)
    {
        if (tokens == null || tokens.Length == 0) return true;
        if (string.IsNullOrEmpty(name)) return false;

        // Convert to lower case once for performance
        var nmLower = name.ToLowerInvariant();

        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (string.IsNullOrEmpty(t)) continue;

            // Use pre-lowercased string for faster comparison
            if (nmLower.IndexOf(t.ToLowerInvariant(), StringComparison.Ordinal) < 0) return false;
        }
        return true;
    }
}
