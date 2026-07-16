using System.Collections.Generic;
using ColorBlocks.Core;
using NUnit.Framework;

namespace ColorBlocks.Tests
{
    public sealed class LevelSequenceTests
    {
        [Test]
        public void Intro_IsAlwaysOneThenTwo()
        {
            LevelSequence sequence = new(1234);
            Assert.That(sequence.Next(), Is.EqualTo(1));
            Assert.That(sequence.Next(), Is.EqualTo(2));
        }

        [Test]
        public void AdvancedLevels_FormUniqueCyclesWithoutBoundaryRepeats()
        {
            LevelSequence sequence = new(90210);
            sequence.Next();
            sequence.Next();
            int previous = -1;

            for (int cycle = 0; cycle < 20; cycle++)
            {
                HashSet<int> seen = new();
                for (int item = 0; item < 3; item++)
                {
                    int level = sequence.Next();
                    Assert.That(level, Is.InRange(3, 5));
                    Assert.That(seen.Add(level), Is.True);
                    Assert.That(level, Is.Not.EqualTo(previous));
                    previous = level;
                }
            }
        }
    }
}
