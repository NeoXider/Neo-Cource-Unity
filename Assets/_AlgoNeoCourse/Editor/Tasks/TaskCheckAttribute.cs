using System;

namespace NeoCource.Editor.Tasks
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TaskCheckAttribute : Attribute
    {
        public string Key { get; }
        public TaskCheckAttribute(string key) => Key = key;
    }
}


