using UnityEngine;

namespace TicTacToe2.Lesson1
{
    public class TicTacToeGame : MonoBehaviour
    {
        public BoardCell[] boardCells;
        public string xSymbol = "X";
        public int[] board = new int[9];

        public void Start()
        {
            PrepareBoardCells();
        }

        public void PrepareBoardCells()
        {
            for (int i = 0; i < boardCells.Length; i++)
            {
                boardCells[i].Setup(this, i);
            }
        }

        public void HandleCellClicked(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= board.Length)
            {
                return;
            }

            if (board[cellIndex] != 0)
            {
                return;
            }

            board[cellIndex] = 1;
            UpdateCell(cellIndex);
        }

        public void UpdateCell(int cellIndex)
        {
            у[cellIndex].SetSymbol(xSymbol);
        }
    }
}
