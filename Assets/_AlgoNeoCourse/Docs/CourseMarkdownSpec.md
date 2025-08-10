# Создание уроков: кратко и по шагам

- Один урок = один `.md` файл, перечисленный в `course.json`.
- Слайды разделяются строкой из трёх дефисов:
```
---
```
- Интерактив — через ссылки `unity://...` (см. раздел «Проверки и действия»).

## 1) Описать уроки в `course.json`
- Поле `lessons`: массив объектов `{ id, title, file }`.
- Пример минимума:
```json
{
  "title": "Пример",
  "lessons": [
    { "id": "lesson01", "title": "Базовая сцена", "file": "lessons/m1y1.md" },
    { "id": "lesson02", "title": "Скрипты в Unity", "file": "lessons/m1y2.md" }
  ]
}
```

## 2) Создать `.md` файл урока
Пример структуры:
```md
# Слайд 1 — Введение
Добро пожаловать!

---

## Слайд 2 — Теория
Картинка:
![](images/intro.png)

Видео:
![](https://www.example.com/intro.mp4)
```
Навигацию вперёд/назад окно плагина уже предоставляет кнопками, ссылки `[◀]/[▶]` добавлять не обязательно.

## 3) Медиа (картинки/видео)
- Изображения: `![](путь-или-url)`
- Видео: `![](путь-или-url.mp4|.mov|.webm|...)` — определяется по расширению.

Как резолвятся пути:
- Абсолютные URL: `http://` / `https://`
- Относительные пути от текущего `.md`: `![](./images/pic.png)` или `![](images/pic.png)`
- Голое имя: поиск ассета по имени в проекте (`![](logo.png)` → найдёт `Assets/**/logo.png`)
- Проектные пути: `Assets/...` и `Packages/...`

Поддерживаемые видео‑форматы: `.mp4`, `.mov`, `.webm`, `.avi`, `.wmv`, `.mpeg`, `.mpg`, `.m4v`, `.ogv`, `.asf`, `.dv`, `.vp8`.

Примечания:
- GIF выводится как статичное изображение. Если включена авто‑конвертация и настроен `ffmpeg`, внешние GIF конвертируются в `.mp4` автоматически; слайд перезагрузится сам.
- Внешние ссылки могут быть недоступны (404) — в консоли будет предупреждение, урок продолжит работу.

Мини‑примеры:
```md
# Изображения
![](sample.jpg)                 # поиск по проекту
![](images/sample.jpg)          # относительный путь
![](https://site/pic.jpg)       # внешний URL

# Видео
![](https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4)
```

## 4) Проверки и действия (`unity://`)
- Открыть ассет: `unity://open?path=Assets/Prefabs/Player.prefab`
- Скрытые проверки (через ссылку):
  - `object-exists`: `unity://check?type=object-exists&target=Player`
  - `component-present`: `unity://check?type=component-present&target=Player&component=Rigidbody`
  - Сложная сцена: `unity://check?type=scene-all&target=Player&components=Rigidbody,BoxCollider`
  - Для отображения команды в тексте, чтобы не превращалась в ссылку, можно писать с пробелом: 
    ```
    unity ://check?type=object-exists&target=Player   # уберите пробел в уроке
    ```

- Открытые проверки (fenced‑блоки). Включите в Project Settings: `AlgoNeoCourse → Validation → Debug Render Check Blocks`.
  Пример комбинированного блока (сцена + скрипт):
  ```
  ```check
  rules:
    - object_exists: "Player"
    - component_exists:
        object: "Player"
        type: "Rigidbody"
    - filename: "PlayerController.cs"
    - contains: "public class PlayerController"
    - contains: "Update"
  ```
  ```

Результат проверок печатается в консоль; пункты помечаются цветными V/X, есть краткие итоги.

## 5) Полный пример (3 слайда)
```md
# Введение
Перейдите далее.

---

## Теория
![](images/intro.png)
![](https://www.example.com/intro.mp4)

---

## Практика и проверка
Добавьте `Rigidbody` на объект `Player`.

[Проверить](unity://check?type=component-present&target=Player&component=Rigidbody)

```check
rules:
  - object_exists: "Player"
  - component_exists: { object: "Player", type: "Rigidbody" }
```
```

## Нельзя / ограничения
- Таблицы Markdown и встроенный HTML не поддерживаются (используйте списки/подзаголовки).
- Анимированные GIF как GIF не воспроизводятся — используйте `.mp4` (или авто‑конвертацию).
- Видео определяется по расширению ссылки.

## Рекомендации
- Держите уроки в `lessons/`, медиа — в `lessons/images/` (для экономии места можно использовать внешние ссылки).
- Один слайд — одна мысль/шаг.
- Для открытых проверок используйте `unity://check?type=...`; fenced‑блоки `check` — для открытой проверки (рекомендуется).

