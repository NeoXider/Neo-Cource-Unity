using System;
using UnityEngine.Events;

namespace SpaceShooter2.Lesson4
{
    [Serializable]
    public class ScoreChangedEvent : UnityEvent<int>
    {
    }
}
