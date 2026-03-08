# AlgoNeoCourse Docs

Entry point for the package documentation inside `Assets/_AlgoNeoCourse/Docs`.

## Read this first

- `CourseMarkdownSpec.md` — current lesson format and course JSON structure
- `Examples/slide_navigation.md` — slide navigation examples
- `Examples/full_lesson.md` — a small end-to-end lesson sample
- `Examples/quiz_single.md` — single choice quiz example
- `Examples/quiz_multiple_truefalse.md` — multiple choice and true/false examples

## What is documented here

- how lesson Markdown is structured
- how `quiz` and `check` blocks work
- how media paths are resolved
- how slide navigation links work
- how the package behaves with local JSON progress and GIF conversion

## Important current facts

- courses are selected through `Course1`, `Course2`, or `Custom`
- course data is described in `course1.json`, `course2.json`, or another `courseN.json`
- one lesson is one `.md` file referenced by `file`
- local progress is persisted automatically
- GIF is better replaced with `.mp4`, but GIF -> MP4 conversion is supported
