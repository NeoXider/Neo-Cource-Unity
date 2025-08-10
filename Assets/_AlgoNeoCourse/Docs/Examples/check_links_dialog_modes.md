# Режимы отображения результата проверки

Можно управлять тем, где показывать результат: в диалоге или в консоли.

- В диалоге (явно):
  Прямая команда:
  ```(пробел что бы не заменялась)
  unity ://check?type=object-exists&target=Player&dialog=dialog
  ```
  [▶ Проверить](unity://check?type=object-exists&target=Player&dialog=dialog)

- Только в консоли (без диалогов):
  Прямая команда:
  ```(пробел что бы не заменялась)
  unity ://check?type=component-present&target=Player&component=Rigidbody&dialog=console
  ```
  [▶ Проверить](unity://check?type=component-present&target=Player&component=Rigidbody&dialog=console)

Если параметр не указан, используется режим из Project Settings → AlgoNeoCourse → Validation.
