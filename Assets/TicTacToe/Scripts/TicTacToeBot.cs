using System.Collections.Generic;
using UnityEngine;

namespace TicTacToeV1
{
public class TicTacToeBot : MonoBehaviour
{
    public int ChooseMove(int[] board)
    {
        List<int> freeCells = GetFreeCells(board);

        if (freeCells.Count == 0)
        {
            return -1;
        }

        return GetRandomMove(freeCells);
    }

    public List<int> GetFreeCells(int[] board)
    {
        List<int> freeCells = new List<int>();

        for (int i = 0; i < board.Length; i++)
        {
            if (board[i] == 0)
            {
                freeCells.Add(i);
            }
        }

        return freeCells;
    }

    public int GetRandomMove(List<int> freeCells)
    {
        int randomIndex = Random.Range(0, freeCells.Count);
        return freeCells[randomIndex];
    }
}
}
