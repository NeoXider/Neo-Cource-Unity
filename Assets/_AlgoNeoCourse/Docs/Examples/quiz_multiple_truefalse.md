# Пример Quiz — Multiple Choice и True/False

Примеры тестов с несколькими правильными ответами и формата True/False, а также описание поведения.

## Общие правила
- Блок `quiz` — fenced‑блок в Markdown. Поля: `id`, `kind`, `text`, `answers`.
- Типы:
  - `multiple` — можно выбрать несколько вариантов, затем нажать кнопку «Проверить».
  - `truefalse` — быстрый вариант `single` с двумя ответами.
- Поведение `multiple`:
  - кликом выделяются варианты (класс `.quiz-answer--selected`),
  - проверка — по кнопке «Проверить» под вариантами,
  - правильные ответы подсвечиваются зелёным, неверно выбранные — красным.
- Попытки и перемешивание — настраиваются в `Tools → AlgoNeoCourse → Settings → Open Quiz Settings`.
- После каждого вопроса размещайте поясняющий слайд. [▶](unity://slide?dir=next)

### Стили
- Мини‑примеры fenced‑блоков с языком `truefalse` подсвечиваются стилем `.codeblock.language-truefalse`.
- В UI заголовки вопросов переносятся по словам; правильные ответы выделяются зелёным, неверно выбранные — красным.

```quiz
id: mc-unity-components
kind: multiple
text: Какие из перечисленных являются компонентами Unity?
answers:
  - text: Transform
    correct: true
  - text: Rigidbody
    correct: true
  - text: AnimatorController
  - text: SceneManager
```

```quiz
id: tf-update
kind: truefalse
text: Метод Update вызывается один раз за кадр.
answers:
  - text: True
    correct: true
  - text: False
```

---

## Пояснение (пример)

- Components в Unity — это `Transform`, `Rigidbody`, `Collider`, и пр.
- `AnimatorController` — это ассет‑контроллер для Animator, не компонент. (а Animator да компонент ^-^ )
- `SceneManager` — статический API для управления сценами, не компонент.

