# Чеклист сборки сцены в Unity

Точные рекомендуемые значения для инспектора смотри в `GameSettings.md`.

## 1. Создай сцену

1. Открой `Assets/SpaceShooter2/Scenes`.
2. Создай сцену `SpaceShooter.unity`.
3. Сохрани её сразу в эту папку.

## 2. Создай основные объекты

На сцене удобно сразу сделать:

- `GameManager`
- `Player`
- `Background`
- `Main Camera`
- `Canvas`

## 3. Что нужно внутри Canvas

Минимальный набор UI:

- `ScoreText`
- `StatusText`
- `GameOverPanel`

Внутри `GameOverPanel` можно сделать:

- текст `Game Over`
- кнопку перезапуска, если она понадобится на полировке

## 4. Что нужно для игрока

На объекте `Player`:

- `SpriteRenderer`
- `Collider2D`
- `PlayerShipController`

Отдельним дочерним объектом удобно сделать:

- `FirePoint`

## 5. Что нужно для пули игрока

Префаб пули игрока:

- `SpriteRenderer`
- `Collider2D` с `Is Trigger`
- `ProjectileMover`

## 6. Что нужно для астероида

Префаб астероида:

- `SpriteRenderer`
- `Collider2D` с `Is Trigger`
- `AsteroidMover`

## 7. Что нужно для врага

Префаб врага:

- `SpriteRenderer`
- `Collider2D` с `Is Trigger`
- `EnemyShip`

У врага тоже удобно сделать дочерний объект:

- `FirePoint`

## 8. Что нужно для вражеской пули

Префаб вражеской пули:

- `SpriteRenderer`
- `Collider2D` с `Is Trigger`
- `EnemyProjectileMover`

## 9. Что нужно для спавнера

На объект `Spawner`:

- `TimedSpawner`

В нём назначаются:

- префаб астероида
- префаб врага
- верхняя граница спавна
- левая и правая границы

## 10. Что нужно для GameManager

На объект `GameManager`:

- `GameManager`
- `GameHud`

В `GameManager` назначаются:

- стартовые очки
- множитель темпа
- события `OnScoreChanged`
- событие `OnGameOver`

В `GameHud` назначаются:

- `ScoreText`
- `StatusText`
- `GameOverPanel`
- ссылка на `GameManager`

## 11. Что проверить в Play Mode

### После урока 1

- игрок двигается
- игрок стреляет
- пуля исчезает сама

### После урока 2

- астероиды спавнятся
- враги спавнятся
- враги стреляют
- объекты не зависают за пределами экрана

### После урока 3

- очки увеличиваются
- `Game Over` срабатывает
- UI обновляется через события
- игра постепенно ускоряется

### После урока 4

- UI читается
- звуки и эффекты не мешают
- темп игры ощущается нормально
- билд запускается
