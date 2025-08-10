# Открытая сложная проверка (fenced-блок)

Включите Debug‑режим блоков: Project Settings → AlgoNeoCourse → Validation → Debug Render Check Blocks.

```check
# Проверим объект, компоненты и содержимое скрипта
# Блок без type будет распознан автоматически.
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
      type: "PlayerController"
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
  - contains: "void Update()"
```
