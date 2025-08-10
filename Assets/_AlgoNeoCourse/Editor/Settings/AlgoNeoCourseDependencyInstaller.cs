using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace NeoCource.Editor
{
    [InitializeOnLoad]
    internal static class AlgoNeoCourseDependencyInstaller
    {
        // Package id and source
        private const string RequiredPackageName = "com.rtl.markdownrenderer";
        private const string RequiredPackageGitUrl = "https://github.com/UnityGuillaume/MarkdownRenderer.git";

        // EditorPrefs keys
        private const string SuppressPromptKey = "AlgoNeoCourse.SuppressDependencyPrompt";
        private const string PromptVersionKey = "AlgoNeoCourse.DependencyPrompt.Version";

        // bump when logic or requirement changes to reprompt existing users
        private const string CurrentPromptVersion = "1";

        private static ListRequest listRequest;
        private static AddRequest addRequest;

        static AlgoNeoCourseDependencyInstaller()
        {
            // Delay until editor is ready
            EditorApplication.delayCall += TryCheckAndPrompt;
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Check & Install", priority = 10)]
        private static void MenuCheckAndInstall()
        {
            ForceCheckAndPrompt(showEvenIfSuppressed: true);
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Reinstall MarkdownRenderer", priority = 11)]
        private static void MenuReinstall()
        {
            InstallRequiredPackage(force: true);
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Reset Prompt", priority = 100)]
        private static void MenuResetPrompt()
        {
            EditorPrefs.DeleteKey(SuppressPromptKey);
            EditorPrefs.DeleteKey(PromptVersionKey);
            Debug.Log("AlgoNeoCourse: диалог зависимости будет показан при следующей проверке.");
        }

        private static void TryCheckAndPrompt()
        {
            bool suppressed = EditorPrefs.GetBool(SuppressPromptKey, false);
            string storedVersion = EditorPrefs.GetString(PromptVersionKey, string.Empty);

            if (!suppressed || storedVersion != CurrentPromptVersion)
            {
                ForceCheckAndPrompt(showEvenIfSuppressed: false);
            }

            // Всегда пробуем применить фикс, если пакет уже есть/появился ранее
            EnsureMarkdownRendererPatched();
        }

        private static void ForceCheckAndPrompt(bool showEvenIfSuppressed)
        {
            listRequest = Client.List(true);
            EditorApplication.update += OnListProgress;

            void OnListProgress()
            {
                if (!listRequest.IsCompleted)
                    return;

                EditorApplication.update -= OnListProgress;

                if (listRequest.Status == StatusCode.Failure)
                {
                    Debug.LogWarning("AlgoNeoCourse: не удалось получить список пакетов: " + listRequest.Error?.message);
                    return;
                }

                bool isInstalled = listRequest.Result.Any(p => string.Equals(p.name, RequiredPackageName, StringComparison.OrdinalIgnoreCase));
                if (isInstalled)
                {
                    EditorPrefs.SetString(PromptVersionKey, CurrentPromptVersion);
                    EnsureMarkdownRendererPatched();
                    return;
                }

                bool suppressed = EditorPrefs.GetBool(SuppressPromptKey, false);
                if (suppressed && !showEvenIfSuppressed)
                {
                    return;
                }

                var accept = EditorUtility.DisplayDialog(
                    title: "AlgoNeoCourse — требуется зависимость",
                    message: "Для отображения уроков нужен пакет MarkdownRenderer (UI Toolkit). Установить сейчас?\n\nИсточник: " + RequiredPackageGitUrl,
                    ok: "Установить",
                    cancel: "Позже");

                EditorPrefs.SetString(PromptVersionKey, CurrentPromptVersion);
                if (!accept)
                {
                    EditorPrefs.SetBool(SuppressPromptKey, true);
                    return;
                }

                InstallRequiredPackage(force: false);
            }
        }

        private static void InstallRequiredPackage(bool force)
        {
            // Optional: if force, try to remove first. Simpler path: just Add; UPM will update to git source.
            addRequest = Client.Add(RequiredPackageGitUrl);
            EditorApplication.update += OnAddProgress;

            void OnAddProgress()
            {
                if (!addRequest.IsCompleted)
                    return;

                EditorApplication.update -= OnAddProgress;

                if (addRequest.Status == StatusCode.Success)
                {
                    var info = addRequest.Result;
                    string id = !string.IsNullOrEmpty(info?.name) && !string.IsNullOrEmpty(info?.version)
                        ? $"{info.name}@{info.version}"
                        : (info?.packageId ?? "<unknown>");
                    Debug.Log($"AlgoNeoCourse: установлен пакет {id}");

                    // После установки — применяем фикс несовместимости UI Toolkit
                    EnsureMarkdownRendererPatched();
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogError("AlgoNeoCourse: не удалось установить пакет — " + addRequest.Error?.message);
                }
            }
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Apply MarkdownRenderer Fix", priority = 12)]
        private static void MenuApplyFix()
        {
            EnsureMarkdownRendererPatched();
            AssetDatabase.Refresh();
        }

        private static void EnsureMarkdownRendererPatched()
        {
            try
            {
                string packageCachePath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");
                if (!Directory.Exists(packageCachePath)) return;

                var candidates = Directory.GetDirectories(packageCachePath)
                    .Where(d => Path.GetFileName(d).StartsWith("com.rtl.markdownrenderer@", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var dir in candidates)
                {
                    string target = Path.Combine(dir, "Editor", "VideoElement", "VideoPlayerElement.cs");
                    if (!File.Exists(target)) continue;

                    string text = File.ReadAllText(target);
                    bool changed = false;

                    // 1) Удаляем атрибут [UxmlElement]/[UxmlElementAttribute] если присутствует
                    var removed = Regex.Replace(text, @"(?m)^\s*\[UxmlElement(?:Attribute)?\]\s*\r?\n", string.Empty);
                    if (!ReferenceEquals(removed, text))
                    {
                        text = removed;
                        changed = true;
                    }

                    // 2) Добавляем вложенную фабрику, если отсутствует
                    if (!text.Contains("class UxmlFactory : UxmlFactory<VideoPlayerElement>"))
                    {
                        // Ищем начало тела класса VideoPlayerElement
                        var classMatch = Regex.Match(text,
                            @"public\s+partial\s+class\s+VideoPlayerElement\s*:\s*VisualElement\s*\{",
                            RegexOptions.Multiline);
                        if (classMatch.Success)
                        {
                            int insertPos = classMatch.Index + classMatch.Length;
                            string insertText = "\n    public new class UxmlFactory : UxmlFactory<VideoPlayerElement> { }\n";
                            text = text.Insert(insertPos, insertText);
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        File.WriteAllText(target, text);
                        Debug.Log($"AlgoNeoCourse: применён фикс MarkdownRenderer в '{target}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AlgoNeoCourse: не удалось применить фикс MarkdownRenderer: {ex.Message}");
            }
        }
    }
}


