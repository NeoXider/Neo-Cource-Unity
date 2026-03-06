using System;
using System.Collections.Generic;
using System.Linq;
using NeoCource.Editor.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizRenderer
    {
        public static void RenderQuizzes(VisualElement root, string lessonPath, List<QuizQuestion> questions,
            Action onStateChanged = null)
        {
            if (root == null || questions == null || questions.Count == 0)
            {
                return;
            }

            QuizSettings settings = QuizSettings.instance;
            LessonQuizState lessonState = QuizStateStore.GetLessonState(lessonPath);

            foreach (QuizQuestion q in questions)
            {
                VisualElement block = new();
                block.AddToClassList("quiz-block");

                Label title = new(q.text);
                title.AddToClassList("quiz-title");
                block.Add(title);

                VisualElement answersRow = new();
                answersRow.style.flexDirection = FlexDirection.Row;
                answersRow.style.flexWrap = Wrap.Wrap;
                answersRow.AddToClassList("quiz-answers");
                block.Add(answersRow);

                if (!lessonState.questionIdToState.TryGetValue(q.id, out QuizQuestionState qState))
                {
                    qState = new QuizQuestionState
                    {
                        questionId = q.id,
                        attemptsUsed = 0,
                        isCompleted = false,
                        isCorrect = false,
                        shuffledOrder = Enumerable.Range(0, q.answers.Count).ToList()
                    };
                    // Перемешивание один раз, если включено
                    if (settings.randomizeAnswersOnCourseOpen)
                    {
                        int seed = (lessonPath + "|" + q.id).GetHashCode();
                        QuizUtils.Shuffle(qState.shuffledOrder, seed);
                    }

                    lessonState.questionIdToState[q.id] = qState;
                }

                // Кнопки ответов
                List<Button> answerButtons = new();
                Dictionary<Button, QuizAnswer> answerByButton = new();
                Button checkBtn = null;
                for (int i = 0; i < qState.shuffledOrder.Count; i++)
                {
                    int ansIdx = qState.shuffledOrder[i];
                    QuizAnswer ans = q.answers[ansIdx];
                    QuizAnswer localAns = ans; // capture
                    Button btn = new() { text = ans.text };
                    btn.userData = ans; // для подсветки после завершения
                    btn.clicked += () =>
                    {
                        if (q.kind == QuizKind.Multiple)
                        {
                            ToggleMultipleSelection(qState, localAns, btn, checkBtn);
                        }
                        else
                        {
                            OnAnswerClicked(settings, lessonPath, q, qState, localAns, block, onStateChanged);
                        }
                    };
                    btn.AddToClassList("quiz-answer");
                    btn.style.marginRight = 6;
                    btn.style.marginBottom = 6;
                    if (q.kind == QuizKind.Multiple && qState.selectedAnswerIds.Contains(ans.id))
                    {
                        btn.AddToClassList("quiz-answer--selected");
                    }

                    btn.SetEnabled(!qState.isCompleted);
                    answersRow.Add(btn);
                    answerButtons.Add(btn);
                    answerByButton[btn] = ans;
                }

                // Для multiple добавим кнопку "Проверить"
                if (q.kind == QuizKind.Multiple)
                {
                    checkBtn = new Button(() =>
                    {
                        if (qState.isCompleted)
                        {
                            return;
                        }

                        qState.attemptsUsed = Math.Max(0, qState.attemptsUsed) + 1;
                        bool correct = IsMultipleSelectionCorrect(q, qState);
                        if (correct)
                        {
                            qState.isCompleted = true;
                            qState.isCorrect = true;
                            if (settings.enableDebugLogging)
                            {
                                Debug.Log($"[Quiz] multiple OK: {q.id}");
                            }
                        }
                        else if (qState.attemptsUsed >= Math.Max(1, settings.maxAttemptsPerQuestion))
                        {
                            qState.isCompleted = true;
                            qState.isCorrect = false;
                            if (settings.enableDebugLogging)
                            {
                                Debug.Log($"[Quiz] multiple FAIL (exhausted): {q.id}");
                            }
                        }

                        UpdateAfterStateChange(settings, lessonPath, qState, block, answerButtons, onStateChanged);
                    }) { text = "Проверить" };
                    checkBtn.style.marginBottom = 6;
                    checkBtn.SetEnabled(qState.selectedAnswerIds.Count > 0 && !qState.isCompleted);
                    answersRow.Add(checkBtn);
                }

                // Результат
                Label resultLabel = new(ComposeResultText(settings, qState));
                resultLabel.AddToClassList("quiz-result");
                UpdateResultStyle(resultLabel, qState);
                block.Add(resultLabel);

                root.Add(block);

                // Если завершено, подсветим правильный/неправильный выбор
                if (qState.isCompleted)
                {
                    HighlightAnswersAfterComplete(q, qState, answerButtons, answerByButton);
                }
            }
        }

        private static void ToggleMultipleSelection(QuizQuestionState qState, QuizAnswer ans, Button btn,
            Button checkBtn)
        {
            if (qState.isCompleted)
            {
                return;
            }

            if (qState.selectedAnswerIds.Contains(ans.id))
            {
                qState.selectedAnswerIds.Remove(ans.id);
                btn.RemoveFromClassList("quiz-answer--selected");
            }
            else
            {
                qState.selectedAnswerIds.Add(ans.id);
                btn.AddToClassList("quiz-answer--selected");
            }

            if (checkBtn != null)
            {
                checkBtn.SetEnabled(qState.selectedAnswerIds.Count > 0);
            }
        }

        private static bool IsMultipleSelectionCorrect(QuizQuestion q, QuizQuestionState qState)
        {
            string[] correctIds = q.answers.Where(a => a.isCorrect).Select(a => a.id).OrderBy(x => x).ToArray();
            string[] chosen = qState.selectedAnswerIds.OrderBy(x => x).ToArray();
            if (correctIds.Length == 0)
            {
                return false;
            }

            if (chosen.Length != correctIds.Length)
            {
                return false;
            }

            for (int i = 0; i < correctIds.Length; i++)
            {
                if (!string.Equals(correctIds[i], chosen[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void UpdateAfterStateChange(QuizSettings settings, string lessonPath, QuizQuestionState qState,
            VisualElement block, List<Button> answerButtons, Action onStateChanged)
        {
            Label resultLabel = block.Q<Label>(className: "quiz-result");
            if (resultLabel != null)
            {
                resultLabel.text = ComposeResultText(settings, qState);
                UpdateResultStyle(resultLabel, qState);
            }

            if (qState.isCompleted && answerButtons != null)
            {
                foreach (Button b in answerButtons)
                {
                    b.SetEnabled(false);
                }

                // Подсветка правильных/неправильных
                foreach (Button b in answerButtons)
                {
                    b.RemoveFromClassList("quiz-answer--selected");
                    b.RemoveFromClassList("quiz-answer--correct");
                    b.RemoveFromClassList("quiz-answer--wrong");
                    QuizAnswer a = b.userData as QuizAnswer;
                    if (a == null)
                    {
                        continue;
                    }

                    if (a.isCorrect)
                    {
                        b.AddToClassList("quiz-answer--correct");
                    }
                    else
                    {
                        // Неправильные подсвечиваем, если они были выбраны
                        if (qState.selectedAnswerIds != null && qState.selectedAnswerIds.Contains(a.id))
                        {
                            b.AddToClassList("quiz-answer--wrong");
                        }
                    }
                }
            }

            QuizStateStore.SaveLessonState(lessonPath);
            onStateChanged?.Invoke();
        }

        private static void HighlightAnswersAfterComplete(QuizQuestion q, QuizQuestionState qState,
            List<Button> buttons, Dictionary<Button, QuizAnswer> map)
        {
            foreach (Button b in buttons)
            {
                if (!map.TryGetValue(b, out QuizAnswer a))
                {
                    continue;
                }

                b.RemoveFromClassList("quiz-answer--correct");
                b.RemoveFromClassList("quiz-answer--wrong");
                if (a.isCorrect)
                {
                    b.AddToClassList("quiz-answer--correct");
                }
                else if (q.kind != QuizKind.Multiple)
                {
                    // для single/truefalse подсветим неправильные клики
                    if (!qState.isCorrect)
                    {
                        b.AddToClassList("quiz-answer--wrong");
                    }
                }
            }
        }

        private static void OnAnswerClicked(QuizSettings settings, string lessonPath, QuizQuestion q,
            QuizQuestionState qState, QuizAnswer ans, VisualElement block, Action onStateChanged)
        {
            try
            {
                if (qState.isCompleted)
                {
                    return;
                }

                qState.attemptsUsed = Math.Max(0, qState.attemptsUsed) + 1;

                bool isCorrectNow = false;
                // Зафиксируем выбор для single/truefalse (нужно для подсветки неверного варианта)
                qState.selectedAnswerIds.Clear();
                qState.selectedAnswerIds.Add(ans.id);
                if (q.kind == QuizKind.Single || q.kind == QuizKind.TrueFalse)
                {
                    isCorrectNow = ans.isCorrect;
                }
                else if (q.kind == QuizKind.Multiple)
                {
                    // Для multiple в простом варианте — по одному клику проверяем по выбранному кнопочному ответу
                    isCorrectNow = ans.isCorrect;
                }

                if (isCorrectNow)
                {
                    qState.isCompleted = true;
                    qState.isCorrect = true;
                    if (settings.enableDebugLogging)
                    {
                        Debug.Log($"[Quiz] OK: {q.id}");
                    }
                }
                else if (qState.attemptsUsed >= Math.Max(1, settings.maxAttemptsPerQuestion))
                {
                    qState.isCompleted = true;
                    qState.isCorrect = false;
                    if (settings.enableDebugLogging)
                    {
                        Debug.Log($"[Quiz] FAIL (exhausted): {q.id}");
                    }
                }

                // Обновить UI и сохранить
                List<Button> buttons = block.Query<Button>(className: "quiz-answer").ToList();
                UpdateAfterStateChange(settings, lessonPath, qState, block, buttons, onStateChanged);
            }
            catch (Exception ex)
            {
                Debug.LogError("QuizRenderer: click error — " + ex.Message);
            }
        }

        private static string ComposeResultText(QuizSettings settings, QuizQuestionState s)
        {
            if (s == null)
            {
                return string.Empty;
            }

            if (!s.isCompleted)
            {
                int left = Math.Max(0, settings.maxAttemptsPerQuestion - s.attemptsUsed);
                return left > 0 ? $"Осталось попыток: {left}" : string.Empty;
            }

            return s.isCorrect ? "Верно" : "Неверно";
        }

        private static void UpdateResultStyle(Label label, QuizQuestionState s)
        {
            label.RemoveFromClassList("quiz-result--success");
            label.RemoveFromClassList("quiz-result--fail");
            if (s.isCompleted)
            {
                label.AddToClassList(s.isCorrect ? "quiz-result--success" : "quiz-result--fail");
            }
        }
    }
}