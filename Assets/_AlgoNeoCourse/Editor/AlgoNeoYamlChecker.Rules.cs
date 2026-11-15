using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NeoCource.Editor.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeoCource.Editor
{
    internal enum CheckRuleKind
    {
        ObjectExists,
        ComponentExists,
        FileExists,
        Contains,
        SceneName,

        // Значение поля или свойства компонента (Camera.orthographicSize, длина массива префабов).
        Field,

        // Строка, которую движок не понимает (опечатка в ключе, придуманное правило) — всегда провал.
        Unknown,

        // Блок без единого правила — тоже провал, а не «зелёная кнопка».
        EmptyBlock
    }

    // Одно правило блока check в виде, удобном для показа ученику.
    internal sealed class CheckRule
    {
        public CheckRuleKind kind;
        public string target; // объект / файл / сцена / непонятая строка
        public string value; // тип компонента / искомая строка

        // Дополнительное правило (optional: true): его провал — замечание, а не ошибка задания.
        public bool optional;

        // component_exists / field: префаб из проекта вместо объекта сцены ("Bolt", "EnemyShip/ShootPoint").
        public string prefab;

        // field: путь к полю или свойству ("orthographicSize", "main.duration"), ожидаемое значение
        // (equals) или длина списка (count), допуск для чисел и векторов.
        public string member;
        public string expected;
        public bool expectCount;
        public float? tolerance;

        // Ошибка в самом правиле (нет type, пустое значение): такое правило не проверяется, а сразу проваливается.
        public string problem;

        // Понятная формулировка: что именно проверит Unity.
        public string Describe()
        {
            switch (kind)
            {
                case CheckRuleKind.ObjectExists:
                    return $"На сцене есть объект <b>{Escape(target)}</b>";
                case CheckRuleKind.ComponentExists:
                    if (!string.IsNullOrEmpty(prefab))
                    {
                        return $"У префаба <b>{Escape(prefab)}</b> есть компонент <b>{Escape(Shown(value))}</b>";
                    }

                    return string.IsNullOrEmpty(target)
                        ? $"На сцене есть объект с компонентом <b>{Escape(Shown(value))}</b>"
                        : $"У объекта <b>{Escape(target)}</b> есть компонент <b>{Escape(Shown(value))}</b>";
                case CheckRuleKind.Field:
                {
                    string owner = !string.IsNullOrEmpty(prefab)
                        ? $"У префаба <b>{Escape(prefab)}</b>"
                        : $"У объекта <b>{Escape(Shown(target))}</b>";
                    string where = $"{Escape(Shown(value))} → <color=#FFD58A>{Escape(Shown(member))}</color>";
                    return expectCount
                        ? $"{owner}: {where}, элементов <b>{Escape(Shown(expected))}</b>"
                        : $"{owner}: {where} = <b>{Escape(ShownValue(expected))}</b>";
                }
                case CheckRuleKind.FileExists:
                    return $"В проекте есть файл <b>{Escape(Shown(target))}</b>";
                case CheckRuleKind.Contains:
                    return string.IsNullOrEmpty(target)
                        ? $"В скрипте есть: <color=#FFD58A>{Escape(Shown(value))}</color>"
                        : $"В файле <b>{Escape(target)}</b> есть: <color=#FFD58A>{Escape(Shown(value))}</color>";
                case CheckRuleKind.SceneName:
                    return $"Открыта сцена <b>{Escape(Shown(target))}</b>";
                case CheckRuleKind.Unknown:
                    return $"Неизвестное правило <b>{Escape(Shown(target))}</b>";
                default:
                    return "В блоке check нет правил";
            }
        }

        // Короткая запись для текстового отчёта (консоль / диалог).
        public string ToLogLine()
        {
            string line = ToLogLineCore();
            return optional ? line + " [дополнительно]" : line;
        }

        private string ToLogLineCore()
        {
            switch (kind)
            {
                case CheckRuleKind.ObjectExists:
                    return $"object_exists: {target}";
                case CheckRuleKind.ComponentExists:
                    if (!string.IsNullOrEmpty(prefab))
                    {
                        return $"component_exists: префаб {prefab}.{Shown(value)}";
                    }

                    return string.IsNullOrEmpty(target)
                        ? $"component_exists: {Shown(value)} (на любом объекте сцены)"
                        : $"component_exists: {target}.{Shown(value)}";
                case CheckRuleKind.Field:
                {
                    string owner = !string.IsNullOrEmpty(prefab) ? "префаб " + prefab : Shown(target);
                    return expectCount
                        ? $"field: {owner}.{Shown(value)}.{Shown(member)} count = {Shown(expected)}"
                        : $"field: {owner}.{Shown(value)}.{Shown(member)} = {ShownValue(expected)}";
                }
                case CheckRuleKind.FileExists:
                    return $"filename: {Shown(target)}";
                case CheckRuleKind.Contains:
                    return string.IsNullOrEmpty(target)
                        ? $"contains: \"{value}\""
                        : $"contains: \"{value}\" [{target}]";
                case CheckRuleKind.SceneName:
                    return $"scene_name: {Shown(target)}";
                case CheckRuleKind.Unknown:
                    return $"неизвестное правило: {Shown(target)}";
                default:
                    return "в блоке check нет правил";
            }
        }

        private static string Shown(string text)
        {
            return string.IsNullOrEmpty(text) ? "?" : text;
        }

        // Пустое ожидаемое значение у field — это «пусто», а не «не указано».
        private static string ShownValue(string text)
        {
            return string.IsNullOrEmpty(text) ? "пусто" : text;
        }

        // Rich text включён: '<' из кода (List<int>) не должен стать тегом — разрываем его пробелом нулевой ширины.
        private static string Escape(string text)
        {
            return (text ?? string.Empty).Replace("<", "<​");
        }
    }

    internal sealed class CheckRuleResult
    {
        public CheckRule rule;
        public bool ok;
        public string hint; // почему не прошло (пусто при успехе)
        public string info; // уточнение при успехе (например, найденный путь файла)
    }

    // Единый движок блока check: ParseRules разбирает, EvaluateRules проверяет, Evaluate() строит
    // вердикт и текст ИЗ ТЕХ ЖЕ результатов — карточка задания и итог не могут разойтись.
    internal static partial class AlgoNeoYamlChecker
    {
        public const string KnownRulesList = "object_exists, component_exists, field, scene_name, filename, contains";

        // Ключи-параметры правила. Строка "ключ: значение" без дефиса сразу под правилом — его параметр,
        // если ключ подходит этому правилу; иначе строка разбирается как отдельное правило (как до 1.9.0).
        private const string OptionalKey = "optional";

        public static List<CheckRule> ParseRules(string raw)
        {
            List<CheckRule> rules = new();
            string[] lines = (raw ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            // contains относится к ближайшему filename выше него.
            string currentFile = null;
            string firstFile = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                bool dashed = t.StartsWith("-", StringComparison.Ordinal);
                string body = dashed ? t.Substring(1).TrimStart() : t;
                if (!TrySplitKey(body, out string key, out string rest))
                {
                    rules.Add(new CheckRule { kind = CheckRuleKind.Unknown, target = body });
                    continue;
                }

                CheckRule rule = null;
                HashSet<string> seen = new();
                switch (NormalizeKey(key))
                {
                    case "rules":
                    case "type" when !dashed:
                        // Заголовок списка и необязательная пометка "type: scene|script|combined".
                        break;

                    case "object_exists":
                    {
                        string name = ParseScalar(rest);
                        rule = new CheckRule
                        {
                            kind = CheckRuleKind.ObjectExists,
                            target = name,
                            problem = string.IsNullOrEmpty(name) ? "не указано имя объекта" : null
                        };
                        break;
                    }

                    case "scene_name":
                    {
                        string scene = ParseScalar(rest);
                        rule = new CheckRule
                        {
                            kind = CheckRuleKind.SceneName,
                            target = scene,
                            problem = string.IsNullOrEmpty(scene) ? "не указано имя сцены" : null
                        };
                        break;
                    }

                    case "filename":
                    {
                        string file = ParseScalar(rest);
                        currentFile = string.IsNullOrEmpty(file) ? null : file;
                        firstFile ??= currentFile;
                        rule = new CheckRule
                        {
                            kind = CheckRuleKind.FileExists,
                            target = file,
                            problem = string.IsNullOrEmpty(file) ? "не указано имя файла" : null
                        };
                        break;
                    }

                    case "contains":
                    {
                        string term = ParseScalar(rest);
                        rule = new CheckRule
                        {
                            kind = CheckRuleKind.Contains,
                            target = currentFile,
                            value = term,
                            problem = string.IsNullOrEmpty(term) ? "пустая строка поиска" : null
                        };
                        break;
                    }

                    case "component_exists":
                        rule = new CheckRule { kind = CheckRuleKind.ComponentExists };
                        ReadInlineParameters(rule, rest, seen);
                        break;

                    case "field":
                        rule = new CheckRule { kind = CheckRuleKind.Field };
                        ReadInlineParameters(rule, rest, seen);
                        break;

                    default:
                        rules.Add(new CheckRule { kind = CheckRuleKind.Unknown, target = key });
                        break;
                }

                if (rule == null)
                {
                    continue;
                }

                ReadParameterLines(rule, lines, ref i, seen);
                ValidateParameters(rule);
                rules.Add(rule);
            }

            // contains выше первого filename (старые уроки) — берём первый файл блока.
            foreach (CheckRule rule in rules)
            {
                if (rule.kind != CheckRuleKind.Contains || rule.target != null)
                {
                    continue;
                }

                rule.target = firstFile;
                if (firstFile == null && rule.problem == null)
                {
                    rule.problem = "перед contains нужен filename — непонятно, в каком файле искать";
                }
            }

            if (rules.Count == 0)
            {
                rules.Add(new CheckRule { kind = CheckRuleKind.EmptyBlock });
            }

            return rules;
        }

        // Значение после ключа правила: короткая запись типа ("- component_exists: "Button"")
        // или параметры в одну строку ({ object: "Player", type: "Rigidbody", optional: true }).
        private static void ReadInlineParameters(CheckRule rule, string rest, HashSet<string> seen)
        {
            string inline = (rest ?? string.Empty).Trim();
            if (inline.StartsWith("{", StringComparison.Ordinal))
            {
                foreach (string part in SplitOutsideQuotes(StripFlowBraces(inline), ','))
                {
                    if (!TrySplitKey(part.Trim(), out string partKey, out string partRest))
                    {
                        continue;
                    }

                    if (rule.kind == CheckRuleKind.ComponentExists)
                    {
                        // Legacy flow mapping: unknown keys are ignored, the last value wins.
                        // Keep subsequent parameter lines behaving as before this compatibility fix.
                        string value = ParseScalar(partRest);
                        switch (NormalizeKey(partKey))
                        {
                            case "object": rule.target = string.IsNullOrEmpty(value) ? null : value; break;
                            case "prefab": rule.prefab = string.IsNullOrEmpty(value) ? null : value; break;
                            case "type": rule.value = value; break;
                            case OptionalKey:
                                if (TryParseBool(value, out bool optional))
                                {
                                    rule.optional = optional;
                                    rule.problem = null;
                                }
                                else
                                {
                                    rule.problem ??= $"optional: «{value}» — пишите true или false";
                                }
                                break;
                        }

                        continue;
                    }

                    if (!TryApplyParameter(rule, NormalizeKey(partKey), partRest, seen))
                    {
                        rule.problem ??= $"непонятный параметр «{partKey}»";
                    }
                }

                return;
            }

            string shortValue = ParseScalar(inline);
            if (string.IsNullOrEmpty(shortValue))
            {
                return;
            }

            if (rule.kind == CheckRuleKind.ComponentExists)
            {
                rule.value = shortValue;
            }
            else
            {
                rule.problem ??= "параметры field пишутся на следующих строках: object, type, name, equals";
            }
        }

        // Строки "ключ: значение" без дефиса сразу под правилом. Как только ключ правилу не подходит
        // (или повторяется), разбор параметров кончается — строка станет следующим правилом, как раньше.
        private static void ReadParameterLines(CheckRule rule, string[] lines, ref int index, HashSet<string> seen)
        {
            for (int j = index + 1; j < lines.Length; j++)
            {
                string tj = lines[j].Trim();
                if (tj.Length == 0 || tj.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (tj.StartsWith("-", StringComparison.Ordinal) ||
                    !TrySplitKey(tj, out string subKey, out string subRest) ||
                    !TryApplyParameter(rule, NormalizeKey(subKey), subRest, seen))
                {
                    break;
                }

                index = j;
            }
        }

        private static bool TryApplyParameter(CheckRule rule, string key, string rest, HashSet<string> seen)
        {
            if (seen.Contains(key))
            {
                return false;
            }

            bool targeted = rule.kind == CheckRuleKind.ComponentExists || rule.kind == CheckRuleKind.Field;
            bool field = rule.kind == CheckRuleKind.Field;
            string value = ParseScalar(rest);
            switch (key)
            {
                case OptionalKey:
                    if (TryParseBool(value, out bool optional))
                    {
                        rule.optional = optional;
                    }
                    else
                    {
                        rule.problem ??= $"optional: «{value}» — пишите true или false";
                    }

                    break;
                case "object" when targeted:
                    rule.target = string.IsNullOrEmpty(value) ? null : value;
                    break;
                case "prefab" when targeted:
                    rule.prefab = string.IsNullOrEmpty(value) ? null : value;
                    break;
                case "type" when targeted:
                    // Короткая запись "- component_exists: "Button"" уже задала тип — второй не принимаем.
                    if (!string.IsNullOrEmpty(rule.value))
                    {
                        return false;
                    }

                    rule.value = value;
                    break;
                case "name" when field:
                    rule.member = value;
                    break;
                case "equals" when field:
                    rule.expected = value;
                    break;
                case "count" when field:
                    rule.expected = value;
                    rule.expectCount = true;
                    break;
                case "tolerance" when field:
                    if (TryParseNumber(value, out double tolerance) && tolerance >= 0)
                    {
                        rule.tolerance = (float)tolerance;
                    }
                    else
                    {
                        rule.problem ??= $"tolerance: «{value}» — нужно неотрицательное число";
                    }

                    break;
                default:
                    return false;
            }

            seen.Add(key);
            if ((key == "equals" && seen.Contains("count")) || (key == "count" && seen.Contains("equals")))
            {
                rule.problem ??= "укажите что-то одно: equals или count";
            }

            return true;
        }

        // Без object правило component_exists значит «компонент есть на каком-нибудь объекте сцены».
        private static void ValidateParameters(CheckRule rule)
        {
            if (rule.kind != CheckRuleKind.ComponentExists && rule.kind != CheckRuleKind.Field)
            {
                return;
            }

            if (!string.IsNullOrEmpty(rule.target) && !string.IsNullOrEmpty(rule.prefab))
            {
                rule.problem ??= "укажите что-то одно: object (объект сцены) или prefab";
            }

            if (string.IsNullOrEmpty(rule.value))
            {
                rule.problem ??= "не указан type — какой компонент искать";
            }

            if (rule.kind != CheckRuleKind.Field)
            {
                return;
            }

            if (string.IsNullOrEmpty(rule.target) && string.IsNullOrEmpty(rule.prefab))
            {
                rule.problem ??= "не указано, где смотреть: object (объект сцены) или prefab";
            }

            if (string.IsNullOrEmpty(rule.member))
            {
                rule.problem ??= "не указан name — какое поле сравнивать";
            }

            if (rule.expected == null)
            {
                rule.problem ??= "не указано, с чем сравнивать: equals (значение) или count (длина списка)";
            }
            else if (rule.expectCount && !int.TryParse(rule.expected, NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out _))
            {
                rule.problem ??= $"count: «{rule.expected}» — нужно целое число";
            }
        }

        internal static bool TryParseBool(string text, out bool value)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "да":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "нет":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        internal static bool TryParseNumber(string text, out double value)
        {
            return double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static List<CheckRuleResult> EvaluateRules(string raw)
        {
            List<CheckRuleResult> results = new();
            // Файл читаем один раз, даже если на него несколько contains. null в path — «не найден».
            Dictionary<string, (string path, string text, string error)> files =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (CheckRule rule in ParseRules(raw))
            {
                CheckRuleResult result = new() { rule = rule };
                results.Add(result);

                if (!string.IsNullOrEmpty(rule.problem))
                {
                    result.hint = rule.problem;
                    continue;
                }

                try
                {
                    switch (rule.kind)
                    {
                        case CheckRuleKind.ObjectExists:
                            result.ok = AlgoNeoEditorUtils.FindSceneObject(rule.target) != null;
                            result.hint = result.ok ? null : "объекта с таким именем на сцене нет";
                            break;

                        case CheckRuleKind.ComponentExists:
                            EvaluateComponentRule(rule, result);
                            break;

                        case CheckRuleKind.Field:
                            EvaluateFieldRule(rule, result);
                            break;

                        case CheckRuleKind.SceneName:
                            EvaluateSceneRule(rule, result);
                            break;

                        case CheckRuleKind.FileExists:
                        case CheckRuleKind.Contains:
                            if (!files.TryGetValue(rule.target, out (string path, string text, string error) file))
                            {
                                file = LoadCheckedFile(rule.target);
                                files[rule.target] = file;
                            }

                            if (string.IsNullOrEmpty(file.path))
                            {
                                result.hint = "файл не найден в проекте";
                            }
                            else if (file.error != null)
                            {
                                result.hint = "не удалось прочитать файл: " + file.error;
                            }
                            else if (rule.kind == CheckRuleKind.FileExists)
                            {
                                result.ok = true;
                                result.info = file.path;
                            }
                            else
                            {
                                // Ordinal: C# чувствителен к регистру — "void update()" не равно "void Update()".
                                result.ok = file.text.IndexOf(rule.value, StringComparison.Ordinal) >= 0;
                                result.hint = result.ok
                                    ? null
                                    : "такой строки в файле нет — проверьте написание и регистр букв";
                            }

                            break;

                        case CheckRuleKind.Unknown:
                            result.hint = "плагин не знает такого правила — проверьте написание. Доступны: " +
                                          KnownRulesList;
                            break;

                        default:
                            result.hint = "проверять нечего — добавьте правила: " + KnownRulesList;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.ok = false;
                    result.hint = "не удалось проверить: " + (ex.InnerException ?? ex).Message;
                }
            }

            return results;
        }

        private static void EvaluateComponentRule(CheckRule rule, CheckRuleResult result)
        {
            GameObject go = null;
            if (!string.IsNullOrEmpty(rule.prefab))
            {
                go = AlgoNeoEditorUtils.FindPrefabObject(rule.prefab, out string prefabProblem);
                if (go == null)
                {
                    result.hint = prefabProblem;
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(rule.target))
            {
                go = AlgoNeoEditorUtils.FindSceneObject(rule.target);
                if (go == null)
                {
                    result.hint = "объекта с таким именем на сцене нет";
                    return;
                }
            }

            Type type = AlgoNeoEditorUtils.FindTypeByName(rule.value);
            if (type == null)
            {
                result.hint = "такого компонента или скрипта нет в проекте (или в скрипте ошибка компиляции)";
                return;
            }

            if (!AlgoNeoEditorUtils.IsComponentType(type))
            {
                result.hint = $"«{type.FullName}» — не компонент, его нельзя добавить на объект";
                return;
            }

            if (go != null)
            {
                result.ok = go.GetComponent(type) != null;
                result.hint = result.ok
                    ? null
                    : string.IsNullOrEmpty(rule.prefab)
                        ? "компонент не добавлен на объект"
                        : "компонент не добавлен на префаб";
            }
            else
            {
                result.ok = AlgoNeoEditorUtils.FindSceneComponent(type) != null;
                result.hint = result.ok ? null : "ни на одном объекте сцены такого компонента нет";
            }
        }

        // Сцена считается открытой, если она загружена в редакторе (имя без учёта регистра, как у файлов).
        private static void EvaluateSceneRule(CheckRule rule, CheckRuleResult result)
        {
            string wanted = rule.target.Replace('\\', '/');
            if (wanted.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                wanted = wanted.Substring(0, wanted.Length - ".unity".Length);
            }

            List<string> open = new();
            foreach ((string name, string path) scene in LoadedScenesProvider())
            {
                open.Add(string.IsNullOrEmpty(scene.name) ? "без имени (не сохранена)" : scene.name);
                string path = (scene.path ?? string.Empty).Replace('\\', '/');
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - ".unity".Length);
                }

                bool match = wanted.Contains("/")
                    ? path.EndsWith(wanted, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(scene.name, wanted, StringComparison.OrdinalIgnoreCase);
                if (match)
                {
                    result.ok = true;
                    return;
                }
            }

            result.hint = open.Count > 0
                ? "сейчас открыта сцена: " + string.Join(", ", open)
                : "ни одна сцена не открыта";
        }

        // Источник списка открытых сцен (имя, путь). Подменяется в тестах, чтобы не трогать сцены редактора.
        internal static Func<List<(string name, string path)>> LoadedScenesProvider = GetLoadedScenes;

        private static List<(string name, string path)> GetLoadedScenes()
        {
            List<(string name, string path)> scenes = new();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    scenes.Add((scene.name, scene.path));
                }
            }

            return scenes;
        }

        private static (string path, string text, string error) LoadCheckedFile(string filename)
        {
            string path = AlgoNeoEditorUtils.FindAssetPath(filename);
            if (string.IsNullOrEmpty(path))
            {
                return (null, null, null);
            }

            try
            {
                return (path, AlgoNeoEditorUtils.ReadAllTextWithRetries(path) ?? string.Empty, null);
            }
            catch (Exception ex)
            {
                return (path, string.Empty, ex.Message);
            }
        }

        // "ключ: значение" — ключом считается только слово без пробелов (буквы, цифры, '_', '-').
        private static bool TrySplitKey(string body, out string key, out string rest)
        {
            key = null;
            rest = null;
            int colon = (body ?? string.Empty).IndexOf(':');
            if (colon <= 0)
            {
                return false;
            }

            string candidate = body.Substring(0, colon).Trim();
            if (candidate.Length == 0)
            {
                return false;
            }

            foreach (char c in candidate)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                {
                    return false;
                }
            }

            key = candidate;
            rest = body.Substring(colon + 1);
            return true;
        }

        private static string NormalizeKey(string key)
        {
            return (key ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        }

        // Значение правила: "двойные" или 'одинарные' кавычки либо текст без кавычек.
        // Всё после закрывающей кавычки (например, "text"  # комментарий) отбрасывается.
        internal static string ParseScalar(string rest)
        {
            string s = (rest ?? string.Empty).Trim();
            // Пусто или сразу комментарий ("key:  # пояснение") — значения нет.
            if (s.Length == 0 || s[0] == '#')
            {
                return string.Empty;
            }

            if (s[0] == '"')
            {
                StringBuilder sb = new();
                for (int i = 1; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '\\' && i + 1 < s.Length && (s[i + 1] == '"' || s[i + 1] == '\\'))
                    {
                        sb.Append(s[i + 1]);
                        i++;
                    }
                    else if (c == '"' && EndsValue(s, i + 1))
                    {
                        return sb.ToString();
                    }
                    else
                    {
                        // В том числе неэкранированная кавычка в середине: "text = "Очки: " + score".
                        sb.Append(c);
                    }
                }

                // Кавычку забыли закрыть — берём всё до конца строки.
                return sb.ToString().TrimEnd();
            }

            if (s[0] == '\'')
            {
                StringBuilder sb = new();
                for (int i = 1; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '\'' && i + 1 < s.Length && s[i + 1] == '\'')
                    {
                        sb.Append('\'');
                        i++;
                    }
                    else if (c == '\'' && EndsValue(s, i + 1))
                    {
                        return sb.ToString();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString().TrimEnd();
            }

            // Без кавычек: комментарий начинается с '#', перед которым стоит пробел.
            for (int i = 1; i < s.Length; i++)
            {
                if (s[i] == '#' && char.IsWhiteSpace(s[i - 1]))
                {
                    return s.Substring(0, i).TrimEnd();
                }
            }

            return s;
        }

        // Кавычка закрывает значение, только если после неё пусто или комментарий.
        private static bool EndsValue(string s, int from)
        {
            for (int i = from; i < s.Length; i++)
            {
                if (!char.IsWhiteSpace(s[i]))
                {
                    return s[i] == '#';
                }
            }

            return true;
        }

        private static string StripFlowBraces(string text)
        {
            string inner = text.Substring(1);
            bool inDouble = false, inSingle = false;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '\\' && inDouble)
                {
                    i++;
                }
                else if (c == '"' && !inSingle)
                {
                    inDouble = !inDouble;
                }
                else if (c == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                }
                else if (c == '}' && !inDouble && !inSingle)
                {
                    return inner.Substring(0, i);
                }
            }

            return inner;
        }

        private static List<string> SplitOutsideQuotes(string text, char separator)
        {
            List<string> parts = new();
            StringBuilder current = new();
            bool inDouble = false, inSingle = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && inDouble && i + 1 < text.Length)
                {
                    current.Append(c).Append(text[i + 1]);
                    i++;
                    continue;
                }

                if (c == '"' && !inSingle)
                {
                    inDouble = !inDouble;
                }
                else if (c == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                }
                else if (c == separator && !inDouble && !inSingle)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            parts.Add(current.ToString());
            return parts;
        }
    }
}
