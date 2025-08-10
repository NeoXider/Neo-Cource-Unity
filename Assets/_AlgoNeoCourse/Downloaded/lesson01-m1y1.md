# Урок 1: Базовая сцена
(Для VS CODE расширения)
- Markdown Preview Enhanced
- Path Intellisense

## Слайд 1 — Введение
В Unity сцена — это контейнер, в котором располагаются объекты.  
Объекты могут быть 3D, 2D, UI или просто пустыми.  

![Neoxider](https://avatars.githubusercontent.com/u/94991394?v=4)

---

## Слайд 2 — Задание
1. Создайте новый 3D объект типа **Cube**.
2. Переименуйте его в **"Player"**.
3. Добавьте к нему компонент **Rigidbody**.

---

## Слайд 3 — Проверка
```check
type: scene
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
---