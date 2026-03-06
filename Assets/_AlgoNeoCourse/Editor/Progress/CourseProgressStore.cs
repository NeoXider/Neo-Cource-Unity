using System;
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

        public static CourseProgressData GetData()
        {
            if (s_cachedData == null)
            {
                s_cachedData = LoadFromDisk() ?? new CourseProgressData();
            }

            if (s_cachedData.lessonStates == null)
            {
                s_cachedData.lessonStates = new System.Collections.Generic.Dictionary<string, LessonQuizState>();
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

            var data = GetData();
            if (!data.lessonStates.TryGetValue(key, out var state) && createIfMissing)
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
            var data = GetData();
            data.lastLessonPath = lessonPath ?? string.Empty;
            data.lastSlideIndex = Math.Max(0, slideIndex);
            SaveToDisk();
        }

        public static bool TryGetLastSession(out string lessonPath, out int slideIndex)
        {
            var data = GetData();
            lessonPath = data.lastLessonPath ?? string.Empty;
            slideIndex = Math.Max(0, data.lastSlideIndex);
            return !string.IsNullOrWhiteSpace(lessonPath);
        }

        public static void SaveToDisk()
        {
            try
            {
                string assetPath = GetProgressFileAssetPath();
                string directoryAssetPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath;
                AlgoNeoPackageAssetLocator.EnsureProjectFolder(directoryAssetPath);

                string fullPath = AlgoNeoPackageAssetLocator.ToAbsolutePath(assetPath);
                string json = JsonConvert.SerializeObject(GetData(), Formatting.Indented);
                File.WriteAllText(fullPath, json);
                AssetDatabase.Refresh();
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
            return Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath;
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
