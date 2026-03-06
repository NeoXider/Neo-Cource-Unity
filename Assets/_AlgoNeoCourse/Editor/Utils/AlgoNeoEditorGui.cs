using System;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Utils
{
    public static class AlgoNeoEditorGui
    {
        private static GUIStyle s_HeaderStyle;
        private static GUIStyle s_SubtitleStyle;
        private static GUIStyle s_SectionTitleStyle;
        private static GUIStyle s_InfoStyle;

        private static GUIStyle HeaderStyle
        {
            get
            {
                if (s_HeaderStyle == null)
                {
                    s_HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 17,
                        richText = true
                    };
                }

                return s_HeaderStyle;
            }
        }

        private static GUIStyle SubtitleStyle
        {
            get
            {
                if (s_SubtitleStyle == null)
                {
                    s_SubtitleStyle = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        fontSize = 11
                    };
                    s_SubtitleStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.72f, 0.78f, 0.86f)
                        : new Color(0.2f, 0.25f, 0.3f);
                }

                return s_SubtitleStyle;
            }
        }

        private static GUIStyle SectionTitleStyle
        {
            get
            {
                if (s_SectionTitleStyle == null)
                {
                    s_SectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12
                    };
                }

                return s_SectionTitleStyle;
            }
        }

        private static GUIStyle InfoStyle
        {
            get
            {
                if (s_InfoStyle == null)
                {
                    s_InfoStyle = new GUIStyle(EditorStyles.textField)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return s_InfoStyle;
            }
        }

        public static void DrawHeader(string title, string subtitle, Color accent)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 64f, GUILayout.ExpandWidth(true));
            Color background = EditorGUIUtility.isProSkin
                ? new Color(0.11f, 0.13f, 0.17f)
                : new Color(0.78f, 0.8f, 0.84f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), accent);

            Rect titleRect = new(rect.x + 14f, rect.y + 10f, rect.width - 24f, 22f);
            Rect subtitleRect = new(rect.x + 14f, rect.y + 34f, rect.width - 24f, 18f);

            EditorGUI.LabelField(titleRect, title, HeaderStyle);
            EditorGUI.LabelField(subtitleRect, subtitle, SubtitleStyle);
            GUILayout.Space(6f);
        }

        public static void BeginSection(string title, Color accent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(2f);

            Rect rect = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 20f, rect.width, 1f),
                new Color(accent.r, accent.g, accent.b, 0.35f));
            EditorGUI.LabelField(rect, title, SectionTitleStyle);
            GUILayout.Space(4f);
        }

        public static void EndSection()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        public static bool DrawActionButton(string text, Color color, params GUILayoutOption[] options)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayoutOption[] mergedOptions;
            if (options == null || options.Length == 0)
            {
                mergedOptions = new[] { GUILayout.Height(26f) };
            }
            else
            {
                mergedOptions = new GUILayoutOption[options.Length + 1];
                mergedOptions[0] = GUILayout.Height(26f);
                Array.Copy(options, 0, mergedOptions, 1, options.Length);
            }

            bool clicked = GUILayout.Button(text, mergedOptions);
            GUI.backgroundColor = previousColor;
            return clicked;
        }

        public static void DrawInfoBox(string message, MessageType messageType = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, messageType);
        }

        public static void DrawPathRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            EditorGUILayout.SelectableLabel(value ?? string.Empty, InfoStyle,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
    }
}