using System;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using UnityEngine;

namespace UIMarkdownRenderer
{
    //This is not a renderer as we ignore the YAML front matter block and only use its data
    public class YamlFrontMatterHandler : MarkdownObjectRenderer<UIMarkdownRenderer, YamlFrontMatterBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, YamlFrontMatterBlock obj)
        {
            try
            {
                //we do not handle real YAML as for now we only support specific uss, so manually parse
                foreach (object line in obj.Lines)
                {
                    string data = line.ToString();
                    if (string.IsNullOrEmpty(data))
                    {
                        continue;
                    }

                    // Разбор по ПЕРВОМУ двоеточию: строка без ':' (например "uss") раньше роняла весь рендер
                    // выходом за границы массива, а путь с двоеточием ("C:/…") обрезался по второму.
                    int separator = data.IndexOf(':');
                    if (separator < 0)
                    {
                        continue;
                    }

                    if (data.Substring(0, separator).Trim() == "uss")
                    {
                        string path = data.Substring(separator + 1).Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            renderer.AddCustomUSS(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Front matter — необязательные метаданные: его ошибка не должна оставлять документ пустым.
                Debug.LogWarning($"Markdown front matter could not be parsed: {ex.Message}");
            }
        }
    }
}
