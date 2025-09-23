using System;
using System.Collections.Generic;
using System.IO;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizStateStore
    {
        private static readonly Dictionary<string, LessonQuizState> s_lessonPathToState = new Dictionary<string, LessonQuizState>(StringComparer.OrdinalIgnoreCase);

        public static LessonQuizState GetLessonState(string lessonPath, bool createIfMissing = true)
        {
            if (string.IsNullOrEmpty(lessonPath)) return null;
            if (!s_lessonPathToState.TryGetValue(lessonPath, out var state) && createIfMissing)
            {
                state = LoadLessonState(lessonPath) ?? new LessonQuizState { lessonPath = lessonPath };
                s_lessonPathToState[lessonPath] = state;
            }
            return state;
        }

        public static void ResetInMemory()
        {
            s_lessonPathToState.Clear();
        }

        public static void SaveLessonState(string lessonPath)
        {
            var settings = QuizSettings.instance;
            if (!settings.persistState || !settings.saveStateAsJson) return;
            var state = GetLessonState(lessonPath, false);
            if (state == null) return;

            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(state, Newtonsoft.Json.Formatting.Indented);
                string full = ResolveStatePath(settings, lessonPath);
                var dir = Path.GetDirectoryName(full);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(full, json);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("QuizStateStore: save failed — " + ex.Message);
            }
        }

        private static LessonQuizState LoadLessonState(string lessonPath)
        {
            var settings = QuizSettings.instance;
            if (!settings.persistState || !settings.saveStateAsJson) return null;
            try
            {
                string full = ResolveStatePath(settings, lessonPath);
                if (!File.Exists(full)) return null;
                string json = File.ReadAllText(full);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<LessonQuizState>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("QuizStateStore: load failed — " + ex.Message);
                return null;
            }
        }

        private static string ResolveStatePath(QuizSettings settings, string lessonPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = settings.stateJsonFolder.Replace('\\','/');
            string safeLesson = MakeSafeFileName(lessonPath);
            string full = Path.GetFullPath(Path.Combine(projectRoot, folder, safeLesson + ".quiz.json"));
            return full;
        }

        private static string MakeSafeFileName(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                safe[i] = Array.IndexOf(invalid, c) >= 0 ? '_' : c;
            }
            return new string(safe);
        }
    }
}


