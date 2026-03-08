# AlgoNeo Course

Version: `1.4.1`

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
- built-in Markdown renderer
- quiz system with local JSON persistence
- validation checks for practical tasks
- editor assemblies via `asmdef`
- GIF -> MP4 support

## Dependency

The only external dependency is:

```text
com.unity.nuget.newtonsoft-json
```

## Course workflow

Use `Tools -> AlgoNeoCourse -> Settings -> Open Course Settings` to:

- set `repositoryBaseUrl`
- choose `Course1`, `Course2`, or `Custom`
- load lesson metadata
- download local lesson files
- configure GIF conversion and download paths

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
