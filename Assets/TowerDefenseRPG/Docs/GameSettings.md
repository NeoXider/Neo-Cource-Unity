# Настройки игры

Этот файл нужен как быстрый ориентир по значениям в инспекторе.

Здесь не описывается сборка сцены по шагам.
Для этого есть `SceneSetupChecklist.md`.

## Базовая идея

Лучше дать детям рабочие стартовые значения, чтобы:

- игра запускалась без долгого баланса;
- было проще проверять код;
- ошибки было легче искать.

---

## 1. CrossbowShooter

Объект: `Crossbow`

Скрипт: `CrossbowShooter`

### Что назначить

- `Shoot Point` - объект `ShootPoint`
- `Bolt Prefab` - префаб стрелы

### Рекомендуемые значения

- `Shoot Key` - `Space`
- `Bolt Damage` - `1`
- `Attack Cooldown` - `0.7`

### Для уроков

- `M5У1`:
  - достаточно `Shoot Point` и `Bolt Prefab`
- `M5У3`:
  - уже включаем `Attack Cooldown`
- `M5У4+`:
  - `Bolt Damage` и `Attack Cooldown` начинают меняться через улучшения

---

## 2. BoltProjectile

Объект: префаб стрелы

Скрипт: `BoltProjectile`

### Что проверить

- стрела смотрит вперёд по своей локальной оси;
- у стрелы есть коллайдер;
- при необходимости у стрелы есть `Rigidbody`;
- префаб назначен в `CrossbowShooter`.

### Рекомендуемые значения

- `Move Speed` - `12`
- `Life Time` - `3`
- `Damage` - `1`

### Что важно

- если стрела летит не туда, сначала проверяй поворот префаба;
- если попадания не работают, сначала проверяй коллайдеры и `Is Trigger`.

---

## 3. EnemyUnit

Объект: префаб врага

Скрипт: `EnemyUnit`

### Что назначить

- `Target Point` - объект базы или точка перед ней
- `Game Manager` - объект `GameManager`

### Рекомендуемые значения для обычного врага

- `Max Health` - `3`
- `Damage To Base` - `1`
- `Reward` - `2`
- `Move Speed` - `2`

### Рекомендуемые значения для быстрого врага

- `Max Health` - `2`
- `Damage To Base` - `1`
- `Reward` - `3`
- `Move Speed` - `3.5`

### Рекомендуемые значения для тяжёлого врага

- `Max Health` - `6`
- `Damage To Base` - `2`
- `Reward` - `4`
- `Move Speed` - `1.5`

---

## 4. DefensePoint

Объект: `DefensePoint`

Скрипт: `DefensePoint`

### Рекомендуемые значения

- `Max Health` - `10`
- `Current Health` на старте можно оставить таким же, как `Max Health`

### Для финальной версии

- улучшение HP базы на `M5У6`:
  - увеличение на `2`
  - стоимость улучшения `8`

---

## 5. GameManager

Объект: `GameManager`

Скрипт: `GameManager`

### Что назначить

- `Crossbow Shooter` - объект `Crossbow`
- `Defense Point` - объект `DefensePoint`
- `Currency Text` - `CurrencyText`
- `Base Health Text` - `BaseHealthText`
- `Wave Text` - `WaveText`

### Рекомендуемые значения

- `Current Currency` - `0`
- `Current Wave` - `1`
- `Damage Upgrade Cost` - `5`
- `Attack Speed Upgrade Cost` - `5`
- `Base Health Upgrade Cost` - `8`

### Что важно проверить

- валюта растёт после убийства врага;
- кнопки не ломают игру, если валюты не хватает;
- UI обновляется после покупок.

---

## 6. WaveSpawner

Объект: `GameManager` или отдельный объект `WaveSpawner`

Скрипт: `WaveSpawner`

### Что назначить

- `Enemy Prefabs` - массив префабов врагов
- `Spawn Point` - `EnemySpawnPoint`
- `Target Point` - `DefensePoint` или точка перед ним
- `Game Manager` - `GameManager`

### Рекомендуемые значения

- `Start Enemies Per Wave` - `3`
- `Time Between Spawns` - `1.2`
- `Time Between Waves` - `4`

### Для первой рабочей версии волн

- сначала можно дать только 1 префаб врага;
- потом добавить 2-3 разных префаба;
- сначала проверять волны на маленьких числах.

---

## 7. UI-кнопки улучшений

### DamageUpgradeButton

- вызывает `GameManager.TryBuyDamageUpgrade()`

### AttackSpeedUpgradeButton

- вызывает `GameManager.TryBuyAttackSpeedUpgrade()`

### BaseHealthUpgradeButton

- вызывает `GameManager.TryBuyBaseHealthUpgrade()`

### Что важно

- кнопки лучше подписать простыми текстами:
  - `+ Урон`
  - `+ Скорость`
  - `+ HP базы`

---

## 8. Быстрый стартовый баланс

Если нужна просто рабочая версия без долгой настройки, можно взять такой набор:

- арбалет:
  - `Bolt Damage = 1`
  - `Attack Cooldown = 0.7`
- база:
  - `Max Health = 10`
- обычный враг:
  - `Max Health = 3`
  - `Move Speed = 2`
  - `Reward = 2`
- волны:
  - `Start Enemies Per Wave = 3`
  - `Time Between Spawns = 1.2`
  - `Time Between Waves = 4`
- улучшения:
  - урон `5`
  - скорость `5`
  - HP базы `8`

---

## 9. Что крутить при проблемах

Если игра слишком лёгкая:

- увеличь врагам `Max Health`;
- уменьши `Time Between Spawns`;
- уменьши награду `Reward`;
- увеличь стоимость улучшений.

Если игра слишком сложная:

- уменьши врагам `Move Speed`;
- уменьши число врагов в волне;
- увеличь `Reward`;
- снизь стоимость улучшений;
- увеличь `Max Health` базы.

---

## 10. Мини-чек перед уроком

Перед занятием полезно проверить:

- все ссылки в инспекторе назначены;
- стрела создаётся из префаба;
- враг знает `Target Point`;
- `GameManager` знает UI;
- у `WaveSpawner` заполнен массив префабов;
- кнопки улучшений вызывают нужные методы.
