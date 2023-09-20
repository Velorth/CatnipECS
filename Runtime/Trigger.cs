using System;

namespace CatnipECS
{
    public readonly struct Trigger : IEquatable<Trigger>
    {
        public readonly Matcher Matcher;
        public readonly GroupEvent Event;

        public Trigger(Matcher matcher, GroupEvent groupEvent)
        {
            Matcher = matcher;
            Event = groupEvent;
        }

        public bool Equals(Trigger other)
        {
            return Matcher.Equals(other.Matcher) && Event == other.Event;
        }

        public override bool Equals(object obj)
        {
            return obj is Trigger other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Matcher, (int) Event);
        }
    }

    [Flags]
    public enum GroupEvent
    {
        None = 0,
        Add = 1 << 1,
        Remove = 1 << 2
    }
}