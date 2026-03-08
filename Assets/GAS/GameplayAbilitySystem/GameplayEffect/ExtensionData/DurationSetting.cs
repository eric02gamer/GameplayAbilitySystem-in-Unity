using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [Serializable]
    public class DurationSetting
    {
        public enum DurationPolicy
        {
            Instant,
            HasDuration,
            Infinite,
        }

        public DurationPolicy durationPolicy = DurationPolicy.Instant;
        public Magnitude durationMagnitude;
    }
}
