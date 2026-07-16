using System;
using UnityEngine;

namespace ColorBlocks.Core
{
    public enum BlockColorId
    {
        Red,
        Blue,
        Yellow,
        Green,
        Purple
    }

    public enum GameFlowState
    {
        Initializing,
        LoadingLevel,
        Playing,
        Completing,
        Won,
        Lost,
        Restarting,
        Transitioning
    }

    public enum UnitRuntimeState
    {
        Queued,
        MovingToSlot,
        Waiting,
        Firing,
        Exhausted,
        Leaving,
        Recycled
    }

    [Serializable]
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }

    public static class ColorPalette
    {
        public static Color Get(BlockColorId id)
        {
            return id switch
            {
                BlockColorId.Red => new Color(0.94f, 0.20f, 0.29f),
                BlockColorId.Blue => new Color(0.16f, 0.48f, 0.98f),
                BlockColorId.Yellow => new Color(1.00f, 0.66f, 0.08f),
                BlockColorId.Green => new Color(0.16f, 0.73f, 0.39f),
                BlockColorId.Purple => new Color(0.58f, 0.24f, 0.91f),
                _ => Color.white
            };
        }

        public static Color GetDark(BlockColorId id) => Color.Lerp(Get(id), new Color(0.04f, 0.06f, 0.15f), 0.34f);
        public static Color GetLight(BlockColorId id) => Color.Lerp(Get(id), Color.white, 0.22f);
    }
}
