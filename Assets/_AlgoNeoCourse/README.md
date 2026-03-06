# AlgoNeo Course

Version: `1.4.0`

`AlgoNeo Course` is a Unity Editor package for interactive Markdown-based courses directly inside the editor.

## Install via Git URL

Add the package through Unity Package Manager using:

```text
https://github.com/NeoXider/AlgoNeoCource.git?path=Assets/_AlgoNeoCourse
```

Package name:

```text
com.neoxider.algoneocourse
```

## Included

- course window inside `Tools -> AlgoNeoCourse -> Open Course Window`
- built-in Markdown renderer
- quiz system with local JSON progress
- validation checks for training tasks
- editor assemblies via `asmdef`

## Dependency

The package depends on:

```text
com.unity.nuget.newtonsoft-json
```

## Path Compatibility

The package supports both modes:

- embedded in project under `Assets/_AlgoNeoCourse`
- installed from Git under `Packages/com.neoxider.algoneocourse`

Internal read-only resources are resolved from either `Assets` or `Packages`.

User-generated writable data is intentionally stored in `Assets`, for example:

- `Assets/_AlgoNeoCourse/Downloaded`
- `Assets/_AlgoNeoCourse/Progress`
- `Assets/_AlgoNeoCourse/VideoCache`

This keeps local lessons, progress, and cache outside the read-only package folder.

## Main Docs

Repository documentation: see the root `README.md`.
