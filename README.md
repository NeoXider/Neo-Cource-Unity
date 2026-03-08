# AlgoNeoCourse

Версия пакета: `1.4.1`

![AlgoNeoCourse screenshot 1](https://github.com/user-attachments/assets/d54299b1-1739-4d7b-a2ef-6a9565eb723b)
![AlgoNeoCourse screenshot 2](https://github.com/user-attachments/assets/2f195a94-42c2-43b1-bb11-3b2c0d116c7d)
![AlgoNeoCourse screenshot 3](https://github.com/user-attachments/assets/ff83b6c4-32a4-4b9b-933d-fb8c43f1e81e)
![AlgoNeoCourse screenshot 4](https://github.com/user-attachments/assets/db08e0b5-61d0-4c3a-9e60-cc2ec7f19939)

## О проекте

`AlgoNeoCourse` — это Unity Editor пакет для интерактивных учебных курсов прямо внутри редактора.

Пакет умеет:

- показывать уроки из Markdown;
- переключать слайды в окне курса;
- выполнять `check`-проверки;
- запускать квизы `single`, `multiple`, `truefalse`;
- сохранять локальный прогресс в JSON;
- работать с изображениями, видео и GIF -> MP4.

Репозиторий: [Neo-Cource-Unity](https://github.com/NeoXider/Neo-Cource-Unity)

Главная спецификация уроков: `Assets/_AlgoNeoCourse/Docs/CourseMarkdownSpec.md`

## Установка

Рекомендуемый способ: через UPM по Git URL.

1. Откройте `Window -> Package Manager`.
2. Нажмите `+ -> Add package from git URL...`.
3. Укажите:

```text
https://github.com/NeoXider/Neo-Cource-Unity.git?path=Assets/_AlgoNeoCourse
```

Альтернатива через `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neoxider.algoneocourse": "https://github.com/NeoXider/Neo-Cource-Unity.git?path=Assets/_AlgoNeoCourse"
  }
}
```

## Зависимости

Единственная внешняя зависимость:

```text
com.unity.nuget.newtonsoft-json
```

`MarkdownRenderer` уже встроен в пакет и отдельно ставить его не нужно.

## Быстрый старт

1. Откройте `Tools -> AlgoNeoCourse -> Settings -> Open Course Settings`.
2. Укажите `repositoryBaseUrl`, если нужен свой источник курса.
3. Выберите JSON курса:
   - `Course1`
   - `Course2`
   - `Custom`
4. Нажмите `Загрузить список уроков`.
5. Скачайте нужные уроки.
6. Откройте `Tools -> AlgoNeoCourse -> Open Course Window`.

## Окно курса

В окне курса доступны:

- выпадающий список уроков;
- навигация по слайдам;
- перезагрузка текущего урока;
- открытие текущего `.md` файла;
- сброс локального прогресса;
- меню `Docs` с примерами из `Docs/Examples`.

Горячие клавиши:

- `Left` / `Right` — предыдущий и следующий слайд;
- `R` — перезагрузить урок;
- `O` — показать текущий `.md` файл.

## Локальное сохранение

Плагин автоматически сохраняет:

- последний открытый урок;
- текущий слайд;
- состояние квизов по всем урокам.

Уже пройденные квизы не нужно проходить заново после перезапуска Unity.

Основные пользовательские папки:

- `Assets/_AlgoNeoCourse/Downloaded`
- `Assets/_AlgoNeoCourse/Progress`
- `Assets/_AlgoNeoCourse/VideoCache`

Это сделано специально, чтобы локальные данные оставались в `Assets`, а не внутри read-only UPM-пакета.

## Настройки

Основные окна настроек:

- `Tools -> AlgoNeoCourse -> Settings -> Open Course Settings`
- `Tools -> AlgoNeoCourse -> Settings -> Open Quiz Settings`
- `Tools -> AlgoNeoCourse -> Settings -> Open Validation Settings`

Что важно в `Course Settings`:

- `repositoryBaseUrl`
- выбор курса `Course1` / `Course2` / `Custom`
- папка загрузки уроков
- GIF -> MP4 конвертация
- путь к `ffmpeg`

Что важно в `Quiz Settings`:

- `maxAttemptsPerQuestion`
- `randomizeAnswersOnCourseOpen`
- `guardSlideNavigation`
- `stateJsonFolder`

## Как устроены курсы

Каждый курс описывается отдельным JSON-файлом:

- `course1.json`
- `course2.json`
- или любой другой `courseN.json` для режима `Custom`

Каждый урок — это отдельный `.md` файл, указанный в поле `file`.

Пример записи урока:

```json
{ "id": "l2m2y1", "title": "Основы синтаксиса C#", "file": "lessons2/m2/y1.md" }
```

Разделитель слайдов:

```md
---
```

## Поддерживаемые возможности Markdown

Поддерживаются:

- обычные заголовки и текст;
- fenced code blocks;
- `quiz`;
- `check`;
- ссылки `unity://open` и `unity://check`;
- изображения и видео.

Поддерживаемые пути к медиа:

- `http://` и `https://`;
- относительные пути от текущего `.md`;
- `Assets/...`;
- `Packages/...`;
- поиск по имени файла.

## Примеры

Пример `check`:

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

Пример `quiz`:

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

## GIF и видео

Важно:

- GIF как анимация не воспроизводится напрямую;
- лучше сразу использовать `.mp4`;
- при включённой авто-конвертации пакет может преобразовывать GIF в MP4;
- для авто-конвертации используется `ffmpeg`.

## FAQ

### Относительные пути к медиа не работают

Проверьте, что путь задан относительно папки текущего `.md`.

### Горячие клавиши не работают

Кликните в тело окна курса, чтобы оно получило фокус. Переход вперёд может быть заблокирован незавершёнными квизами.

### GIF не анимируется

Это ожидаемо. Используйте `.mp4` или включите GIF -> MP4 конвертацию.

### Появляется сообщение про MarkdownRenderer

В актуальной версии пакет использует встроенный `MarkdownRenderer`; отдельная установка не требуется.

## Документация

- `Assets/_AlgoNeoCourse/Docs/README.md`
- `Assets/_AlgoNeoCourse/Docs/CourseMarkdownSpec.md`
- `Assets/_AlgoNeoCourse/Docs/Examples`
