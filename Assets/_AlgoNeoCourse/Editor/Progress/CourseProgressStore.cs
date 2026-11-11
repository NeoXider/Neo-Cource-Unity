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
        // Версия схемы файла: 2 — уроки хранятся по пути относительно проекта (Assets/...).
        internal const int CurrentSchemaVersion = 2;

        private static CourseProgressData s_cachedData;

        // Флаг одноразового предупреждения о чужом lastLessonPath.
        private static bool s_ForeignPathWarned;

        // Флаг coalesced-импорта: пока delayCall висит — новые не планируем.
        private static bool s_ImportScheduled;

        // Текст файла до переноса ключей на относительные пути. Перед первой перезаписью
        // кладём его рядом резервной копией: прогресс ученика при миграции потерять нельзя.
        private static string s_PreMigrationJson;

        public static CourseProgressData GetData()
        {
            if (s_cachedData == null)
            {
                s_cachedData = LoadFromDisk() ?? new CourseProgressData();
            }

            if (s_cachedData.lessonStates == null)
            {
                s_cachedData.lessonStates = NewLessonStates();
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
                    lessonPath = key
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

            // Относительно проекта: сохранение переживает перенос проекта и Export/Import на другой ПК.
            data.lastLessonPath = NormalizeLessonPathKey(normalized);
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

        // Дальняя точка: только вперёд, вызывается из CourseWindow после ShowSlide.
        public static void SaveFarthestSession(string lessonPath, int slideIndex)
        {
            CourseProgressData data = GetData();
            string normalized = string.IsNullOrWhiteSpace(lessonPath)
                ? string.Empty
                : lessonPath.Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized) || !IsPathInsideProject(normalized))
            {
                return;
            }

            data.farthestLessonPath = NormalizeLessonPathKey(normalized);
            data.farthestSlideIndex = Math.Max(0, slideIndex);
            data.updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SaveToDisk();
        }

        public static bool TryGetFarthestSession(out string lessonPath, out int slideIndex)
        {
            CourseProgressData data = GetData();
            lessonPath = data.farthestLessonPath ?? string.Empty;
            slideIndex = Math.Max(0, data.farthestSlideIndex);
            if (string.IsNullOrWhiteSpace(lessonPath) || !IsPathInsideProject(lessonPath))
            {
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
                string json = SerializeProgressJson(data);

                WritePreMigrationBackup(fullPath);

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
            s_PreMigrationJson = null;
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

        // Ключ урока в прогрессе: путь ОТНОСИТЕЛЬНО проекта с прямыми слешами ("Assets/.../lesson.md").
        // Абсолютный путь зависел от диска, папки и регистра букв ("D:/GIT/..." и "D:/Git/..." давали
        // два разных урока), и прогресс терялся при переносе проекта. Сравнение ключей — без учёта регистра.
        // Файл вне проекта (временный, тестовый) остаётся с полным путём.
        public static string NormalizeLessonPathKey(string lessonPath)
        {
            return ToPortablePath(lessonPath, AlgoNeoPackageAssetLocator.GetProjectRoot(), false, null);
        }

        internal static string SerializeProgressJson(CourseProgressData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        // Разбор файла прогресса + перенос старых ключей. Используется и при загрузке, и при импорте.
        internal static CourseProgressData ParseProgressJson(string json)
        {
            return ParseProgressJson(json, out _);
        }

        internal static CourseProgressData ParseProgressJson(string json, out bool migrated)
        {
            return ParseProgressJson(json, AlgoNeoPackageAssetLocator.GetProjectRoot(), out migrated);
        }

        internal static CourseProgressData ParseProgressJson(string json, string projectRoot, out bool migrated)
        {
            migrated = false;
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CourseProgressData();
            }

            // Replace: словари создаются заново с обычным (регистрозависимым) сравнением, поэтому ключи,
            // различающиеся только регистром, доживают до MigrateLessonKeys и объединяются там,
            // а не затирают друг друга молча при чтении.
            JsonSerializerSettings settings = new()
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            CourseProgressData data = JsonConvert.DeserializeObject<CourseProgressData>(json, settings) ??
                                      new CourseProgressData();
            migrated = MigrateLessonKeys(data, projectRoot);
            return data;
        }

        // Переводит ключи уроков и сохранённые позиции на относительные пути. Возвращает true, если
        // что-то изменилось. Записи, которые после перевода совпали, объединяются без потери данных.
        internal static bool MigrateLessonKeys(CourseProgressData data, string projectRoot)
        {
            if (data == null)
            {
                return false;
            }

            bool changed = false;
            Dictionary<string, LessonQuizState> migrated = NewLessonStates();
            if (data.lessonStates != null)
            {
                foreach (KeyValuePair<string, LessonQuizState> pair in data.lessonStates)
                {
                    LessonQuizState state = pair.Value;
                    if (state == null)
                    {
                        continue;
                    }

                    string oldKey = pair.Key ?? string.Empty;
                    string key = ToPortablePath(oldKey, projectRoot, true, state.lessonPath);
                    if (string.IsNullOrEmpty(key))
                    {
                        key = oldKey;
                    }

                    if (!string.Equals(key, oldKey, StringComparison.Ordinal))
                    {
                        changed = true;
                    }

                    if (migrated.TryGetValue(key, out LessonQuizState existing))
                    {
                        state = MergeLessonStates(existing, state);
                        changed = true;
                    }

                    state.lessonPath = key;
                    migrated[key] = state;
                }
            }

            data.lessonStates = migrated;

            string last = ToPortablePath(data.lastLessonPath, projectRoot, true, null);
            if (!string.IsNullOrEmpty(data.lastLessonPath) &&
                !string.Equals(last, data.lastLessonPath, StringComparison.Ordinal))
            {
                data.lastLessonPath = last;
                changed = true;
            }

            string farthest = ToPortablePath(data.farthestLessonPath, projectRoot, true, null);
            if (!string.IsNullOrEmpty(data.farthestLessonPath) &&
                !string.Equals(farthest, data.farthestLessonPath, StringComparison.Ordinal))
            {
                data.farthestLessonPath = farthest;
                changed = true;
            }

            if (data.schemaVersion < CurrentSchemaVersion)
            {
                data.schemaVersion = CurrentSchemaVersion;
            }

            return changed;
        }

        // Две записи одного урока -> одна. За основу берётся запись с большим прогрессом (дальше дошли по
        // слайдам, больше завершённых вопросов и пройденных проверок); из второй добираем всё, чего в основной нет.
        internal static LessonQuizState MergeLessonStates(LessonQuizState a, LessonQuizState b)
        {
            if (a == null)
            {
                return b;
            }

            if (b == null)
            {
                return a;
            }

            LessonQuizState primary = ProgressScore(b) > ProgressScore(a) ? b : a;
            LessonQuizState secondary = ReferenceEquals(primary, a) ? b : a;

            primary.maxSlideReached = Math.Max(primary.maxSlideReached, secondary.maxSlideReached);
            primary.lastActivityAtUnixMs = Math.Max(primary.lastActivityAtUnixMs, secondary.lastActivityAtUnixMs);
            if (primary.slidesTotal <= 0)
            {
                primary.slidesTotal = secondary.slidesTotal;
            }

            if (string.IsNullOrEmpty(primary.lessonId))
            {
                primary.lessonId = secondary.lessonId ?? string.Empty;
            }

            primary.questionIdToState ??= new Dictionary<string, QuizQuestionState>();
            if (secondary.questionIdToState != null)
            {
                foreach (KeyValuePair<string, QuizQuestionState> pair in secondary.questionIdToState)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    if (!primary.questionIdToState.TryGetValue(pair.Key, out QuizQuestionState mine) ||
                        mine == null || IsBetterQuestionState(pair.Value, mine))
                    {
                        primary.questionIdToState[pair.Key] = pair.Value;
                    }
                }
            }

            // Проверка считается пройденной, если она пройдена хотя бы в одной из записей.
            primary.checkIdToPassed ??= new Dictionary<string, bool>();
            if (secondary.checkIdToPassed != null)
            {
                foreach (KeyValuePair<string, bool> pair in secondary.checkIdToPassed)
                {
                    bool mine = primary.checkIdToPassed.TryGetValue(pair.Key, out bool passed) && passed;
                    primary.checkIdToPassed[pair.Key] = mine || pair.Value;
                }
            }

            return primary;
        }

        private static long ProgressScore(LessonQuizState state)
        {
            int completed = 0;
            if (state.questionIdToState != null)
            {
                foreach (QuizQuestionState question in state.questionIdToState.Values)
                {
                    if (question != null && question.isCompleted)
                    {
                        completed++;
                    }
                }
            }

            int passedChecks = 0;
            if (state.checkIdToPassed != null)
            {
                foreach (bool passed in state.checkIdToPassed.Values)
                {
                    if (passed)
                    {
                        passedChecks++;
                    }
                }
            }

            return Math.Max(0, state.maxSlideReached) * 1000000L + completed * 1000L + passedChecks;
        }

        private static bool IsBetterQuestionState(QuizQuestionState candidate, QuizQuestionState current)
        {
            if (candidate.isCompleted != current.isCompleted)
            {
                return candidate.isCompleted;
            }

            if (candidate.isCompleted && candidate.isCorrect != current.isCorrect)
            {
                return candidate.isCorrect;
            }

            return candidate.attemptsUsed > current.attemptsUsed;
        }

        private static Dictionary<string, LessonQuizState> NewLessonStates()
        {
            return new Dictionary<string, LessonQuizState>(StringComparer.OrdinalIgnoreCase);
        }

        // Путь урока в переносимом виде. Внутри проекта — относительный ("Assets/..."); относительный
        // остаётся относительным; чужой абсолютный остаётся абсолютным.
        // guessForeign (только при миграции старых файлов): абсолютный путь из другого места/ПК
        // сводим к "Assets/..." — сначала по подсказке (сохранённый lessonPath), затем по сегменту /Assets/.
        internal static string ToPortablePath(string path, string projectRoot, bool guessForeign, string hint)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string p = path.Trim().Replace('\\', '/');
            if (!LooksAbsolute(p))
            {
                while (p.StartsWith("./", StringComparison.Ordinal))
                {
                    p = p.Substring(2);
                }

                return p;
            }

            string full = p;
            try
            {
                // Путь чужой ОС ("D:/..." на macOS) GetFullPath превратил бы в мусор — его не трогаем.
                if (Path.IsPathRooted(p))
                {
                    full = Path.GetFullPath(p).Replace('\\', '/');
                }
            }
            catch
            {
                full = p;
            }

            string root = (projectRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (root.Length > 0 && full.Length > root.Length + 1 &&
                full.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return full.Substring(root.Length + 1);
            }

            if (guessForeign)
            {
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    string relativeHint = hint.Trim().Replace('\\', '/');
                    if (!LooksAbsolute(relativeHint) &&
                        full.EndsWith("/" + relativeHint, StringComparison.OrdinalIgnoreCase))
                    {
                        return relativeHint;
                    }
                }

                int at = Math.Max(full.LastIndexOf("/Assets/", StringComparison.OrdinalIgnoreCase),
                    full.LastIndexOf("/Packages/", StringComparison.OrdinalIgnoreCase));
                if (at >= 0)
                {
                    return full.Substring(at + 1);
                }
            }

            return full;
        }

        // Абсолютный в любом из привычных видов: "/home/..", "//server/..", "C:/..".
        private static bool LooksAbsolute(string forwardSlashPath)
        {
            if (string.IsNullOrEmpty(forwardSlashPath))
            {
                return false;
            }

            return forwardSlashPath[0] == '/' ||
                   (forwardSlashPath.Length >= 2 && char.IsLetter(forwardSlashPath[0]) && forwardSlashPath[1] == ':');
        }

        // Per-project изоляция: полный путь должен лежать внутри корня проекта.
        // Относительные пути (Assets/...) считаются от корня проекта.
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
                string full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(root, path));
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

        // Копия файла в прежнем формате — один раз, перед первой записью после миграции ключей.
        // Имя заканчивается на '~': Unity такие файлы не импортирует и .meta для них не создаёт.
        private static void WritePreMigrationBackup(string fullPath)
        {
            if (s_PreMigrationJson == null)
            {
                return;
            }

            string original = s_PreMigrationJson;
            s_PreMigrationJson = null;
            try
            {
                string backupPath = fullPath + ".before-relative-paths.bak~";
                if (!File.Exists(backupPath))
                {
                    File.WriteAllText(backupPath, original);
                    Debug.Log("[AlgoNeoCourse] Прогресс переведён на пути уроков относительно проекта " +
                              "(переживает перенос проекта). Копия прежнего файла: " + backupPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseProgressStore: не удалось сохранить копию прогресса перед миграцией — " +
                                 ex.Message);
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

                CourseProgressData data = ParseProgressJson(json, out bool migrated);
                if (migrated)
                {
                    s_PreMigrationJson = json;
                }

                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseProgressStore: load failed - " + ex.Message);
                return new CourseProgressData();
            }
        }
    }
}
