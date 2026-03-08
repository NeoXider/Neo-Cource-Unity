using UnityEngine;
using UnityEngine.Events;

namespace SpaceShooter2.Lesson4
{
    public class GameManager : MonoBehaviour
    {
        [Header("Game State")]
        [SerializeField] private int score;
        [SerializeField] private bool isGameOver;

        [Header("Difficulty")]
        [SerializeField] private float speedMultiplier = 1f;
        [SerializeField] private float speedStep = 0.2f;
        [SerializeField] private float difficultyRate = 10f;

        [Header("Events")]
        [SerializeField] private ScoreChangedEvent onScoreChanged = new ScoreChangedEvent();
        [SerializeField] private UnityEvent onGameOver = new UnityEvent();

        public int Score => score;
        public bool IsGameOver => isGameOver;
        public float SpeedMultiplier => speedMultiplier;
        public ScoreChangedEvent OnScoreChanged => onScoreChanged;
        public UnityEvent OnGameOver => onGameOver;

        public void Start()
        {
            onScoreChanged.Invoke(score);
            InvokeRepeating(nameof(IncreaseDifficulty), difficultyRate, difficultyRate);
        }

        public void AddScore(int points)
        {
            if (isGameOver)
            {
                return;
            }

            score += points;
            onScoreChanged.Invoke(score);
        }

        public void FinishGame()
        {
            if (isGameOver)
            {
                return;
            }

            isGameOver = true;
            CancelInvoke(nameof(IncreaseDifficulty));
            onGameOver.Invoke();
        }

        public void IncreaseDifficulty()
        {
            if (isGameOver)
            {
                return;
            }

            speedMultiplier += speedStep;
        }
    }
}
