# Пример Quiz — Multiple Choice и True/False

Примеры для `multiple` и `truefalse`.

## Что важно

- `multiple` используется, когда правильных ответов несколько;
- `truefalse` — это короткое утверждение с двумя вариантами;
- для `multiple` проверка идёт кнопкой `Проверить`;
- состояние квизов сохраняется автоматически.

Настройки доступны в:

```text
Tools -> AlgoNeoCourse -> Settings -> Open Quiz Settings
```

## Пример `multiple`

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

## Пример `truefalse`

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

- `Transform` и `Rigidbody` — компоненты Unity;
- `AnimatorController` — это ассет, а не компонент;
- `SceneManager` — статический API, а не компонент.
