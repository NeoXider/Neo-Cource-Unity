# AlgoNeoCourse Docs

Entry point for the package documentation inside `Assets/_AlgoNeoCourse/Docs`.

## Read this first

- `CourseMarkdownSpec.md` — current lesson format and course JSON structure
- `Examples/full_lesson.md` — a small end-to-end lesson sample
- `Examples/quiz_single.md` — single choice quiz example
- `Examples/quiz_multiple_truefalse.md` — multiple choice and true/false examples
- `Examples/check_optional_field.md` — mandatory and optional checks with a component value comparison

## What is documented here

- how lesson Markdown is structured
- how `quiz` and `check` blocks work
- how media paths are resolved
- how tables are written
- how the package behaves with local JSON progress

## Important current facts

- courses are selected through `Course1`, `Course2`, or `Custom`
- course data is described in `course1.json`, `course2.json`, or another `courseN.json`
- one lesson is one `.md` file referenced by `file`
- local progress is persisted automatically
- animated GIF is not played; use `.mp4` for animation
- pipe tables (a header row plus a separator row) are rendered as a grid since `1.8.0`
- the package requires Unity `6000.0` or newer since `1.8.0`
- slides are rendered by the embedded Neoxider Markdown Renderer `1.3.0` (`Plugins/markdownrenderer`)
- a `check` block is shown as a task card; with `Validation Settings -> Debug Show Check Source` (off by default, since `1.9.0`) the raw ```check block is shown below the card; Validation Settings toggles apply immediately
- since `1.9.1` the task card remembers each rule's result (✓ / ! / ✕ and the hint) after leaving the slide and after an editor restart; course progress is reset only from `Course Settings` or `Tools -> AlgoNeoCourse -> Settings -> Progress -> Reset Course Progress`
- `optional: true` on any check rule allows a pass with remarks; malformed rules still fail, and saved check results distinguish remarks from a perfect pass
- `field` compares component fields / properties on scene objects or prefab assets using `equals` or collection `count`; `component_exists` also accepts `prefab`
- the progress bar tracks the farthest slide reached, marks the current slide, and provides quiz / practice markers with state tooltips; a click jumps to that slide within the part already reached
