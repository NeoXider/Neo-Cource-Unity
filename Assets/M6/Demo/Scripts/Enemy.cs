using UnityEngine;

namespace Lessons.M6.Y4
{
    public class Enemy : MovingObject
    {
        public int maxHealth = 2;
        public int scoreValue = 10;
        public GameObject bonusPrefab;
        public float bonusChance = 0.2f;
        public GameObject explosionPrefab;

        private int health;

        // Вызывается один раз при появлении врага
        void Start()
        {
            health = maxHealth;
        }

        // Получает урон от пули
        public void TakeDamage(int damage)
        {
            // Уже сбитый враг урон не получает
            if (health <= 0)
            {
                return;
            }

            health -= damage;

            if (health <= 0)
            {
                Die();
            }
        }

        // Гибель: очки, иногда бонус и взрыв
        private void Die()
        {
            // Контроллер игры на сцене один — находим его и отдаём очки
            ShooterController gameController = FindAnyObjectByType<ShooterController>();
            gameController.AddScore(scoreValue);

            // Бонус выпадает, если он назначен и повезло: Random.value — случайное число от 0 до 1
            if (bonusPrefab != null && Random.value < bonusChance)
            {
                Instantiate(bonusPrefab, transform.position, Quaternion.identity);
            }

            // Взрыв — только если префаб эффекта назначен
            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        // Вызывается, когда враг коснулся другого коллайдера
        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerHealth player = other.GetComponent<PlayerHealth>();

            // Это не корабль игрока — летим дальше
            if (player == null)
            {
                return;
            }

            player.LoseLife();
            Destroy(gameObject);
        }
    }
}
