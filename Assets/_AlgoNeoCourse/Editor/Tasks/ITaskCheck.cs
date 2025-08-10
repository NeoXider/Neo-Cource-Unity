using System.Collections.Generic;

namespace NeoCource.Editor.Tasks
{
    public interface ITaskCheck
    {
        string Key { get; }
        bool TryExecute(Dictionary<string, string> args, out string message);
    }
}


