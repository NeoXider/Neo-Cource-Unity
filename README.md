# AlgoNeoCourse

Версия пакета: `1.4.0`

![AlgoNeoCourse screenshot 1](https://github.com/user-attachments/assets/d54299b1-1739-4d7b-a2ef-6a9565eb723b)
![AlgoNeoCourse screenshot 2](https://github.com/user-attachments/assets/2f195a94-42c2-43b1-bb11-3b2c0d116c7d)
![AlgoNeoCourse screenshot 3](https://github.com/user-attachments/assets/ff83b6c4-32a4-4b9b-933d-fb8c43f1e81e)
![AlgoNeoCourse screenshot 4](https://github.com/user-attachments/assets/db08e0b5-61d0-4c3a-9e60-cc2ec7f19939)

## О проекте

`AlgoNeoCourse` это editor-пакет для Unity, который показывает учебные уроки прямо в `Unity Editor`:

- Markdown-слайды с навигацией по урокам.
- Встроенные `check`-проверки.
- Мини-квизы `single`, `multiple`, `truefalse`.
- Локальное JSON-сохранение прогресса.
- Поддержка медиа, ссылок и GIF → MP4 конвертации.

Репозиторий: [AlgoNeoCource](https://github.com/NeoXider/AlgoNeoCource)

Подробная спецификация уроков: `Assets/_AlgoNeoCourse/Docs/CourseMarkdownSpec.md`

## Установка

Рекомендуемый способ установки: через `Package Manager` по `Git URL`.

1. Откройте `Window -> Package Manager`.
2. Нажмите `+ -> Add package from git URL...`.
3. Укажите путь к пакету внутри репозитория:

```text
https://github.com/NeoXider/AlgoNeoCource.git?path=Assets/_AlgoNeoCourse
```

Пакет включает:

- встроенный `MarkdownRenderer`;
- собственную editor-сборку через `asmdef`;
- зависимость от `com.unity.nuget.newtonsoft-json`.

Если `Newtonsoft.Json` не подтянулся автоматически, установите его вручную:

```text
com.unity.nuget.newtonsoft-json
```

## Быстрый старт

1. Откройте окно курса: `Tools -> AlgoNeoCourse -> Open Course Window`.
2. Откройте настройки курса: `Tools -> AlgoNeoCourse -> Settings -> Open Course Settings`.
3. Загрузите `course.json`.
4. Скачайте выбранные уроки.
5. Переключайтесь по слайдам и проходите квизы.

## Что умеет окно курса

- список уроков и навигация по слайдам;
- перезагрузка текущего урока;
- открытие текущего `.md` в проводнике;
- debug-меню `Docs` с примерами из `Docs/Examples`;
- локальный сброс прогресса прямо из окна.

Горячие клавиши:

- `Left` / `Right` — переключение слайдов;
- `R` — перезагрузка урока;
- `O` — показать текущий `.md` в проводнике.

## Локальное сохранение

Прогресс курса сохраняется автоматически в локальный JSON:

- сохраняется последний открытый урок;
- сохраняется текущий слайд;
- сохраняется состояние всех квизов по всем урокам;
- предыдущие квизы не нужно проходить заново после перезапуска Unity.

Файл прогресса по умолчанию:

```text
Assets/_AlgoNeoCourse/Progress/course-progress.json
```

Сброс прогресса:

- кнопка сброса в тулбаре окна курса;
- `Tools -> AlgoNeoCourse -> Settings -> Reset Course Progress`;
- кнопка `Очистить сохранения` в `Quiz Settings`.

## Настройки

Основные настройки доступны через:

- `Tools -> AlgoNeoCourse -> Settings -> Open Course Settings`
- `Tools -> AlgoNeoCourse -> Settings -> Open Quiz Settings`
- `Tools -> AlgoNeoCourse -> Settings -> Open Validation Settings`

Наиболее важные параметры `Quiz Settings`:

- `maxAttemptsPerQuestion`
- `randomizeAnswersOnCourseOpen`
- `guardSlideNavigation`
- `stateJsonFolder`

## Как писать уроки

Один урок это один `.md` файл, который перечислен в `course.json`.

Разделитель слайдов:

```md
---
```

Поддерживаемые пути к медиа:

- абсолютные URL: `http://`, `https://`;
- относительные пути от текущего `.md`;
- проектные пути: `Assets/...`, `Packages/...`;
- поиск по имени файла: `![](logo.png)`.

Пример `check`-блока:

````md
```check
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
```
````

Пример квиза:

````md
```quiz
id: sc-hello
kind: single
text: Какой модификатор доступа делает поле приватным в C#?
answers:
  - text: private
    correct: true
  - text: public
  - text: protected
  - text: internal
```
````

## Мини-шаблоны

Шаблон урока:

````md
# Введение
Коротко о целях.

---

## Теория
![](images/intro.png)

---

## Вопрос
```quiz
id: q-1
kind: truefalse
text: Метод Update вызывается один раз за кадр.
answers:
  - text: True
    correct: true
  - text: False
```
````

Шаблон открытой проверки:

````md
```check
rules:
  - object_exists: "Player"
  - component_exists: { object: "Player", type: "Rigidbody" }
```
````

## Зависимости и примечания

- `MarkdownRenderer` уже встроен в пакет и не требует отдельной установки.
- `Newtonsoft.Json` это единственная внешняя зависимость.
- Для GIF → MP4 нужен `ffmpeg.exe`, если вы хотите использовать авто-конвертацию.

## FAQ

- `Относительные пути к медиа не работают`
Проверьте, что путь задан относительно папки текущего `.md`.

- `Горячие клавиши не работают`
Кликните в тело окна курса, чтобы оно получило фокус. Переход вперёд может блокироваться незавершёнными вопросами.

- `GIF не анимируется`
Используйте `.mp4` или настройте путь к `ffmpeg`.

- `Появляется сообщение про MarkdownRenderer`
В актуальной версии пакет использует встроенный `MarkdownRenderer`; отдельная установка не требуется.

## Материалы

- релизы и исходники: [AlgoNeoCource](https://github.com/NeoXider/AlgoNeoCource)
- примеры уроков: `Assets/_AlgoNeoCourse/Docs/Examples`
