# Проверка скрипта

```check
type: script
rules:
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
  - contains: "Update"
  - contains: "transform.Translate"
```

## Несуществующий файл

```check
rules:
  - filename: "NoFile.cs"
```

## Файл в котором нет else

```check
type: script
rules:
  - filename: "PlayerController.cs"
  - contains: "else"
```