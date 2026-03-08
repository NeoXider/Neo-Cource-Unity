# LessonRule

Краткая памятка по актуальной структуре курсов для `AlgoNeoCourse`.

## Что используется сейчас

- курсы описываются отдельными JSON-файлами;
- обычно это `course1.json`, `course2.json` или любой `courseN.json`;
- один урок — это один `.md` файл, указанный в поле `file`;
- уроки могут храниться по структуре `lessons1/`, `lessons2/` и далее;
- прогресс курса и квизов сохраняется локально автоматически.

## Минимальный пример файла курса

```json
{
  "title": "Unity Basics",
  "description": "Базовый курс по Unity",
  "lessons": [
    { "id": "l2m2y1", "title": "Введение", "file": "lessons2/m2/y1.md" },
    { "id": "l2m2y2", "title": "Rigidbody", "file": "lessons2/m2/y2.md" }
  ]
}
```

## Где смотреть полную документацию

- `README.md`
- `Assets/_AlgoNeoCourse/Docs/README.md`
- `Assets/_AlgoNeoCourse/Docs/CourseMarkdownSpec.md`
