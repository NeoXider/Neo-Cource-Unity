using System;

namespace NeoCource.Editor.Tasks
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TaskCheckAttribute : Attribute
    {
        public TaskCheckAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }
}