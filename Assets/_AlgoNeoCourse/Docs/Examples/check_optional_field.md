# Пример: дополнительные проверки и поле камеры

Создайте объект `Main Camera` с компонентом `Camera`; размер камеры и строка в `PlayerController.cs` проверяются как рекомендации и не мешают зачёту.

```check
rules:
  - object_exists: "Main Camera"
  - field:
      object: "Main Camera"
      type: "Camera"
      name: "orthographicSize"
      equals: 5
      optional: true
  - filename: "PlayerController.cs"
    optional: true
  - contains: "Debug.Log"
    optional: true
```

> Для `contains` нужен `filename`; здесь обе проверки файла дополнительные, поэтому для зачёта достаточно объекта, а для «идеально» должны пройти все пункты.
