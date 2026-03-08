# CourseMarkdownSpec

Актуальная спецификация уроков для `AlgoNeoCourse`.

## 1. Структура курса

Каждый курс описывается отдельным JSON-файлом:

- `course1.json`
- `course2.json`
- любой другой `courseN.json` для режима `Custom`

Минимальная структура:

```json
{
  "title": "Название курса",
  "description": "Короткое описание",
  "lessons": [
    { "id": "l2m2y1", "title": "Основы синтаксиса C#", "file": "lessons2/m2/y1.md" }
  ]
}
```

Правила:

- `id` должен быть уникален в рамках курса;
- `file` хранит путь к `.md` файлу;
- путь задаётся относительно источника курса.

## 2. Один урок = один `.md`

Каждый урок — это отдельный Markdown-файл, указанный в `file`.

Пример:

```json
{ "id": "l2m2y3", "title": "Операторы", "file": "lessons2/m2/y3.md" }
```

## 3. Слайды

Слайды разделяются строкой `---`.

```md
# Введение

Короткий контекст.

---

## Теория

Объяснение темы.
```

Правила:

- используйте `#` для титульного слайда;
- используйте `##` для обычных слайдов;
- держите один слайд = одна мысль.

## 4. Медиа

Поддерживаются:

- относительные пути от текущего `.md`;
- `Assets/...`;
- `Packages/...`;
- поиск по имени файла;
- внешние `http://` и `https://` URL.

Примеры:

```md
![](images/example.png)
![](videos/demo.mp4)
![](https://example.com/reference.jpg)
```

GIF:

- как GIF не проигрывается;
- лучше сразу использовать `.mp4`;
- при включённой авто-конвертации GIF может быть преобразован в MP4.

## 5. Квизы

Квизы оформляются через fenced-блок `quiz`.

```quiz
id: l2m2y3-q1
kind: single
text: Какой оператор выполняет проверку условия?
answers:
  - text: if
    correct: true
  - text: for
  - text: while
```

Поддерживаемые типы:

- `single`
- `multiple`
- `truefalse`

Правила:

- у каждого квиза должен быть уникальный `id`;
- один вопрос лучше размещать на одном слайде;
- пояснение к ответу лучше выносить на следующий слайд.

Прогресс квизов сохраняется локально автоматически.

Настройки квизов:

```text
Tools -> AlgoNeoCourse -> Settings -> Open Quiz Settings
```

Ключевые параметры:

- `maxAttemptsPerQuestion`
- `randomizeAnswersOnCourseOpen`
- `guardSlideNavigation`
- `stateJsonFolder`

## 6. Автопроверки

Автопроверки оформляются через fenced-блок `check`.

````md
```check
rules:
  - object_exists: "Player"
  - component_exists: { object: "Player", type: "Rigidbody" }
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
```
````

Базовые правила:

- `object_exists`
- `component_exists`
- `filename`
- `contains`

Для отладки:

```text
Project Settings -> AlgoNeoCourse -> Validation -> Debug Render Check Blocks
```

## 7. Unity-ссылки

Поддерживаются ссылки вида:

```md
[Открыть префаб](unity://open?path=Assets/Prefabs/Player.prefab)
[Проверить](unity://check?type=component-present&target=Player&component=Rigidbody)
```

## 8. Окно курса

Окно открывается через:

```text
Tools -> AlgoNeoCourse -> Open Course Window
```

Горячие клавиши:

- `Left` / `Right`
- `R`
- `O`

Переход вперёд может блокироваться незавершённым квизом.

## 9. Front matter

В начале урока можно использовать front matter:

```yaml
---
id: l2m2y3
module: 2
lesson: 3
title: Операторы и приоритет
tags: [beginner, csharp]
est_time_min: 15
---
```

Это необязательный блок, но он помогает держать единый формат.

## 10. Минимальный шаблон урока

````md
# Название урока

Короткое вступление.

---

## Теория

Объяснение и пример.

---

## Вопрос

```quiz
id: lesson-q1
kind: single
text: Вопрос по теме?
answers:
  - text: Верный ответ
    correct: true
  - text: Неверный ответ
```

---

## Пояснение

Почему ответ именно такой.

---

## Практика

```check
rules:
  - filename: "Example.cs"
  - contains: "Debug.Log"
```

---

## Итоги

- Что изучили
- Что закрепили
````

## 11. Ограничения

- не рассчитывайте на сложный HTML;
- не используйте неуникальные `id`;
- не полагайтесь на GIF как на воспроизводимое видео;
- лучше не делать гигантские стены текста.

## 12. Чек-лист перед публикацией

- запись в `course1.json`, `course2.json` или `courseN.json` добавлена;
- путь `file` существует;
- слайды разделены `---`;
- у квизов уникальные `id`;
- медиа доступны;
- урок корректно открывается в `AlgoNeoCourse`.
