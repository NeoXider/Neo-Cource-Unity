# Neoxider Markdown Renderer Changelog

## 1.3.0 — 2026-11-11

- Таблицы Markdown (pipe tables). В конвейер Markdig добавлено `UsePipeTables()` (порядок расширений:
  `UsePipeTables().UseYamlFrontMatter().UseGenericAttributes()` — атрибуты по рекомендации Markdig последние),
  новый `TableRenderer` строит дерево `.md-table` → `.md-table-row` (шапка — `.md-table-header`, каждая вторая
  строка тела — `.md-table-row--alt`) → `.md-table-cell` (`--left` / `--center` / `--right` по строке-разделителю
  `:---`, `:---:`, `---:`; выравнивание ставится метками ячейки через `-unity-text-align`). Содержимое ячейки
  идёт через обычный конвейер: выделение, встроенный код, ссылки и картинки работают как в абзаце.
  Ширина колонок не измеряется по тексту: у всех ячеек колонки один вес (длина самого длинного содержимого
  плюс запас на отступы, в пределах 7…30) — он идёт в `flex-grow`, а `flex-basis` равен весу × 8px при общем
  `flex-shrink`, поэтому колонки совпадают между строками, таблица не шире родителя, а длинный текст
  переносится. В родителе, который обжимает содержимое, таблица получает эту оценку ширины и не
  схлопывается (с `flex-basis: 0` схлопывалась до отступов ячеек). Короткая строка дополняется пустыми
  ячейками. Таблица внутри списка выводится читаемыми строками «ячейка | ячейка» (список — одна метка).
  Оформление — в `Styles/MarkdownRenderer.uss`: рамки, фон и жирный текст шапки, полосатые строки, отступы.
- Абзац с символами `|` без строки-разделителя остаётся абзацем. Для этого добавлен `DelimiterInlineRenderer`:
  с включёнными таблицами Markdig оставляет такие `|` узлами-разделителями, и без него они пропали бы из текста.
- Нумерованный список начинается с числа автора (`ListBlock.OrderedStart`): `3.` рисуется как 3, а список,
  продолженный после абзаца или блока кода, не сбрасывается на 1.
- Блок кода внутри пункта списка больше не пропадает: его строки пишутся в метку списка (с подсветкой
  известного языка, иначе цветом встроенного кода).
- `UIMarkdownRenderer.SetFileContext(string filePath)` — выставляет папку файла для относительных ссылок и
  медиа так же, как `OpenFile`, но ничего не рендерит. `OpenFile` с пустым путём больше не падает, а
  показывает тот же fallback, что и для отсутствующего файла; путь без папки (`"lesson.md"`) тоже принят.
- `ResolveLink`: `./` и `../` резолвятся через `Path.GetFullPath(Path.Combine(папка файла, ссылка))` —
  путь нормализован, недопустимые символы не роняют рендер. Без контекста файла путь считается от текущей
  папки процесса (раньше ссылка возвращалась как есть).
- Явно переданные в конструктор `defaultStylesheet`, `videoPlayerStyleSheet` и `videoPlayerElementPrefab`
  действуют на свой экземпляр и больше не становятся значением по умолчанию для всех рендереров процесса.
  Без аргумента в редакторе берётся ассет пакета; в сборке, как и раньше, — последний явно переданный.
- `MediaPopoutWindow` стал `public`. `MediaPopoutWindow.EnsureClosed()` закрывает существующие окна через
  `Resources.FindObjectsOfTypeAll` и больше не создаёт и не фокусирует окно (`GetWindow`). `Popout`
  игнорирует элемент без родителя, `OnDestroy` не бросает исключение, обработчик клавиш не множится при
  повторном фокусе, убран отладочный `Debug.Log` таблиц стилей.
- Картинки и видео: расширение видео сравнивается без учёта регистра (`.MP4`), создание
  `UnityWebRequest` для картинки обёрнуто в `try/catch`, картинка без подписи (`![](url)`) не роняет рендер.
- YAML front matter: разбор по первому двоеточию и `try/catch` — строка без `:` больше не роняет документ,
  путь с двоеточием не обрезается. `AddCustomUSS` не падает на относительном пути без контекста файла.
- Редактор: `.md` распознаётся без учёта регистра (`MarkdownViewer`, инспектор `.md`, ссылки на `.md` /
  `.txt`); инспектор не падает, если файл не удалось открыть. `SendCommand` без обработчиков не бросает.
- USS без предупреждений импортёра. `Styles/MarkdownRenderer.uss`: убран недопустимый `scale: 0.99%` у
  `.linkHovered`, добавлены единицы (`border-left-width`, `.check-result-label`), размер заголовков задан в
  px (22 / 18 / 15 вместо 200% / 150% / 120% — проценты импортёр отбрасывал, и заголовки оставались без
  размера). `Styles/Quiz.uss`: удалены неподдерживаемые `gap`, `box-shadow`, `border-style` (Unity их
  игнорировал, вид не меняется).
- Примеры: `Examples/04_Tables.md`; в `03_Media_And_EdgeCases.md` таблица больше не описана как «текст»;
  в образец окна Markdown Preview добавлена таблица.
- Тесты EditMode: таблицы (дерево, веса колонок, выравнивание, код и ссылка в ячейке, короткие строки, текст с
  `|` без разделителя, таблица в списке, картинка в ячейке) и раскладка таблицы в панели UI Toolkit без окна
  редактора (колонки совпадают между строками, таблица не шире родителя, текст переносится); исправления
  этого выпуска.
- Видеоплеер: цвета в `VideoPlayerElement.uss` заданы через переменные `--rs-code-*` с запасными значениями, поэтому элементы управления видны и в проектах, где эти переменные не объявлены.

## 1.2.1 — 2026-10-28

- Корень встроенной копии пакета — `Path.GetDirectoryName` на три уровня вверх от скрипта, без обрезки строки.

## 1.2.0 — 2026-10-28

- Отдельный публичный репозиторий https://github.com/NeoXider/NeoxiderMarkdownRenderer. Лицензия: изменения и
  дополнения Neoxider — MIT; основа — Markdown UIElement (© 2023 Unity Technologies, Unity Companion License);
  Markdig — BSD-2-Clause (`LICENSE.md`, `Third Party Notices.md`).
- Пути редакторных ассетов (`Styles/MarkdownRenderer.uss`, шаблон и стиль `VideoPlayerElement`) — от корня
  пакета (`PackageInfo`, для встроенной копии — по скрипту), а не `Assets/ThirdParty/...`: пакет работает и
  установленным из git в `Packages`.
- Удалён неиспользуемый `Editor/AlgoNeoPackageAssetLocator.cs` (остаток исходного ассета).

- `UIMarkdownRenderer.InlineCodeInheritsTextColor` и `LinkInheritsTextColor`: встроенный код и ссылка не пишут
  тег `<color>` и берут цвет текста от USS (код остаётся жирным, ссылка подчёркнутой). Нужны поверхностям,
  где весь текст одного цвета (плашки вопроса и ответа комнат). Поведение по умолчанию не изменено.

## 1.1.1

- Мигрирован `VideoPlayerElement` на UI Toolkit UXML API Unity 6.6: удалена legacy-фабрика
  `UxmlFactory<VideoPlayerElement>`, сам элемент помечен `[UxmlElement]` и объявлен `partial`.
  Публичных UXML-атрибутов у элемента не было, поэтому контракт UXML не расширялся. Полный аудит
  Runtime, Editor, Examples, Tests и `.uxml` не нашёл других `UxmlFactory`, `UxmlTraits`,
  `Uxml*AttributeDescription`, `IUxmlAttributes` или legacy `Init(...)`.
- Имя тега в `Runtime/VideoElement/VideoPlayerElement.uxml` (`<VideoPlayerElement>`, глобальный
  namespace) оставлено прежним — `[UxmlElement]` регистрирует элемент под тем же полным именем, что
  и снятая фабрика, поэтому `UIMarkdownRenderer.AddVideoPlayer` продолжает находить его через
  `Q<VideoPlayerElement>()`. Внимание: пока сборка проекта была сломана, Unity импортировала этот
  `.uxml` без доступного типа и закешировала элемент как заглушку-`Label` («Element
  'VideoPlayerElement' is missing a UxmlElementAttribute»). После того как проект соберётся целиком и
  пройдёт domain reload, `.uxml` нужно переимпортировать (Reimport), иначе видеоплеер в Markdown
  соберётся без панели управления.
- Минимальная версия Unity в `package.json` осознанно оставлена на `6000.0`: `[UxmlElement]` доступен
  начиная с Unity 6000.0, в 6.6 сломался ровно старый путь, а не новый. Мигрированный элемент собирается
  на всём диапазоне 6000.0+, поэтому сужать поддержку незачем.

## 1.1.0

- Verified the package compiles clean (zero console errors/warnings) on the project's current Unity
  `6000.5.0f1`. No source changes were needed: the `EntityId`-based double-click asset lookup in
  `MarkdownViewer` already branches on `UNITY_6000_3_OR_NEWER`, and the `UNITY_2022_x` reflection
  fallbacks in `UIMarkdownRenderer` are inert on this project (both take the modern branch). Documented
  these version guards, and flagged `Editor/AlgoNeoPackageAssetLocator.cs` as unreferenced vendor
  leftover (wrong package name/paths for this layout), in the README.
- Added `MarkdownDisplay`, a runtime `[UxmlElement]` control (`Runtime/MarkdownRendering/MarkdownDisplay.cs`)
  that wraps `UIMarkdownRenderer` behind `Markdown`/`SourceAsset`/`DefaultStyleSheet`/
  `IncludeDefaultStylesheet` UXML attributes and a `LinkClicked` event, reusing a single renderer
  instance across re-renders. Unlike the Editor tools, its stylesheet is a serialized reference rather
  than an `AssetDatabase` path, so it works identically in builds (WebGL/standalone/etc), not just the
  Editor. Verified live: renders, re-renders on a second `SetMarkdown` call without rebuilding the
  renderer, and reports link clicks — zero console errors/warnings.

## 1.0.0

- Formalized as a standalone Neoxider developer package: added `package.json`
  (`com.neoxider.markdownrenderer`), `README.md` and this changelog, matching the layout already
  used by the sibling `Neoxider LevelGeneration`/`CoreAiPro` packages under
  `Assets/ThirdParty/Neoxider`.
- Added `MarkdownPreviewWindow` (`Window/Neoxider/Markdown Preview`): a split-pane editor tool (raw
  Markdown on the left, live render on the right, with a size slider) for iterating on the renderer
  itself using only the package's own default stylesheet — no host project theme dependency. This is
  the package-owned counterpart to project-specific preview tools that layer their own theme on top
  of `UIMarkdownRenderer` (e.g. RedoSchool's `ChatUI/Editor/MarkdownStylePreviewWindow`, which renders
  through the same pipeline but with the host project's chat-bubble theme instead of the default
  stylesheet).
- No behavioral changes to the renderer, object renderers, syntax highlighters, or existing
  `MarkdownCustomEditor`/`MarkdownViewer`/`MediaPopoutWindow` tools — this release only adds the
  package metadata and the new preview window on top of the existing, already-shipping code.
