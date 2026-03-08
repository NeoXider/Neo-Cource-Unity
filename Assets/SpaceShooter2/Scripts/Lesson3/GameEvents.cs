using System;
using UnityEngine.Events;

namespace SpaceShooter2.Lesson3
{
    [Serializable]
    public class ScoreChangedEvent : UnityEvent<int>
    {
    }
}
