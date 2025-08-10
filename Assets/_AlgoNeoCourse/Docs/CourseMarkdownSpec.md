# Спецификация Markdown для интерактивного курса в Unity Editor

## Общее
- Каждый урок описывается одним `.md`‑файлом, перечисленным в `course.json`.
- Разделяйте урок на слайды отдельно стоящей строкой `---`.
- Рендер выполняется через UI Toolkit (MarkdownRenderer). Поддерживаются заголовки, списки, цитаты, таблицы, код‑блоки, изображения и видео.
- Для интерактива используйте ссылки со схемой `unity://...`.

## Формат course.json
- Поля:
  - `title`: заголовок курса
  - `description`: описание (необязательно)
  - `lessons`: массив объектов `{ id, title, file }`
    - `id`: уникальный идентификатор урока
    - `title`: отображаемое имя урока
    - `file`: относительный путь к `.md` от корня репозитория (напр. `lessons/m1y1.md`)

Пример:
```json
{
  "title": "Пример",
  "description": "Тестовый курс",
  "lessons": [
    { "id": "lesson01", "title": "Базовая сцена", "file": "lessons/m1y1.md" },
    { "id": "lesson02", "title": "Скрипты в Unity", "file": "lessons/m1y2.md" }
  ]
}
```

## Структура `.md` урока
- Слайды разделяются одиночной строкой:
```
---
```
- Пример разбиения и навигации:
```md
# Слайд 1 — Введение
Добро пожаловать!

---

## Слайд 2 — Теория
Картинка:
![](images/intro.png)

Видео:
![](https://www.example.com/intro.mp4)

[◀](unity://slide?dir=prev) [▶](unity://slide?dir=next)
```

## Действия (`unity://`)
- Навигация по слайдам:
  - `unity://slide?dir=next` — следующий слайд
  - `unity://slide?dir=prev` — предыдущий слайд
- Проверка задания через реестр проверок:
  - `unity://check?type=component-present&target=<ObjectName>&component=<ComponentType>`
    - пример: `unity://check?type=component-present&target=Player&component=Rigidbody`
- Открытие ассета/показ файла в проводнике:
  - `unity://open?path=Assets/Prefabs/Player.prefab`

## Медиа
- Изображения: `![](путь-или-url)`
- Видео: `![](путь-или-url.mp4|.mov|.webm|...)` — определяется по расширению, создаётся видео‑плеер.

Разрешение путей к медиа:
- Абсолютные URL: поддерживаются `http://` и `https://` (например, `![](https://example.com/pic.png)`).
- Относительные пути: считаются относительно текущего `.md`‑файла урока (например, `![](./images/pic.png)` или `![](images/pic.png)`).
- Голое имя файла без путей: выполняется поиск ассета по имени в проекте (например, `![](logo.png)` найдёт `Assets/**/logo.png`).
- Абсолютные проектные пути `Assets/...` и `Packages/...` также поддерживаются.

Видео‑форматы:
- Определяются по расширению ссылки/пути. Поддерживаются, в частности: `.mp4`, `.mov`, `.webm`, `.avi`, `.wmv`, `.mpeg`, `.mpg`, `.m4v`, `.ogv`, `.asf`, `.dv`, `.vp8`.

Примечания:
- GIF‑файлы загружаются как статические текстуры (анимация GIF в UI Toolkit не воспроизводится из коробки).
- Внешние ссылки могут быть недоступны (404) — в таком случае отобразится предупреждение в консоли, урок продолжит работу.

Примеры:
```md
# Изображения

Голое имя (поиск по проекту):
![](sample.jpg)

Относительный путь от файла урока:
![](images/sample.jpg)

Внешняя ссылка:
![](https://upload.wikimedia.org/wikipedia/commons/thumb/a/a3/June_odd-eyed-cat.jpg/320px-June_odd-eyed-cat.jpg)

# Видео

Внешний mp4:
![](https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4)
```

## Debug‑блоки проверок (опционально)
- Включается в Project Settings: `AlgoNeoCourse → Validation → Debug Render Check Blocks`.
- Формат fenced‑блока:
```
```check
# Пример YAML‑подобных правил
type: scene
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
```
```
- Под блоком автоматически появляется кнопка “▶ Проверить” и текстовый результат.
- Поддерживаемые правила в debug‑режиме:
  - `object_exists: "<ObjectName>"`
  - `component_exists: { object: "<ObjectName>", type: "<ComponentType>" }`

## Рекомендации
- Храните уроки в `lessons/`, медиа — в `lessons/images/`.
- Один слайд — одна мысль/шаг.
- На длинных слайдах добавляйте навигацию `[◀] ... [▶]`.
- Для стабильных проверок используйте `unity://check?type=...`; fenced‑блоки `check` применяйте как быстрый способ отладки.
