using UnityEngine;

namespace Lessons.M5.Y6
{
    public class BoltProjectile : MonoBehaviour
    {
        public float speed = 10f;
        public float lifeTime = 2f;
        public int damage = 1;

        // Вызывается один раз при появлении стрелы
        void Start()
        {
            // Стрела сама убирает себя через lifeTime секунд
            Destroy(gameObject, lifeTime);
        }

        // Вызывается каждый кадр
        void Update()
        {
            // Летим вперёд — туда, куда смотрит нос стрелы
            transform.position += transform.up * speed * Time.deltaTime;
        }

        // Вызывается, когда стрела коснулась другого коллайдера
        void OnTriggerEnter2D(Collider2D other)
        {
            EnemyUnit enemy = other.GetComponent<EnemyUnit>();

            // Это не враг — летим дальше
            if (enemy == null)
            {
                return;
            }

            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
