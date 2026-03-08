# TicTacToe V1

Первая учебная версия игры для модуля 4.

## Структура

- `Scripts` - код игры
- `Scenes` - сюда можно сохранить сцену `TicTacToe.unity`
- `Prefabs` - сюда можно сохранить префабы клеток и UI
- `Sprites` - картинки для крестиков, ноликов и фона
- `Materials` - материалы, если понадобятся
- `Audio` - звуки для полировки

## Скрипты

- `TicTacToeGame.cs` - основная логика партии
- `BoardCell.cs` - поведение одной клетки
- `TicTacToeUI.cs` - кнопки и текст интерфейса
- `TicTacToeBot.cs` - простой бот со случайным ходом

## Какую сцену собрать в Unity

1. Создай сцену `Assets/TicTacToe/Scenes/TicTacToe.unity`.
2. Добавь `Canvas`.
3. Внутри `Canvas` создай:
   - `ModePanel`
   - `GamePanel`
   - `StatusText`
4. Внутри `ModePanel` создай две кнопки:
   - `TwoPlayersButton`
   - `BotButton`
5. Внутри `GamePanel` создай поле `3 x 3` из девяти UI-кнопок.
6. На каждую клетку повесь `BoardCell`.
7. Создай объект `GameManager` и повесь на него:
   - `TicTacToeGame`
   - `TicTacToeUI`
   - `TicTacToeBot`
8. Назначь ссылки в инспекторе.

## Как это раскладывается по урокам

- `M4У1` - массив поля, ходы, победа, ничья
- `M4У2` - `List<BoardCell>` и `foreach`
- `M4У3` - режимы игры и подготовка одиночного режима
- `M4У4` - простой бот и `Random.Range`
- `M4У5` - полировка, тесты, доводка UI
