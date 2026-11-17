using System;

namespace Game.Networking
{
    /// <summary>One simulated internet connection: what this machine adds to every packet it sends.</summary>
    [Serializable]
    public struct NetworkConditionProfile
    {
        public string Name;

        /// <summary>Delay added to each sent packet. Both machines add theirs, so the round trip is about twice this.</summary>
        public int DelayMs;

        /// <summary>Random extra delay, 0 to this many milliseconds, per packet.</summary>
        public int JitterMs;

        /// <summary>Whole percent of sent packets that are dropped.</summary>
        public int LossPercent;

        public NetworkConditionProfile(string name, int delayMs, int jitterMs, int lossPercent)
        {
            Name = name;
            DelayMs = delayMs;
            JitterMs = jitterMs;
            LossPercent = lossPercent;
        }
    }
}
