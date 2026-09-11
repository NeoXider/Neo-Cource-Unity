# Пример: проверка наличия компонента у объекта

Добавьте компонент `Rigidbody` на объект `Player` и нажмите проверку:

```check
rules:
  - component_exists:
      object: "Player"
      type: "Rigidbody"
```

> Проверка `component_exists` работает и с типами Unity (`Rigidbody`), и с полными именами (`Namespace.CustomComponent, AssemblyName`). Кнопка «Проверить» подставляется автоматически.
