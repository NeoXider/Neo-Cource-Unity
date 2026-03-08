# Чеклист сборки сцены в Unity

Ниже базовая сборка сцены для модуля 5.

Значения параметров в инспекторе смотри в `GameSettings.md`.

## 1. Создай сцену

1. Создай сцену `Assets/TowerDefenseRPG/Scenes/TowerDefenseRPG.unity`.
2. Сохрани её сразу в папку игры.

## 2. Создай основные объекты

На сцене создай:

- `GameManager`
- `Crossbow`
- `ShootPoint`
- `DefensePoint`
- `EnemySpawnPoint`
- `Canvas`

## 3. Настрой арбалет

На `Crossbow` повесь:

- `CrossbowShooter`

В `CrossbowShooter` назначь:

- `Shoot Point`
- `Bolt Prefab`

## 4. Создай префаб стрелы

Создай объект стрелы или болта:

- добавь визуал;
- добавь коллайдер;
- при необходимости добавь `Rigidbody`;
- повесь `BoltProjectile`.

Сохрани объект как префаб `BoltProjectile.prefab`.

## 5. Создай врага

Создай объект врага:

- добавь визуал;
- добавь коллайдер;
- повесь `EnemyUnit`.

Сохрани как минимум один префаб врага.

## 6. Настрой точку защиты

На `DefensePoint` повесь:

- `DefensePoint`

Она должна хранить:

- здоровье базы;
- урон от врага;
- состояние проигрыша.

## 7. Настрой UI

Внутри `Canvas` создай:

- `CurrencyText`
- `WaveText`
- `BaseHealthText`
- `DamageUpgradeButton`
- `AttackSpeedUpgradeButton`
- `BaseHealthUpgradeButton`

## 8. Настрой GameManager

На `GameManager` повесь:

- `GameManager`
- позже `WaveSpawner`

Назначь:

- ссылки на UI;
- ссылку на `CrossbowShooter`;
- ссылку на `DefensePoint`;
- префабы врагов и точку спавна для волн.

## 9. Что должно быть по урокам

### После M5У1

- `Crossbow`
- `ShootPoint`
- `BoltProjectile`

### После M5У2

- `EnemyUnit`
- `DefensePoint`

### После M5У4

- `GameManager`
- UI улучшений

### После M5У5

- `WaveSpawner`
- несколько префабов врагов

## 10. Что проверить в Play Mode

- стрела появляется в правильной точке;
- стрела летит вперёд;
- враг получает урон;
- база теряет здоровье;
- валюта начисляется;
- улучшения покупаются;
- волны запускаются;
- игра собирается в билд без сломанных ссылок.
