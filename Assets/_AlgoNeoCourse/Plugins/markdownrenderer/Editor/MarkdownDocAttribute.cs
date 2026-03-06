using System;

namespace UIMarkdownRenderer
{
    public class MarkdownDocAttribute : Attribute
    {
        public MarkdownDocAttribute(string docName)
        {
            DocName = docName;
        }

        public string DocName { get; }
    }
}