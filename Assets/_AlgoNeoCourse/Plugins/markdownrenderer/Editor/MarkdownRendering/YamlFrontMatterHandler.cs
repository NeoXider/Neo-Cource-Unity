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

                    // Безопасный разбор по ПЕРВОМУ двоеточию: строка без ':' (например "uss") раньше роняла весь слайд.
                    int c = data.IndexOf(':');
                    if (c < 0)
                    {
                        continue;
                    }

                    if (data.Substring(0, c).Trim() == "uss")
                    {
                        string path = data.Substring(c + 1).Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            renderer.AddCustomUSS(path);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("YamlFrontMatterHandler: ошибка разбора front matter — " + ex.Message);
            }
        }
    }
}