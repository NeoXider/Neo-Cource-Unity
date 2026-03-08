# Чеклист сборки сцены в Unity (версия 1)

Ниже пошаговая сборка сцены руками в редакторе.

## 1. Создай сцену

1. Открой `Assets/TicTacToe/Scenes`.
2. Создай сцену `TicTacToe.unity`.
3. Сохрани её сразу в эту папку.

## 2. Создай GameManager

1. Создай пустой объект `GameManager`.
2. Повесь на него:
   - `TicTacToeGame`
   - `TicTacToeUI`
   - `TicTacToeBot`

## 3. Создай Canvas

Внутри `Canvas` создай:

- `StatusText` через TextMeshPro
- `ModePanel`
- `GamePanel`

## 4. Настрой ModePanel

Внутри `ModePanel` создай две кнопки:

- `TwoPlayersButton`
- `BotButton`

## 5. Настрой GamePanel

1. Создай внутри `GamePanel` объект-контейнер, например `Board`.
2. Сделай сетку `3 x 3`.
3. Создай 9 UI-кнопок:
   - `Cell0`
   - `Cell1`
   - `Cell2`
   - `Cell3`
   - `Cell4`
   - `Cell5`
   - `Cell6`
   - `Cell7`
   - `Cell8`
4. На каждую кнопку повесь `BoardCell`.
5. Внутрь каждой кнопки добавь TextMeshPro-текст для символа `X` или `O`.

## 6. Ссылки для BoardCell

На каждой клетке проверь:

- `Button` - сама кнопка
- `Symbol Text` - текст внутри кнопки
- `Background Image` - `Image` этой же кнопки

Если включён `Auto Assign Cell Indexes` в `TicTacToeGame`, индекс руками можно не выставлять.

## 7. Ссылки для TicTacToeGame

На `GameManager` в `TicTacToeGame` назначь:

- `Board Cells` - все 9 клеток в правильном порядке
- `Ui` - компонент `TicTacToeUI`
- `Bot` - компонент `TicTacToeBot`

Порядок клеток должен быть таким:

- верхний ряд: `0 1 2`
- средний ряд: `3 4 5`
- нижний ряд: `6 7 8`

## 8. Ссылки для TicTacToeUI

На `GameManager` в `TicTacToeUI` назначь:

- `Status Text`
- `Mode Panel`
- `Game Panel`
- `Two Players Button`
- `Bot Button`

## 9. Стартовое состояние

Перед запуском лучше сделать так:

- `ModePanel` включён
- `GamePanel` выключен или включён, но неактивен для нажатий

Скрипт сам переведёт игру в режим выбора при старте.

## 10. Что проверить в Play Mode

### Режим 2 игрока

- X ходит первым
- O ходит вторым
- занятую клетку нельзя нажать
- игра определяет победу
- игра определяет ничью
- после победы поле блокируется

### Режим против бота

- игрок ходит X
- бот ходит O
- бот выбирает случайную свободную клетку
