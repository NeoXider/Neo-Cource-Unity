using System;
using UnityEngine;

namespace UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting
{
    /// <summary>
    /// JSON shape of a per-language palette (one hex string per token kind). Field names == JSON keys.
    /// </summary>
    [Serializable]
    public sealed class SyntaxPaletteData
    {
        public string comment;
        public string str;
        public string number;
        public string keyword;
        public string function;
        public string type;
        public string variable;
        public string bracket;
    }

    // TODO(syntax-palette-so): add a ScriptableObject wrapper around these per-language JSONs. The SO holds
    // Color/hex fields editable with the native Inspector color picker, and on save WRITES the matching
    // <lang>.json (Resources/SyntaxHighlighting/). Goal: tweak colors per language conveniently from the
    // Inspector; the JSON stays the source of truth the runtime reads.

    /// <summary>
    /// A language's token colors. EACH language loads its OWN JSON
    /// (<c>Resources/SyntaxHighlighting/&lt;language&gt;.json</c>, e.g. python.json / csharp.json) so colors
    /// can differ per language and be retuned without recompiling. Missing file or empty fields fall back
    /// to the <see cref="VsCodePalette"/> defaults, so rendering never breaks. WebGL-safe (Resources are
    /// bundled into the build).
    /// </summary>
    public sealed class SyntaxPalette
    {
        public string Comment = VsCodePalette.Comment;
        public string String = VsCodePalette.String;
        public string Number = VsCodePalette.Number;
        public string Keyword = VsCodePalette.Keyword;
        public string Function = VsCodePalette.Function;
        public string Type = VsCodePalette.Type;

        public string Variable = VsCodePalette.Variable;

        // У старых палитр без bracket скобки наследуют цвет ключевых слов.
        public string Bracket = VsCodePalette.Keyword;

        public static SyntaxPalette Load(string language)
        {
            SyntaxPalette palette = new();
            if (string.IsNullOrWhiteSpace(language))
            {
                return palette;
            }

            TextAsset asset = Resources.Load<TextAsset>($"SyntaxHighlighting/{language}");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return palette;
            }

            try
            {
                SyntaxPaletteData data = JsonUtility.FromJson<SyntaxPaletteData>(asset.text);
                if (data == null)
                {
                    return palette;
                }

                Set(ref palette.Comment, data.comment);
                Set(ref palette.String, data.str);
                Set(ref palette.Number, data.number);
                Set(ref palette.Keyword, data.keyword);
                Set(ref palette.Function, data.function);
                Set(ref palette.Type, data.type);
                Set(ref palette.Variable, data.variable);
                palette.Bracket = palette.Keyword;
                Set(ref palette.Bracket, data.bracket);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SyntaxPalette] Failed to parse '{language}' palette JSON, keeping defaults: {ex.Message}");
            }

            return palette;
        }

        private static void Set(ref string target, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target = value;
            }
        }
    }
}
