# AlgoNeo Course

Version: `1.8.0`

Requires Unity `6000.0` or newer.

`AlgoNeo Course` is a Unity Editor package for interactive Markdown-based learning directly inside the editor.

## Install via Git URL

Add the package through Unity Package Manager:

```text
https://github.com/NeoXider/Neo-Cource-Unity.git?path=Assets/_AlgoNeoCourse
```

Package name:

```text
com.neoxider.algoneocourse
```

## What the package includes

- course window in `Tools -> AlgoNeoCourse -> Open Course Window`
- embedded Markdown renderer (Neoxider Markdown Renderer `1.3.0`): headings, lists, quotes, links,
  pipe tables, fenced code with C# / Python syntax highlighting
- quiz system with local JSON persistence
- validation checks for practical tasks
- editor assemblies via `asmdef`
- images and `.mp4` video in slides

## Dependency

The only external dependency is:

```text
com.unity.nuget.newtonsoft-json
```

The package works from a single `.unitypackage`: no git and no internet access are needed to install it.

## Embedded Markdown renderer

`Plugins/markdownrenderer` is an unmodified copy of
[Neoxider Markdown Renderer](https://github.com/NeoXider/NeoxiderMarkdownRenderer) `v1.3.0`
(`com.neoxider.markdownrenderer`): its `Runtime`, `Editor` and `Styles` folders plus `package.json`,
`README.md`, `CHANGELOG.md`, `LICENSE.md` and `Third Party Notices.md`, with the upstream `.meta` files.
The upstream `Tests`, `Examples` and `Docs~` folders are not included.

- To update the renderer, replace the contents of that folder with the same set of files from a newer
  upstream tag. Nothing specific to AlgoNeo Course lives inside it.
- Everything course-specific is on the plugin side: the window theme `Editor/Styles/AlgoNeoCourseWindow.uss`
  (slide colours, tables), the quiz / check styles `Editor/Styles/AlgoNeoQuiz.uss`, the orange bold text and
  the inline code colour (`CourseWindow.Content.cs`).
- Assemblies: `Markdig` and `Rtl.MarkdownRenderer.Runtime` (all platforms), `Rtl.MarkdownRenderer.Editor`
  (Editor only). `AlgoNeoCourse.Editor` references all three.
- `Tools -> AlgoNeoCourse -> Settings -> Dependencies -> Validate Embedded Markdown` checks that the renderer
  folder and the plugin styles are in place.

## Course workflow

Use `Tools -> AlgoNeoCourse -> Settings -> Open Course Settings` to:

- set `repositoryBaseUrl`
- choose `Course1`, `Course2`, or `Custom`
- load lesson metadata
- download local lesson files
- pick lessons by module and download one module or everything

Then open:

```text
Tools -> AlgoNeoCourse -> Open Course Window
```

## Path compatibility

The package supports both modes:

- embedded in project under `Assets/_AlgoNeoCourse`
- installed from Git under `Packages/com.neoxider.algoneocourse`

Internal read-only resources are resolved from either `Assets` or `Packages`.

Writable user data is intentionally stored in `Assets`, for example:

- `Assets/_AlgoNeoCourse/Downloaded`
- `Assets/_AlgoNeoCourse/Progress`
- `Assets/_AlgoNeoCourse/VideoCache`

This keeps local lessons, progress, and cache outside the read-only package folder.

## Docs

- root repo docs: `README.md`
- package docs index: `Assets/_AlgoNeoCourse/Docs/README.md`
- lesson format spec: `Assets/_AlgoNeoCourse/Docs/CourseMarkdownSpec.md`
- examples: `Assets/_AlgoNeoCourse/Docs/Examples`
