# Полный пример урока

## Введение

Это стартовый слайд урока.

---

## Теория

Картинка:

![intro image](images/intro.png)

Видео:

![intro video](https://www.example.com/intro.mp4)

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

---

## Пояснение

`private` ограничивает доступ к полю текущим классом.

---

## Практика и проверка

Добавьте `Rigidbody` на объект `Player`, затем выполните проверку:

[Проверить](unity://check?type=component-present&target=Player&component=Rigidbody)

```check
rules:
  - object_exists: "Player"
  - component_exists: { object: "Player", type: "Rigidbody" }
```

---

## Дополнительный вопрос

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

---

## Итоги

- Урок состоит из отдельных слайдов;
- теория может включать медиа и квизы;
- практика может использовать `check`.
