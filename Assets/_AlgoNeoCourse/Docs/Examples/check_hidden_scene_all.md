# Сложная проверка сцены одним блоком

> Один блок проверяет, что в сцене есть `Player` и на нём набор компонентов:

```check
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
  - component_exists:
      object: "Player"
      type: "BoxCollider"
  - component_exists:
      object: "Player"
      type: "MeshRenderer"
```

Кнопка «Проверить» подставляется автоматически.
