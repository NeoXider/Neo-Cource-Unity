📚 Спецификация интерактивного курса в Unity Editor
🎯 Цель
Сделать плагин для Unity Editor, который позволит проходить учебные курсы прямо внутри редактора Unity, с:

Теорией (текст, картинки, видео)

Интерактивными заданиями

Автоматической проверкой

Возможностью выбора всех уроков или отдельных

Модульным хранением контента на GitHub (Markdown + изображения)

Гибким расширением без изменения кода

🏗 Архитектура
Основные модули плагина:

CourseManager — загружает список уроков (course.json) с GitHub.

LessonLoader — подгружает Markdown-файл урока.

Markdown Renderer — преобразует Markdown в UI Toolkit интерфейс.

TaskChecker — выполняет автоматическую проверку заданий.

ProgressTracker — сохраняет, какие уроки и задания пройдены.

UI Panel — список уроков с чекбоксами + контентная панель.

📂 Хранение на GitHub
Структура репозитория:

perl
Копировать
Редактировать
my-unity-course/
│
├── lessons/
│   ├── lesson01.md
│   ├── lesson02.md
│   └── lesson03.md
│
├── images/
│   ├── rigidbody.png
│   └── gravity.gif
│
└── course.json
Пример course.json
json
Копировать
Редактировать
{
  "title": "Unity Basics",
  "description": "Базовый курс по Unity",
  "lessons": [
    { "id": "lesson01", "title": "Введение", "file": "lessons/lesson01.md" },
    { "id": "lesson02", "title": "Rigidbody", "file": "lessons/lesson02.md" }
  ]
}