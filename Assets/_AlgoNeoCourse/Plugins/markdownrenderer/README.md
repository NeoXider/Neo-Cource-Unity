<p align="center">
  <img src="Docs~/Images/readme/01_hero.svg" alt="Neoxider Markdown Renderer: Markdown text becomes a live UI Toolkit VisualElement tree with a heading, a list with inline code and a syntax-highlighted Python block" width="100%">
</p>

# <img src="Docs~/Images/icon.svg" alt="Markdown Renderer icon" width="40" height="40" align="absmiddle"> Neoxider Markdown Renderer

**Markdig-based Markdown renderer for Unity UI Toolkit.** Turns Markdown text into a live `VisualElement`
tree — no HTML or WebView — so it can sit in any runtime UI (chat bubbles, quiz text, docs panels) or power
Editor tools.

[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black)](https://unity.com/releases/editor)
[![License](https://img.shields.io/badge/license-MIT%20%28Neoxider%20changes%29%20%2B%20Unity%20Companion-blue)](#license)
[![Version](https://img.shields.io/badge/version-1.3.0-5ee05a)](CHANGELOG.md)

<sub>
<a href="#why">Why</a>&nbsp;&nbsp;•&nbsp;
<a href="#what-is-inside">What is inside</a>&nbsp;&nbsp;•&nbsp;
<a href="#install">Install</a>&nbsp;&nbsp;•&nbsp;
<a href="#quick-start">Quick start</a>&nbsp;&nbsp;•&nbsp;
<a href="#tables">Tables</a>&nbsp;&nbsp;•&nbsp;
<a href="#syntax-highlighting">Highlighting</a>&nbsp;&nbsp;•&nbsp;
<a href="#styling">Styling</a>&nbsp;&nbsp;•&nbsp;
<a href="#editor-tooling">Editor</a>&nbsp;&nbsp;•&nbsp;
<a href="#limits">Limits</a>&nbsp;&nbsp;•&nbsp;
<a href="#license">License</a>
</sub>

---

## Why

Chat replies, lesson text and in-project docs are usually written in Markdown, but UI Toolkit has no
built-in Markdown element. This package parses Markdown with Markdig and builds native UI Toolkit
elements from it, so the text lives inside your own panel, takes your USS, reports link clicks to your
code, and colours code blocks from a JSON palette instead of hard-coded colours.

---

## What is inside

- **`UIMarkdownRenderer`** — headings, bold/italic, inline code, blockquotes, nested lists, links, tables,
  thematic breaks and fenced code blocks with syntax highlighting (Python, C#).
- **`MarkdownDisplay`** (`[UxmlElement]`) — ready-to-use runtime control: drop it into UXML or create it in
  code, set `Markdown` / `SourceAsset`. Its stylesheet is a serialized reference, so it renders the same in
  the Editor, Play Mode and builds (WebGL, standalone).
- **`VideoPlayerElement`** (`[UxmlElement]`) — inline video embeds.
- **`YamlFrontMatterHandler`** — optional YAML front matter ahead of the Markdown body.
- **Syntax highlighting** — `PythonSyntaxHighlighter`, `CSharpSyntaxHighlighter`; colours come from JSON
  palettes, not from code.
- **Editor tooling** — rendered inspector for `.md` assets, Markdown Doc Viewer, Markdown Preview, media
  pop-out window.
- **Default styles** `Styles/MarkdownRenderer.uss` and `Styles/Quiz.uss` — optional.
- **`Examples/*.md`** — sample documents.

---

## Install

Unity **6000.0** or newer. Package Manager → *Add package from git URL*:

```text
https://github.com/NeoXider/NeoxiderMarkdownRenderer.git#v1.3.0
```

or in `Packages/manifest.json`:

```json
"com.neoxider.markdownrenderer": "https://github.com/NeoXider/NeoxiderMarkdownRenderer.git#v1.3.0"
```

`package.json` declares no dependencies: the Markdig parser is vendored under `Runtime/Markdig`.

---

## Quick start

The namespace and the renderer class share the name `UIMarkdownRenderer`, so refer to the class through an
alias (or `global::UIMarkdownRenderer.UIMarkdownRenderer`):

```csharp
using UIMarkdownRenderer;
using UnityEngine;
using MarkdownRenderer = UIMarkdownRenderer.UIMarkdownRenderer;

var renderer = new MarkdownRenderer((link, r) => Application.OpenURL(link));
renderer.SetMarkdown("# Hello\n\nSome **markdown**.");
myVisualElement.Add(renderer.RootElement);
```

Or the ready-made control:

```csharp
var display = new MarkdownDisplay { DefaultStyleSheet = myStylesheet };
display.LinkClicked += link => Application.OpenURL(link);
display.SetMarkdown("# Hello\n\nSome **markdown**.");
myVisualElement.Add(display);
```

In UXML, `MarkdownDisplay` exposes the attributes `markdown`, `source-asset`, `default-stylesheet` and
`include-default-stylesheet`.

Per-instance options on the renderer: `InlineCodeColor`, `InlineCodeInheritsTextColor`,
`LinkInheritsTextColor`.

Relative links and media (`./img/pic.png`, `../clip.mp4`) are resolved against the folder of the current
file. `OpenFile(path)` sets that folder and renders the file; `SetFileContext(path)` sets the same folder
without rendering anything, for when you feed the text yourself through `SetMarkdown`:

```csharp
renderer.SetFileContext("Assets/Docs/lesson.md");
renderer.SetMarkdown(slideText); // ./img/pic.png -> Assets/Docs/img/pic.png
```

---

## Tables

Pipe tables are rendered as a real grid:

```markdown
| Item   | Status | Price |
| :----- | :----: | ----: |
| Sword  | ready  |   120 |
| Shield | draft  |    85 |
```

- A table needs a header row and a separator row (`| --- | --- |`), and a blank line before it. Text that
  merely contains `|` without a separator row stays a paragraph.
- The separator row sets the column alignment: `:---` left, `:---:` center, `---:` right.
- Cells go through the normal pipeline, so bold/italic, inline code, links and images work inside them. Write
  a literal pipe as `\|`.
- Columns are sized without measuring text: every cell of a column gets the same weight (length of the
  column's longest content plus a small allowance for cell padding, clamped to 7…30). The weight is the
  cell's `flex-grow`, and its `flex-basis` is the weight × 8px with the same `flex-shrink`, so growing and
  shrinking are both proportional to the weight. The table takes the width of its parent, columns line up
  across rows, and long text wraps inside its column instead of widening the table. In a parent that
  shrink-wraps its content (a chat bubble) the table gets that estimated width instead of collapsing.
- A row with fewer cells than the header is padded with empty cells.
- Inside a list item a table falls back to text rows (`cell | cell`), because a list is rendered as a single
  label.

Element tree and USS classes, for theming:

| Class | Element |
|---|---|
| `md-table` | The table (also has `block`, plus any classes from `{.my-class}` attributes) |
| `md-table-row` | Every row |
| `md-table-header` | Added to the header row |
| `md-table-row--alt` | Added to every second body row (striping) |
| `md-table-cell` | Every cell; its content is a regular `.block` with a `.paragraph` label |
| `md-table-cell--left`, `--center`, `--right` | Added to cells of a column with an explicit alignment |

The renderer sets only the layout inline (`flex-direction: row` on rows; `flex-grow`, `flex-shrink`,
`flex-basis`, `min-width: 0` on cells; `white-space: normal` and the alignment on cell labels), so tables
lay out correctly under any theme. Borders, the header background, striping and padding come from
`Styles/MarkdownRenderer.uss` and can be overridden. Sample: `Examples/04_Tables.md`.

---

## Syntax highlighting

- Fenced blocks tagged `python` / `py` go through `PythonSyntaxHighlighter`, `csharp` / `cs` through
  `CSharpSyntaxHighlighter`.
- Colours come from JSON palettes: `SyntaxPalette.Load(name)` reads `Resources/SyntaxHighlighting/<name>.json`.
  The package ships `python.json`, `python-light.json` and `csharp.json`; a key missing from a palette
  falls back to the VS Code Dark+ colour.
- A highlighter takes its palette in the constructor (`new PythonSyntaxHighlighter(palette)`), and
  `CodeBlockRenderer.ReloadPalette(language, palette)` swaps the palette used by code blocks at runtime,
  for example in a live style preview.

---

## Styling

- `Styles/MarkdownRenderer.uss` and `Styles/Quiz.uss` are defaults, not requirements: render with
  `includeDefaultStylesheet: false` and bring your own theme.
- A stylesheet passed to a renderer (or assigned to `MarkdownDisplay.DefaultStyleSheet`) applies to that
  instance only. An instance without one uses the package stylesheet in the Editor; in a build, where the
  package asset cannot be looked up, it falls back to the stylesheet most recently passed to any renderer.
  In a build, either always assign one explicitly or turn the default off and layer your own theme.
- `InlineCodeInheritsTextColor` and `LinkInheritsTextColor` drop the `<color>` tag, so inline code and links
  take the text colour from your USS (code stays bold, links stay underlined).

---

## Editor tooling

- Rendered inspector for `.md` assets.
- `Window → Markdown Doc Viewer` — anchors, `Assets/…` / `Packages/…` links, `search:` / `package:`
  lookups, `cmd:` commands.
- `Window → Neoxider → Markdown Preview` — raw text next to the live render.
- Media pop-out window: `MediaPopoutWindow.Popout(element)` detaches a media element into its own window,
  `MediaPopoutWindow.EnsureClosed()` closes any open pop-out (call it when your page or slide changes).

---

## Assemblies

| Assembly | Contents |
|---|---|
| `Rtl.MarkdownRenderer.Runtime` | Renderer, `MarkdownDisplay`, highlighters, video element |
| `Rtl.MarkdownRenderer.Editor` | Editor tooling (Editor only) |
| `Markdig` | Vendored Markdown parser |
| `Rtl.MarkdownRenderer.Tests.EditMode` | EditMode tests |

---

## Limits

- **Two highlighted languages.** Only Python and C# fences are coloured; `ReloadPalette` swaps palettes for
  those two, it does not register new languages.
- **No HTML.** Markdown becomes UI Toolkit elements; there is no HTML or WebView rendering path.
- **Tables are pipe tables.** No grid tables, no cell spans, no multi-line cells; a table always takes the
  full width of its parent.
- **Lists are a single label.** Blocks inside a list item (code blocks, tables) are written into the list
  text rather than rendered as their own elements.

---

## Changelog

Release notes: [CHANGELOG.md](CHANGELOG.md) (the latest entries are in Russian).

---

## License

This package is a derivative work, and the licenses apply to different parts of it:

- **Modifications and additions by Neoxider** — [MIT](LICENSE.md), © 2026 Neoxider. The MIT terms cover
  the modifications and additions only; they do not relicense the original work.
- **Original work** — [Markdown UIElement](https://github.com/UnityGuillaume/MarkdownRenderer) by Guillaume
  Saby, © 2023 Unity Technologies. The original parts and the parts derived from them remain under the
  [Unity Companion License](https://unity3d.com/legal/licenses/unity_companion_license) for
  Unity-dependent projects.
- **Markdig** (vendored in `Runtime/Markdig`) — BSD-2-Clause, © Alexandre Mutel; see
  [Third Party Notices.md](Third%20Party%20Notices.md).

Full text: [LICENSE.md](LICENSE.md).

**Author:** [Neoxider](https://github.com/NeoXider) · **Issues:**
[GitHub Issues](https://github.com/NeoXider/NeoxiderMarkdownRenderer/issues)
