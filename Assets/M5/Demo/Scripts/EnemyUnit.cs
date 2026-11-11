using UnityEngine;

namespace Lessons.M5.Y6
{
    public class EnemyUnit : MonoBehaviour
    {
        public int maxHealth = 3;
        public float moveSpeed = 1.2f;
        public int wallDamage = 1;
        public int reward = 2;
        public GameObject deathEffectPrefab;

        private int currentHealth;

        // Вызывается один раз при появлении врага
        void Start()
        {
            currentHealth = maxHealth;
        }

        // Вызывается каждый кадр
        void Update()
        {
            // Идём вниз, к стене
            transform.position += Vector3.down * moveSpeed * Time.deltaTime;
        }

        // Получает урон от стрелы
        public virtual void TakeDamage(int damage)
        {
            // Уже побеждённый враг урон не получает
            if (currentHealth <= 0)
            {
                return;
            }

            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        // Гибель врага: награда, брызги и исчезновение
        protected virtual void Die()
        {
            // Кошелёк на сцене один — находим его и отдаём награду
            Wallet wallet = FindAnyObjectByType<Wallet>();
            wallet.AddCoins(reward);

            // Брызги — только если префаб эффекта назначен
            if (deathEffectPrefab != null)
            {
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        // Вызывается, когда враг коснулся другого коллайдера
        void OnTriggerEnter2D(Collider2D other)
        {
            DefensePoint wall = other.GetComponent<DefensePoint>();

            // Это не стена — идём дальше
            if (wall == null)
            {
                return;
            }

            wall.TakeDamage(wallDamage);
            Destroy(gameObject);
        }
    }
}
