# Fenced‑блок проверки: сцена

Включите Debug‑режим блоков: Project Settings → AlgoNeoCourse → Validation → Debug Render Check Blocks.

```check
# Проверим наличие объекта и компонента
type: scene
rules:
  - object_exists: "Player"
  - component_exists:
      object: "Player"
      type: "Rigidbody"
```

Альтернатива через стабильные ссылки:

```
unity://check?type=object-exists&target=Player
```

```
unity://check?type=component-present&target=Player&component=Rigidbody
```
