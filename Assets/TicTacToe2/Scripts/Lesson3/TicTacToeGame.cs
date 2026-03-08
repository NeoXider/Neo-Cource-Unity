using System.Collections.Generic;
using UnityEngine;

namespace TicTacToe2.Lesson3
{
    public class TicTacToeGame : MonoBehaviour
    {
        public List<BoardCell> boardCells = new List<BoardCell>();
        public string xSymbol = "X";
        public string oSymbol = "O";
        public int[] board = new int[9];
        public int currentPlayer = 1;
        public bool isGameOver;

        public void Start()
        {
            PrepareBoardCells();
            ResetBoard();
        }

        public void PrepareBoardCells()
        {
            for (int i = 0; i < boardCells.Count; i++)
            {
                boardCells[i].Setup(this, i);
            }
        }

        public void HandleCellClicked(int cellIndex)
        {
            if (isGameOver)
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
        }

        public void UpdateCell(int cellIndex)
        {
            BoardCell cell = boardCells[cellIndex];

            if (board[cellIndex] == 1)
            {
                cell.SetSymbol(xSymbol);
            }
            else if (board[cellIndex] == 2)
            {
                cell.SetSymbol(oSymbol);
            }

            cell.SetInteractable(false);
        }

        public void ResetBoard()
        {
            currentPlayer = 1;
            isGameOver = false;

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
                bool canClick = isInteractable && board[cell.cellIndex] == 0;
                cell.SetInteractable(canClick);
            }
        }

        public void FinishGameWithWinner(int winner)
        {
            isGameOver = true;
            SetBoardInteractable(false);
            Debug.Log("Победил игрок " + winner);
        }

        public void FinishGameWithDraw()
        {
            isGameOver = true;
            SetBoardInteractable(false);
            Debug.Log("Ничья");
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
    }
}
