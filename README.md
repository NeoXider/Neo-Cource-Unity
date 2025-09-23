## Neo‑Cource‑Unity

Версия курса: V 1.3

<img width="813" height="693" alt="image" src="https://github.com/user-attachments/assets/d54299b1-1739-4d7b-a2ef-6a9565eb723b" />
<img width="420" height="250" alt="image" src="https://github.com/user-attachments/assets/2f195a94-42c2-43b1-bb11-3b2c0d116c7d" />
<img width="418" height="193" alt="image" src="https://github.com/user-attachments/assets/ff83b6c4-32a4-4b9b-933d-fb8c43f1e81e" />
<img width="424" height="251" alt="image" src="https://github.com/user-attachments/assets/db08e0b5-61d0-4c3a-9e60-cc2ec7f19939" />

### О проекте
Neo‑Cource‑Unity — это плагин/шаблон для создания интерактивных обучающих курсов прямо в Unity с разметкой Markdown, поддержкой слайдов, встроенными проверками прогресса и мини‑викторинами.

— Репозиторий курса: [AlgoNeoCource](https://github.com/NeoXider/AlgoNeoCource)

— Документация по синтаксису уроков см. в `Assets/_AlgoNeoCourse/Docs/CourseMarkdownSpec.md` (краткие выдержки ниже).


## Быстрый старт (установка за 3 шага)

1) Установите зависимости
   - (уже есть в .packages, устанавливать не нужно) MarkdownRenderer (через Package Manager → Add package from Git URL):
     `https://github.com/UnityGuillaume/MarkdownRenderer.git`
   - (обязательно) Newtonsoft.Json (официальный пакет Unity):
     - Откройте Window → Package Manager → «+» → Add package by name
     - Введите имя пакета: `com.unity.nuget.newtonsoft-json`

2) Импортируйте курс
   - Скачайте последнюю релизную версию `.unitypackage` со страницы [релизов](https://github.com/NeoXider/AlgoNeoCource/releases)
   - Дважды кликните `.unitypackage` или импортируйте через Assets → Import Package → Custom Package…

3) Откройте окно курса
   - Открыть в Menu: Tools -> AlgoNeoCource -> Open Cource Window


## Детали: как писать уроки (Markdown)

— Один урок = один `.md` файл, перечисленный в `course.json`.

— Разделитель слайдов — строка из трёх дефисов:
```md
---
```

— Изображения и видео вставляются обычным Markdown. Видео определяется по расширению ссылки (`.mp4`, `.webm`, и т. п.):

— Пути к медиа:
  - Абсолютные URL: `http://`, `https://`
  - Относительно текущего `.md`: `./images/pic.png`, `images/pic.png`
  - Поиск по проекту по имени: `![](logo.png)`
  - Проектные пути: `Assets/...`, `Packages/...`

— Проверки (fenced‑блоки `check`), результат виден в консоли:
```md
```check
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
```
```

— Квизы (бета): `single | multiple | truefalse`.
```md
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
```

— Горячие клавиши в окне курса: Left/Right — переключение слайдов; R — перезагрузка урока; O — открыть исходный `.md`.

— Опции квизов (`Tools → AlgoNeoCourse → Settings → Open Quiz Settings`):
  - `maxAttemptsPerQuestion`, `randomizeAnswersOnCourseOpen`, `guardSlideNavigation`, `persistState`, `saveStateAsJson`, `stateJsonFolder`.


## Полезные ссылки и примечания

— MarkdownRenderer (включенная зависимость):
  - Установка через Git URL: `https://github.com/UnityGuillaume/MarkdownRenderer.git`

— Newtonsoft.Json (обязательная зависимость):
  - Пакет: `com.unity.nuget.newtonsoft-json`
  - Установка через Package Manager → Add package by name

— Конвертация GIF в видео для корректного воспроизведения в курсе:
  - Установите FFmpeg: [ffmpeg.org/download](https://ffmpeg.org/download.html)
  - Рекомендуется конвертировать `.gif` → `.mp4` (H.264) для стабильного и лёгкого воспроизведения


## Мини‑шаблоны

— Шаблон урока из нескольких слайдов:
```md
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
```

— Шаблон открытой проверки:
```md
```check
rules:
  - object_exists: "Player"
  - component_exists: { object: "Player", type: "Rigidbody" }
```
```


## Частые вопросы

— «Относительные пути к медиа не работают» → Проверьте, что путь задан относительно папки текущего `.md` и корректно написан протокол (без пробелов).

— «Горячие клавиши не работают» → Кликните в тело окна курса, чтобы оно получило фокус; переход может блокироваться незавершёнными вопросами, если включён `guardSlideNavigation`.

— «GIF не анимируется» → Используйте `.mp4` или настройте авто‑конвертацию через FFmpeg.


## Авторские материалы

— Курс и материалы: [AlgoNeoCource — релизы](https://github.com/NeoXider/AlgoNeoCource/releases)
