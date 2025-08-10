# Урок 2: Скрипты в Unity


## Слайд 1 — Теория
Скрипты в Unity пишутся на C#.  
Они позволяют управлять поведением объектов: перемещать, вращать, изменять свойства.  

![C# код](https://raw.githubusercontent.com/github/explore/main/topics/csharp/csharp.png)

---

## Слайд 2 — Задание
1. Создайте скрипт `PlayerController.cs`.
2. В нём должен быть класс `PlayerController`.
3. В методе `Update()` перемещайте объект вперёд по нажатию **W**.

---

## Слайд 3 — Подсказка
```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * 5);
        }
    }
}
```

## Слайд 4 — Проверка
```check
type: script
rules:
  - filename: "PlayerController.cs"
  - contains: "public class PlayerController"
  - contains: "Update"
  - contains: "transform.Translate"
```

---
## 📌 Что в этом тестовом курсе:
- **2 урока**
- Каждый урок содержит:
  - Теоретические слайды
  - Изображения
  - Пошаговые задания
  - Автопроверку (scene и script)
- Есть разделитель `---` между слайдами
- Проверка через блоки ```check``` с разными типами (`scene` и `script`)
---