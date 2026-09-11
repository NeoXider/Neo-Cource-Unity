using System;
using System.Collections.Generic;
using System.IO;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Quizzes;
using NeoCource.Editor.Settings;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Progress
{
    public static class CourseProgressStore
    {
        private static CourseProgressData s_cachedData;

        // Флаг одноразового предупреждения о чужом lastLessonPath.
        private static bool s_ForeignPathWarned;

        // Флаг coalesced-импорта: пока delayCall висит — новые не планируем.
        private static bool s_ImportScheduled;

        public static CourseProgressData GetData()
        {
            if (s_cachedData == null)
            {
                s_cachedData = LoadFromDisk() ?? new CourseProgressData();
            }

            if (s_cachedData.lessonStates == null)
            {
                s_cachedData.lessonStates = new Dictionary<string, LessonQuizState>();
            }

            return s_cachedData;
        }

        public static LessonQuizState GetLessonState(string lessonPath, bool createIfMissing = true)
        {
            string key = NormalizeLessonPathKey(lessonPath);
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            CourseProgressData data = GetData();
            if (!data.lessonStates.TryGetValue(key, out LessonQuizState state) && createIfMissing)
            {
                state = new LessonQuizState
                {
                    lessonPath = lessonPath
                };
                data.lessonStates[key] = state;
            }

            return state;
        }

        public static void SaveLastSession(string lessonPath, int slideIndex)
        {
            CourseProgressData data = GetData();
            // Всегда прямые слеши: иначе один и тот же урок сохраняется то с '\', то с '/',
            // и сравнение/поиск при восстановлении сессии не срабатывает.
            string normalized = string.IsNullOrWhiteSpace(lessonPath)
                ? string.Empty
                : lessonPath.Replace('\\', '/');
            // Per-project изоляция: чужой путь (другой проект) не сохраняем.
            if (!string.IsNullOrEmpty(normalized) && !IsPathInsideProject(normalized))
            {
                Debug.LogWarning("CourseProgressStore: путь вне проекта, сессия не сохранена — " + normalized);
                return;
            }

            data.lastLessonPath = normalized;
            data.lastSlideIndex = Math.Max(0, slideIndex);
            data.updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SaveToDisk();
        }

        public static bool TryGetLastSession(out string lessonPath, out int slideIndex)
        {
            CourseProgressData data = GetData();
            lessonPath = data.lastLessonPath ?? string.Empty;
            slideIndex = Math.Max(0, data.lastSlideIndex);
            if (string.IsNullOrWhiteSpace(lessonPath))
            {
                return false;
            }

            // Per-project изоляция: чужой путь считаем отсутствием сейва.
            if (!IsPathInsideProject(lessonPath))
            {
                if (!s_ForeignPathWarned)
                {
                    s_ForeignPathWarned = true;
                    Debug.LogWarning("CourseProgressStore: сохранённый путь вне проекта, игнорирую — " +
                                     lessonPath);
                }

                lessonPath = string.Empty;
                return false;
            }

            return true;
        }

        public static void SaveToDisk()
        {
            try
            {
                string assetPath = GetProgressFileAssetPath();
                string directoryAssetPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ??
                                            AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath;
                AlgoNeoPackageAssetLocator.EnsureProjectFolder(directoryAssetPath);

                CourseProgressData data = GetData();
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (data.createdAtUnixMs == 0)
                {
                    data.createdAtUnixMs = nowMs;
                }

                data.updatedAtUnixMs = nowMs;

                string fullPath = AlgoNeoPackageAssetLocator.ToAbsolutePath(assetPath);
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);

                // Атомарная запись: сначала во временный файл, затем замена.
                string tmpPath = fullPath + ".tmp";
                File.WriteAllText(tmpPath, json);
                try
                {
                    File.Replace(tmpPath, fullPath, null);
                }
                catch
                {
                    // Fallback, если File.Replace недоступен (например, файла ещё нет):
                    // удаляем целевой и переносим временный.
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }

                    File.Move(tmpPath, fullPath);
                }

                ScheduleProgressImport(assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseProgressStore: save failed - " + ex.Message);
            }
        }

        public static void ResetInMemory()
        {
            s_cachedData = null;
        }

        public static void Clear()
        {
            try
            {
                string assetPath = GetProgressFileAssetPath();
                string fullPath = AlgoNeoPackageAssetLocator.ToAbsolutePath(assetPath);
                if (File.Exists(fullPath))
                {
                    FileUtil.DeleteFileOrDirectory(fullPath);
                    FileUtil.DeleteFileOrDirectory(fullPath + ".meta");
                }

                ResetInMemory();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError("CourseProgressStore: clear failed - " + ex.Message);
            }
        }

        public static string GetProgressFileAssetPath()
        {
            return QuizSettings.instance.GetProgressFileAssetPath();
        }

        public static string GetProgressDirectoryAssetPath()
        {
            string assetPath = GetProgressFileAssetPath();
            return Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ??
                   AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath;
        }

        public static string NormalizeLessonPathKey(string lessonPath)
        {
            if (string.IsNullOrWhiteSpace(lessonPath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(lessonPath).Replace('\\', '/');
            }
            catch
            {
                return lessonPath.Replace('\\', '/');
            }
        }

        // Per-project изоляция: полный путь должен лежать внутри корня проекта.
        // Относительные пути (Assets/...) резолвятся через GetFullPath
        // относительно текущей папки (в Editor это корень проекта).
        private static bool IsPathInsideProject(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string root = Path.GetFullPath(AlgoNeoPackageAssetLocator.GetProjectRoot());
                root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // Дешёвая замена AssetDatabase.Refresh(): один coalesced ImportAsset
        // через delayCall вместо тяжёлого рефреша всей базы ассетов.
        // В Clear() Refresh оставлен сознательно (редкая операция).
        private static void ScheduleProgressImport(string assetPath)
        {
            if (s_ImportScheduled)
            {
                return;
            }

            s_ImportScheduled = true;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    AssetDatabase.ImportAsset(assetPath);
                }
                finally
                {
                    s_ImportScheduled = false;
                }
            };
        }

        private static CourseProgressData LoadFromDisk()
        {
            try
            {
                string fullPath = AlgoNeoPackageAssetLocator.ToAbsolutePath(GetProgressFileAssetPath());
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                string json = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new CourseProgressData();
                }

                return JsonConvert.DeserializeObject<CourseProgressData>(json) ?? new CourseProgressData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseProgressStore: load failed - " + ex.Message);
                return new CourseProgressData();
            }
        }
    }
}