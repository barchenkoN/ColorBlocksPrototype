using System;
using System.Collections.Generic;

namespace ColorBlocks.Core
{
    public sealed class LevelSequence
    {
        private readonly Random _random;
        private readonly List<int> _cycle = new() { 3, 4, 5 };
        private int _introIndex;
        private int _cycleIndex = 3;
        private int _lastLevel;

        public LevelSequence(int seed)
        {
            _random = new Random(seed);
        }

        public int Next()
        {
            if (_introIndex < 2)
            {
                int introLevel = ++_introIndex;
                _lastLevel = introLevel;
                return introLevel;
            }

            if (_cycleIndex >= _cycle.Count)
            {
                ShuffleCycle();
                _cycleIndex = 0;
            }

            int result = _cycle[_cycleIndex++];
            _lastLevel = result;
            return result;
        }

        private void ShuffleCycle()
        {
            for (int i = _cycle.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                (_cycle[i], _cycle[swapIndex]) = (_cycle[swapIndex], _cycle[i]);
            }

            if (_cycle[0] == _lastLevel)
            {
                int swapIndex = 1 + _random.Next(_cycle.Count - 1);
                (_cycle[0], _cycle[swapIndex]) = (_cycle[swapIndex], _cycle[0]);
            }
        }
    }
}
