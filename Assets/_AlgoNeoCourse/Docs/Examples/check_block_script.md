# Fenced‑блок проверки: скрипт

Проверяем наличие файла и фрагментов кода:

```check
# Если type не указан, будет определён автоматически по правилам
rules:
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
  - contains: "Update"
  - contains: "transform.Translate"
```
