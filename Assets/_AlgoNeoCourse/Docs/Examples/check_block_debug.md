# Пример: fenced‑блок проверки (debug)

Включите `Project Settings → AlgoNeoCourse → Validation → Debug Render Check Blocks`, затем используйте блок:

```check
# Пример YAML‑подобных правил
type: scene
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
```

Под блоком появится кнопка “▶ Проверить” и текстовый результат.

