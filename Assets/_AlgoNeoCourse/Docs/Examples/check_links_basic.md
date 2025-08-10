# Примеры стабильных проверок (ссылки `unity ://check`)

Создайте объект `Player` и добавьте на него компонент `Rigidbody`, затем попробуйте кнопки:

- Проверка наличия объекта:
  Прямая команда: 
  ```(пробел что бы не заменялась)
  unity ://check?type=object-exists&target=Player
  ```
  [▶ Проверить](unity://check?type=object-exists&target=Player)

- Проверка наличия компонента:
  Прямая команда:
  ```(пробел что бы не заменялась)
  unity ://check?type=component-present&target=Player&component=Rigidbody
  ```
  [▶ Проверить](unity://check?type=component-present&target=Player&component=Rigidbody)

Подсказка: сообщения об успехе/ошибке показываются в диалоге или консоли в зависимости от настроек Validation.
