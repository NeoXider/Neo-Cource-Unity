using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    public partial class CourseSettings
    {
        public bool IsDownloadingLessons => currentDownloadCts != null && !currentDownloadCts.IsCancellationRequested;

        public void DownloadSelectedLessons()
        {
            EnsureRuntimeState();
            CancelDownloads();
            currentDownloadCts = new CancellationTokenSource();
            _ = DownloadSelectedLessonsAsync(currentDownloadCts.Token);
        }

        private async Task DownloadSelectedLessonsAsync(CancellationToken cancellationToken)
        {
            if (lessonSelections == null || lessonSelections.Count == 0)
            {
                Debug.LogWarning("CourseSettings: список уроков пуст. Сначала выполните 'Загрузить список уроков'.");
                return;
            }

            string targetFolder = GetDownloadFolderPath();
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            try
            {
                int total = lessonSelections.Count(selection => selection.selected);
                int completed = 0;
                foreach (LessonSelection selection in lessonSelections.Where(selection => selection.selected))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    string remoteRel = selection.file?.TrimStart('/');
                    if (string.IsNullOrEmpty(remoteRel))
                    {
                        Debug.LogWarning($"CourseSettings: у урока '{selection.title}' не задан путь к .md");
                        continue;
                    }

                    string url = repositoryBaseUrl.TrimEnd('/') + "/" + remoteRel;
                    string localPath = Path.Combine(targetFolder,
                        SanitizeFileName(selection.id + "-" + Path.GetFileName(remoteRel)));

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Загрузка уроков",
                            $"{selection.title} ({completed + 1}/{Math.Max(total, 1)})",
                            completed / (float)Math.Max(total, 1)))
                    {
                        CancelDownloads();
                        break;
                    }

                    const int maxAttempts = 3;
                    int attempt = 0;
                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        attempt++;
                        try
                        {
                            HttpResponseMessage response = await GetHttpClient().GetAsync(url, cancellationToken);
                            if (!response.IsSuccessStatusCode)
                            {
                                Debug.LogError(
                                    $"CourseSettings: не удалось загрузить {selection.title} — {response.StatusCode} {url}");
                            }
                            else
                            {
                                string markdown = await response.Content.ReadAsStringAsync();
                                File.WriteAllText(localPath, markdown);
                                Debug.Log($"Сохранено: {localPath}");
                            }

                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (attempt >= maxAttempts)
                            {
                                Debug.LogError("CourseSettings: ошибка загрузки урока — " + ex.Message);
                                break;
                            }

                            await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt - 1)),
                                cancellationToken);
                        }
                    }

                    completed++;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.Log("CourseSettings: загрузка уроков отменена пользователем.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("CourseSettings: ошибка загрузки уроков — " + ex.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                CancelDownloads();
            }
        }

        public void CancelDownloads()
        {
            try
            {
                currentDownloadCts?.Cancel();
            }
            catch
            {
            }
            finally
            {
                currentDownloadCts = null;
            }
        }

        public void CancelDownloadsButton()
        {
            CancelDownloads();
        }

        public void DeleteDownloadedFiles()
        {
            string targetFolder = GetDownloadFolderPath();
            if (!Directory.Exists(targetFolder))
            {
                Debug.Log("CourseSettings: загруженных файлов не найдено.");
                return;
            }

            try
            {
                FileUtil.DeleteFileOrDirectory(targetFolder);
                FileUtil.DeleteFileOrDirectory(targetFolder + ".meta");
                Debug.Log("CourseSettings: папка с загруженными уроками очищена.");
            }
            catch (Exception ex)
            {
                Debug.LogError("CourseSettings: не удалось удалить папку — " + ex.Message);
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return string.Concat(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
        }

        private static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "lesson";
            }

            string lower = input.Trim().ToLowerInvariant();
            lower = Regex.Replace(lower, @"[\t\s_]+", "-");
            lower = Regex.Replace(lower, @"[^a-z0-9-]", "");
            lower = Regex.Replace(lower, "-+", "-");
            lower = lower.Trim('-');
            return string.IsNullOrEmpty(lower) ? "lesson" : lower;
        }
    }
}