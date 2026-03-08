using System.Collections.Generic;
using UnityEngine;

namespace TicTacToeV1
{
public class TicTacToeGame : MonoBehaviour
{
    public List<BoardCell> boardCells = new List<BoardCell>();
    public TicTacToeUI ui;
    public TicTacToeBot bot;
    public string xSymbol = "X";
    public string oSymbol = "O";
    public bool autoAssignCellIndexes = true;

    public readonly int[] board = new int[9];
    public int currentPlayer = 1;
    public bool isGameOver;
    public bool playWithBot;
    public bool gameStarted;

    public void Start()
    {
        PrepareBoardCells();
        ui.Bind(this);
        ShowModeSelection();
    }

    public void HandleCellClicked(int cellIndex)
    {
        if (!gameStarted || isGameOver)
        {
            return;
        }

        if (cellIndex < 0 || cellIndex >= board.Length)
        {
            return;
        }

        if (board[cellIndex] != 0)
        {
            return;
        }

        MakeMove(cellIndex);

        if (playWithBot && !isGameOver && currentPlayer == 2)
        {
            MakeBotMove();
        }
    }

    public void StartTwoPlayersGame()
    {
        playWithBot = false;
        StartNewMatch();
    }

    public void StartBotGame()
    {
        playWithBot = true;
        StartNewMatch();
    }

    public void ShowModeSelection()
    {
        gameStarted = false;
        isGameOver = false;
        ResetBoard();
        SetBoardInteractable(false);
        ui.ShowModeSelection();
        ui.SetStatusText("Выберите режим игры");
    }

    public void StartNewMatch()
    {
        gameStarted = true;
        isGameOver = false;
        currentPlayer = 1;
        ResetBoard();
        SetBoardInteractable(true);
        ui.ShowGamePanel();
        UpdateStatusText();
    }

    public void PrepareBoardCells()
    {
        for (int i = 0; i < boardCells.Count; i++)
        {
            BoardCell cell = boardCells[i];

            int cellIndex = cell.CellIndex;
            if (autoAssignCellIndexes)
            {
                cellIndex = i;
            }

            cell.Setup(this, cellIndex);
        }
    }

    public void MakeMove(int cellIndex)
    {
        board[cellIndex] = currentPlayer;
        UpdateCell(cellIndex);

        if (CheckWinner(currentPlayer))
        {
            FinishGameWithWinner(currentPlayer);
            return;
        }

        if (IsBoardFull())
        {
            FinishGameWithDraw();
            return;
        }

        SwitchPlayer();
        UpdateStatusText();
    }

    public void MakeBotMove()
    {
        int botMove = bot.ChooseMove(board);

        if (botMove < 0)
        {
            botMove = GetFirstFreeCell();
        }

        if (botMove >= 0)
        {
            MakeMove(botMove);
        }
    }

    public void UpdateCell(int cellIndex)
    {
        foreach (BoardCell cell in boardCells)
        {
            if (cell.CellIndex == cellIndex)
            {
                if (board[cellIndex] == 1)
                {
                    cell.SetSymbol(xSymbol);
                }
                else if (board[cellIndex] == 2)
                {
                    cell.SetSymbol(oSymbol);
                }

                cell.SetInteractable(false);
                break;
            }
        }
    }

    public void ResetBoard()
    {
        for (int i = 0; i < board.Length; i++)
        {
            board[i] = 0;
        }

        foreach (BoardCell cell in boardCells)
        {
            cell.ClearCell();
        }
    }

    public void SetBoardInteractable(bool isInteractable)
    {
        foreach (BoardCell cell in boardCells)
        {
            bool canClick = isInteractable && board[cell.CellIndex] == 0;
            cell.SetInteractable(canClick);
        }
    }

    public void FinishGameWithWinner(int winner)
    {
        isGameOver = true;
        SetBoardInteractable(false);

        if (winner == 1)
        {
            ui.SetStatusText("Победил игрок X");
        }
        else
        {
            ui.SetStatusText("Победил игрок O");
        }
    }

    public void FinishGameWithDraw()
    {
        isGameOver = true;
        SetBoardInteractable(false);
        ui.SetStatusText("Ничья");
    }

    public void UpdateStatusText()
    {
        if (currentPlayer == 1)
        {
            ui.SetStatusText("Ход игрока X");
        }
        else if (playWithBot)
        {
            ui.SetStatusText("Ход бота");
        }
        else
        {
            ui.SetStatusText("Ход игрока O");
        }
    }

    public void SwitchPlayer()
    {
        if (currentPlayer == 1)
        {
            currentPlayer = 2;
        }
        else
        {
            currentPlayer = 1;
        }
    }

    public bool CheckWinner(int player)
    {
        return
            MatchesLine(player, 0, 1, 2) ||
            MatchesLine(player, 3, 4, 5) ||
            MatchesLine(player, 6, 7, 8) ||
            MatchesLine(player, 0, 3, 6) ||
            MatchesLine(player, 1, 4, 7) ||
            MatchesLine(player, 2, 5, 8) ||
            MatchesLine(player, 0, 4, 8) ||
            MatchesLine(player, 2, 4, 6);
    }

    public bool MatchesLine(int player, int a, int b, int c)
    {
        return board[a] == player && board[b] == player && board[c] == player;
    }

    public bool IsBoardFull()
    {
        for (int i = 0; i < board.Length; i++)
        {
            if (board[i] == 0)
            {
                return false;
            }
        }

        return true;
    }

    public int GetFirstFreeCell()
    {
        for (int i = 0; i < board.Length; i++)
        {
            if (board[i] == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
}
