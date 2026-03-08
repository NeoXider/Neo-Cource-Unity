using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TicTacToeV1
{
public class TicTacToeUI : MonoBehaviour
{
    public TMP_Text statusText;
    public GameObject modePanel;
    public GameObject gamePanel;
    public Button twoPlayersButton;
    public Button botButton;

    public TicTacToeGame game;

    public void Bind(TicTacToeGame ticTacToeGame)
    {
        game = ticTacToeGame;
    }

    public void ShowModeSelection()
    {
        modePanel.SetActive(true);
        gamePanel.SetActive(false);
    }

    public void ShowGamePanel()
    {
        modePanel.SetActive(false);
        gamePanel.SetActive(true);
    }

    public void SetStatusText(string text)
    {
        statusText.text = text;
    }

    public void OnEnable()
    {
        twoPlayersButton.onClick.AddListener(OnTwoPlayersClicked);
        botButton.onClick.AddListener(OnBotClicked);
    }

    public void OnDisable()
    {
        twoPlayersButton.onClick.RemoveListener(OnTwoPlayersClicked);
        botButton.onClick.RemoveListener(OnBotClicked);
    }

    public void OnTwoPlayersClicked()
    {
        game.StartTwoPlayersGame();
    }

    public void OnBotClicked()
    {
        game.StartBotGame();
    }
}
}
