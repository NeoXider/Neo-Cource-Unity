using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using NeoCource.Editor.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeoCource.Editor
{
    // Правило field: значение поля или свойства компонента на объекте сцены или на префабе.
    //   - field:
    //       object: "Main Camera"        # или prefab: "Bolt"
    //       type: "Camera"
    //       name: "orthographicSize"     # публичное поле/свойство, можно путь: "main.duration"
    //       equals: 5                    # или count: 5 — длина массива/списка
    //       optional: true               # необязательно: провал — замечание, а не ошибка
    internal static partial class AlgoNeoYamlChecker
    {
        private const float DefaultTolerance = 0.01f;
        private const float DefaultColorTolerance = 0.02f;

        private static void EvaluateFieldRule(CheckRule rule, CheckRuleResult result)
        {
            try
            {
                GameObject go;
                if (!string.IsNullOrEmpty(rule.prefab))
                {
                    go = AlgoNeoEditorUtils.FindPrefabObject(rule.prefab, out string prefabProblem);
                    if (go == null)
                    {
                        result.hint = prefabProblem;
                        return;
                    }
                }
                else
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
                    result.hint = $"«{type.FullName}» — не компонент";
                    return;
                }

                Component component = go.GetComponent(type);
                if (component == null)
                {
                    result.hint = string.IsNullOrEmpty(rule.prefab)
                        ? $"у объекта нет компонента {rule.value}"
                        : $"у префаба нет компонента {rule.value}";
                    return;
                }

                if (!TryReadMember(component, rule.member, out object actual, out string readProblem))
                {
                    result.hint = readProblem;
                    return;
                }

                if (rule.expectCount)
                {
                    CompareCount(rule, actual, result);
                    return;
                }

                if (!TryCompare(actual, rule.expected, rule.tolerance, out bool equal, out string shown,
                        out string compareProblem))
                {
                    rule.problem = result.hint = compareProblem;
                    return;
                }

                result.ok = equal;
                result.info = "сейчас " + shown;
                result.hint = equal ? null : $"сейчас {shown}, в уроке {ShownExpected(rule.expected)}";
            }
            catch (Exception ex)
            {
                result.ok = false;
                result.hint = "не удалось проверить: " + (ex.InnerException ?? ex).Message;
            }
        }

        private static void CompareCount(CheckRule rule, object actual, CheckRuleResult result)
        {
            int wanted = int.Parse(rule.expected, NumberStyles.Integer, CultureInfo.InvariantCulture);
            int count;
            if (actual == null)
            {
                count = 0;
            }
            else if (actual is ICollection collection)
            {
                count = collection.Count;
            }
            else
            {
                rule.problem = result.hint = "это поле не массив и не список — count к нему не подходит";
                return;
            }

            result.ok = count == wanted;
            result.info = "элементов " + count;
            result.hint = result.ok ? null : $"сейчас элементов {count}, в уроке {wanted}";
        }

        // Путь "main.duration": по шагам читаем публичное поле или свойство. Скрытое поле с
        // [SerializeField] тоже подходит — его видно в инспекторе.
        internal static bool TryReadMember(object owner, string path, out object value, out string problem)
        {
            value = owner;
            problem = null;
            foreach (string rawPart in (path ?? string.Empty).Split('.'))
            {
                string part = rawPart.Trim();
                if (part.Length == 0 || value == null)
                {
                    problem = $"не удалось прочитать «{path}»";
                    return false;
                }

                Type type = value.GetType();
                try
                {
                    MemberInfo member = FindMember(type, part);
                    if (member == null)
                    {
                        problem = $"у {type.Name} нет поля или свойства «{part}»";
                        return false;
                    }

                    value = member is FieldInfo fieldInfo
                        ? fieldInfo.GetValue(value)
                        : ((PropertyInfo)member).GetValue(value);
                }
                catch (Exception ex)
                {
                    problem = $"не удалось прочитать «{part}»: {(ex.InnerException ?? ex).Message}";
                    return false;
                }
            }

            return true;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            const BindingFlags publicFlags = BindingFlags.Instance | BindingFlags.Public;
            foreach (bool ignoreCase in new[] { false, true })
            {
                BindingFlags flags = ignoreCase ? publicFlags | BindingFlags.IgnoreCase : publicFlags;
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    return property;
                }

                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    return field;
                }
            }

            // [SerializeField] private — тоже то, что видно в инспекторе.
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                FieldInfo hidden = t.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (hidden != null && hidden.IsDefined(typeof(SerializeField), true))
                {
                    return hidden;
                }
            }

            return null;
        }

        // Сравнение по типу значения: число (с допуском), bool, enum по имени, строка, вектор,
        // цвет, ссылка на объект Unity (по имени объекта; "none" — пусто).
        internal static bool TryCompare(object actual, string expected, float? tolerance, out bool equal,
            out string shown, out string problem)
        {
            equal = false;
            problem = null;
            expected ??= string.Empty;
            string wanted = expected.Trim();
            bool wantsEmpty = wanted.Length == 0 || IsNoneWord(wanted);

            if (actual is Object || (actual == null && IsUnityObjectSlot(expected)))
            {
                // Unity-null: удалённый объект — тоже «пусто».
                Object asObject = actual as Object;
                bool empty = asObject == null;
                shown = empty ? "пусто" : $"«{asObject.name}»";
                equal = wantsEmpty ? empty : !empty && string.Equals(asObject.name, wanted, StringComparison.Ordinal);
                return true;
            }

            if (actual == null)
            {
                shown = "пусто";
                equal = wantsEmpty;
                return true;
            }

            float tol = tolerance ?? DefaultTolerance;
            switch (actual)
            {
                case bool flag:
                    shown = flag ? "true" : "false";
                    if (!TryParseBool(wanted, out bool wantedFlag))
                    {
                        problem = $"equals: «{expected}» — для галочки пишите true или false";
                        return false;
                    }

                    equal = flag == wantedFlag;
                    return true;

                case Enum enumValue:
                    shown = enumValue.ToString();
                    if (int.TryParse(wanted, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    {
                        equal = Convert.ToInt64(enumValue, CultureInfo.InvariantCulture) == number;
                        return true;
                    }

                    try
                    {
                        object parsed = Enum.Parse(enumValue.GetType(), wanted.Replace(" ", string.Empty), true);
                        equal = Equals(parsed, enumValue);
                        return true;
                    }
                    catch
                    {
                        problem = $"equals: «{expected}» — у {enumValue.GetType().Name} такого варианта нет " +
                                  $"(есть: {string.Join(", ", Enum.GetNames(enumValue.GetType()))})";
                        return false;
                    }

                case string text:
                    shown = $"«{text}»";
                    equal = string.Equals(text, expected, StringComparison.Ordinal);
                    return true;

                case Color color:
                    return CompareColor(color, wanted, tolerance ?? DefaultColorTolerance, out equal, out shown,
                        out problem);

                case Color32 color32:
                    return CompareColor(color32, wanted, tolerance ?? DefaultColorTolerance, out equal, out shown,
                        out problem);

                case Vector2 v2:
                    return CompareVector(new[] { v2.x, v2.y }, wanted, tol, out equal, out shown, out problem);
                case Vector3 v3:
                    return CompareVector(new[] { v3.x, v3.y, v3.z }, wanted, tol, out equal, out shown, out problem);
                case Vector4 v4:
                    return CompareVector(new[] { v4.x, v4.y, v4.z, v4.w }, wanted, tol, out equal, out shown,
                        out problem);
                case Vector2Int i2:
                    return CompareVector(new float[] { i2.x, i2.y }, wanted, tol, out equal, out shown, out problem);
                case Vector3Int i3:
                    return CompareVector(new float[] { i3.x, i3.y, i3.z }, wanted, tol, out equal, out shown,
                        out problem);
            }

            if (IsNumber(actual))
            {
                double value = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
                shown = FormatNumber(value);
                if (!TryParseNumber(wanted, out double wantedNumber))
                {
                    problem = $"equals: «{expected}» — нужно число (дробная часть через точку)";
                    return false;
                }

                equal = !double.IsNaN(value) && !double.IsInfinity(value) &&
                        Math.Abs(value - wantedNumber) <= tol + 1e-6;
                return true;
            }

            // Остальное — по тексту, как его показал бы ToString().
            shown = $"«{actual}»";
            equal = string.Equals(actual.ToString(), expected, StringComparison.Ordinal);
            return true;
        }

        private static bool CompareVector(float[] actual, string wanted, float tolerance, out bool equal,
            out string shown, out string problem)
        {
            equal = false;
            problem = null;
            shown = string.Join(", ", Array.ConvertAll(actual, v => FormatNumber(v)));
            if (!TryParseNumbers(wanted, out double[] parts) || parts.Length == 0 || parts.Length > actual.Length)
            {
                problem = $"equals: «{wanted}» — нужно {actual.Length} числа через запятую, например \"0, -4.2, 0\"";
                return false;
            }

            // "0, -4.2" у Vector3 сравнивает только x и y.
            equal = Array.TrueForAll(actual, v => !float.IsNaN(v) && !float.IsInfinity(v));
            for (int i = 0; i < parts.Length; i++)
            {
                if (Math.Abs(actual[i] - parts[i]) > tolerance + 1e-6)
                {
                    equal = false;
                }
            }

            return true;
        }

        private static bool CompareColor(Color actual, string wanted, float tolerance, out bool equal,
            out string shown, out string problem)
        {
            equal = false;
            problem = null;
            shown = "#" + ColorUtility.ToHtmlStringRGBA(actual);
            Color target;
            if (ColorUtility.TryParseHtmlString(wanted, out Color parsed))
            {
                target = parsed;
            }
            else if (TryParseNumbers(wanted, out double[] parts) && (parts.Length == 3 || parts.Length == 4))
            {
                target = new Color((float)parts[0], (float)parts[1], (float)parts[2],
                    parts.Length == 4 ? (float)parts[3] : 1f);
            }
            else
            {
                problem = $"equals: «{wanted}» — цвет пишите как \"#RRGGBB\" или \"#RRGGBBAA\"";
                return false;
            }

            // Без альфы в записи (#RRGGBB) прозрачность не сравниваем.
            bool withAlpha = wanted.StartsWith("#", StringComparison.Ordinal) ? wanted.Length == 9 : true;
            equal = !float.IsNaN(actual.a) && !float.IsInfinity(actual.a) &&
                    Math.Abs(actual.r - target.r) <= tolerance && Math.Abs(actual.g - target.g) <= tolerance &&
                    Math.Abs(actual.b - target.b) <= tolerance &&
                    (!withAlpha || Math.Abs(actual.a - target.a) <= tolerance);
            if (!withAlpha)
            {
                shown = "#" + ColorUtility.ToHtmlStringRGB(actual);
            }

            return true;
        }

        private static bool TryParseNumbers(string text, out double[] numbers)
        {
            string[] parts = (text ?? string.Empty).Trim().Trim('(', ')').Split(new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            numbers = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseNumber(parts[i], out numbers[i]))
                {
                    return false;
                }
            }

            return parts.Length > 0;
        }

        private static bool IsNumber(object value)
        {
            return value is int || value is float || value is double || value is long || value is short ||
                   value is byte || value is uint || value is ulong || value is ushort || value is sbyte ||
                   value is decimal;
        }

        private static string FormatNumber(double value)
        {
            return Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool IsNoneWord(string text)
        {
            string lower = text.ToLowerInvariant();
            return lower == "none" || lower == "null" || lower == "пусто";
        }

        // Пустое поле-ссылка возвращает null без типа; если ждали имя объекта — сравниваем как ссылку.
        private static bool IsUnityObjectSlot(string expected)
        {
            return !string.IsNullOrEmpty(expected) && !IsNoneWord(expected.Trim()) &&
                   !TryParseNumber(expected, out _) && !TryParseBool(expected, out _);
        }

        private static string ShownExpected(string expected)
        {
            return string.IsNullOrEmpty(expected) ? "пусто" : expected;
        }
    }
}
