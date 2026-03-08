using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TicTacToeV1
{
[RequireComponent(typeof(Button))]
public class BoardCell : MonoBehaviour
{
    public int cellIndex;
    public Button button;
    public TMP_Text symbolText;

    public TicTacToeGame game;

    public int CellIndex
    {
        get { return cellIndex; }
    }

    public void Awake()
    {
        button = GetComponent<Button>();
        symbolText = GetComponentInChildren<TMP_Text>();
        button.onClick.AddListener(OnCellClicked);
    }

    public void OnDestroy()
    {
        button.onClick.RemoveListener(OnCellClicked);
    }

    public void Setup(TicTacToeGame ticTacToeGame, int newCellIndex)
    {
        game = ticTacToeGame;
        cellIndex = newCellIndex;
    }

    public void ClearCell()
    {
        symbolText.text = string.Empty;
        SetInteractable(true);
    }

    public void SetSymbol(string symbol)
    {
        symbolText.text = symbol;
    }

    public void SetInteractable(bool isInteractable)
    {
        button.interactable = isInteractable;
    }

    public void OnCellClicked()
    {
        game.HandleCellClicked(cellIndex);
    }
}
}
