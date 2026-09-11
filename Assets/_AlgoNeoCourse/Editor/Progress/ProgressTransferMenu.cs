using System;
using System.Collections.Generic;
using System.IO;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Quizzes;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Progress
{
    // Ручной перенос прогресса между машинами/проектами через JSON.
    public static class ProgressTransferMenu
    {
        [MenuItem("Tools/AlgoNeoCourse/Progress/Export JSON...")]
        public static void ExportJson()
        {
            try
            {
                string currentFull =
                    AlgoNeoPackageAssetLocator.ToAbsolutePath(CourseProgressStore.GetProgressFileAssetPath());
                if (!File.Exists(currentFull))
                {
                    EditorUtility.DisplayDialog("AlgoNeoCourse",
                        "Файл прогресса не найден, экспортировать нечего.", "OK");
                    return;
                }

                string dest = EditorUtility.SaveFilePanel("Экспорт прогресса",
                    Application.dataPath, "algo-neocourse-progress", "json");
                if (string.IsNullOrEmpty(dest))
                {
                    return;
                }

                File.Copy(currentFull, dest, true);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("AlgoNeoCourse", "Прогресс экспортирован:\n" + dest, "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("AlgoNeoCourse", "Ошибка экспорта: " + ex.Message, "OK");
            }
        }

        [MenuItem("Tools/AlgoNeoCourse/Progress/Import JSON...")]
        public static void ImportJson()
        {
            try
            {
                string src = EditorUtility.OpenFilePanel("Импорт прогресса", Application.dataPath, "json");
                if (string.IsNullOrEmpty(src))
                {
                    return;
                }

                string json = File.ReadAllText(src);
                CourseProgressData data = JsonConvert.DeserializeObject<CourseProgressData>(json);
                if (data == null)
                {
                    throw new InvalidDataException("Файл не похож на прогресс AlgoNeoCourse.");
                }

                data.lessonStates ??= new Dictionary<string, LessonQuizState>();

                string assetPath = CourseProgressStore.GetProgressFileAssetPath();
                string dirAssetPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ??
                                      AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath;
                AlgoNeoPackageAssetLocator.EnsureProjectFolder(dirAssetPath);

                string currentFull = AlgoNeoPackageAssetLocator.ToAbsolutePath(assetPath);
                // Бэкап текущего прогресса перед перезаписью.
                if (File.Exists(currentFull))
                {
                    File.Copy(currentFull, currentFull + ".bak", true);
                }

                File.WriteAllText(currentFull, JsonConvert.SerializeObject(data, Formatting.Indented));
                CourseProgressStore.ResetInMemory();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("AlgoNeoCourse",
                    "Прогресс импортирован (старый сохранён в *.bak).\nПереоткройте окно курса.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("AlgoNeoCourse", "Ошибка импорта: " + ex.Message, "OK");
            }
        }
    }
}
