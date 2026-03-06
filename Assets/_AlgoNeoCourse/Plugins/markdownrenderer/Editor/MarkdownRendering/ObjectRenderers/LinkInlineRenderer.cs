using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax.Inlines;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class LinkInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, LinkInline>
    {
        private const string RequestUserAgent = "AlgoNeoCourseEditor/1.0";
        private static readonly Dictionary<string, DateTime> s_SkipImageUrlsUntil = new();

        protected override void Write(UIMarkdownRenderer renderer, LinkInline obj)
        {
            string link = obj.GetDynamicUrl != null ? obj.GetDynamicUrl() ?? obj.Url : obj.Url;

            if (!obj.IsImage)
            {
                renderer.OpenLink(link);
                renderer.WriteChildren(obj);
                renderer.CloseLink();
            }
            else
            {
                link = renderer.ResolveLink(link);
                if (!link.StartsWith("http"))
                {
                    link = "file://" + Path.Combine(renderer.FileFolder, link);
                }

                string[] videoFilesTypes =
                    { ".asf", ".avi", ".dv", ".m4v", ".mov", ".mp4", ".mpg", ".mpeg", ".ogv", ".vp8", ".webm", ".wmv" };

                VisualElement resultingElement = null;

                string ext = Path.GetExtension(link);

                if (videoFilesTypes.Contains(ext))
                {
                    // video
                    VideoPlayerElement vidPlayer = renderer.AddVideoPlayer();
                    vidPlayer.SetVideoUrl(link, false);
                    resultingElement = vidPlayer;
                }
                else
                {
                    // hopefully image
                    Image imgElem = renderer.AddImage();
                    if (ShouldSkipImageUrl(link))
                    {
                        resultingElement = imgElem;
                        ApplyAttributes(obj, resultingElement);
                        resultingElement.tooltip = obj.FirstChild.ToString();
                        return;
                    }

                    UnityWebRequest uwr = new(link, UnityWebRequest.kHttpVerbGET);
                    imgElem.RegisterCallback<GeometryChangedEvent>(evt =>
                    {
                        if (imgElem.image != null)
                        {
                            Texture texture = imgElem.image;
                            float aspectRatio = texture.width / (float)texture.height;
                            float targetWidth = evt.newRect.width;
                            float targetHeight = targetWidth / aspectRatio;

                            if (!Mathf.Approximately(targetWidth, evt.newRect.width) ||
                                !Mathf.Approximately(targetHeight, evt.newRect.height))
                            {
                                //we always set the width as 100% as this will allow to resize on parent resize
                                //be height will be based on aspect ratio
                                imgElem.style.width = Length.Percent(100);
                                imgElem.style.height = targetHeight;
                            }
                        }
                    });

                    uwr.downloadHandler = new DownloadHandlerTexture();
                    uwr.SetRequestHeader("User-Agent", RequestUserAgent);
                    uwr.SetRequestHeader("Accept", "image/*,*/*;q=0.8");
                    UnityWebRequestAsyncOperation asyncOp = uwr.SendWebRequest();

                    asyncOp.completed += operation =>
                    {
                        try
                        {
                            if (uwr.result != UnityWebRequest.Result.Success)
                            {
                                HandleImageRequestFailure(link, uwr);
                                return;
                            }

                            imgElem.image = DownloadHandlerTexture.GetContent(uwr);
                            ClearImageFailure(link);
                            //force a resize to call our custom callback
                            imgElem.style.width = 10;
                        }
                        catch (Exception x)
                        {
                            if (!x.Message.StartsWith("HTTP/1.1 404"))
                            {
                                HandleImageException(link, x);
                                return;
                            }

                            Debug.LogWarning($"{x.Message}: {link}");
                        }
                        finally
                        {
                            uwr.Dispose();
                        }
                    };

                    resultingElement = imgElem;
                }

                ApplyAttributes(obj, resultingElement);
                resultingElement.tooltip = obj.FirstChild.ToString();
            }
        }

        private static void ApplyAttributes(LinkInline obj, VisualElement resultingElement)
        {
            HtmlAttributes attribute = obj.GetAttributes();
            if (attribute.Classes != null)
            {
                foreach (string c in attribute.Classes)
                {
                    resultingElement.AddToClassList(c);
                }
            }
        }

        private static bool ShouldSkipImageUrl(string link)
        {
            if (s_SkipImageUrlsUntil.TryGetValue(link, out DateTime until))
            {
                if (until > DateTime.UtcNow)
                {
                    return true;
                }

                s_SkipImageUrlsUntil.Remove(link);
            }

            return false;
        }

        private static void ClearImageFailure(string link)
        {
            s_SkipImageUrlsUntil.Remove(link);
        }

        private static void HandleImageRequestFailure(string link, UnityWebRequest uwr)
        {
            long responseCode = uwr.responseCode;
            if (responseCode == 404)
            {
                Debug.LogWarning($"HTTP/1.1 404 Not Found: {link}");
                return;
            }

            if (responseCode == 403 || responseCode == 429)
            {
                TimeSpan cooldown = responseCode == 429 ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(10);
                s_SkipImageUrlsUntil[link] = DateTime.UtcNow.Add(cooldown);
                return;
            }

            Debug.LogWarning($"Markdown image request failed: HTTP {responseCode} {link}");
        }

        private static void HandleImageException(string link, Exception exception)
        {
            if (exception.Message.StartsWith("HTTP/1.1 429"))
            {
                s_SkipImageUrlsUntil[link] = DateTime.UtcNow.AddMinutes(2);
                return;
            }

            if (exception.Message.StartsWith("HTTP/1.1 403"))
            {
                s_SkipImageUrlsUntil[link] = DateTime.UtcNow.AddMinutes(10);
                return;
            }

            Debug.LogWarning($"Markdown image request failed: {exception.Message}: {link}");
        }
    }
}