using System;
using System.Collections.Generic;
using ColorBlocks.Core;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlocks.Tests
{
    public sealed class LevelSolverTests
    {
        [Test]
        public void StrategicLevel_ReportsAndReplaysAWinningAndLosingPath()
        {
            LevelDefinition level = CreateLevel(
                new[] { new CellDefinition(0, 0, new[] { BlockColorId.Red }) },
                1,
                1,
                Lane(Unit(BlockColorId.Blue)),
                Lane(Unit(BlockColorId.Yellow)),
                Lane(Unit(BlockColorId.Green)),
                Lane(Unit(BlockColorId.Purple)),
                Lane(Unit(BlockColorId.Blue), Unit(BlockColorId.Red)));

            LevelSolveResult solution = LevelSolver.Solve(level);

            Assert.That(solution.IsSolvable, Is.True);
            Assert.That(solution.SearchLimitReached, Is.False);
            Assert.That(solution.WinningLaneChoices, Is.EqualTo(new[] { 1, 2, 3, 5, 5 }));
            Assert.That(solution.HasLosingPath, Is.True);
            Assert.That(solution.FirstLosingLaneChoices, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(
                LevelSolver.Simulate(level, solution.WinningLaneChoices).Status,
                Is.EqualTo(LevelSimulationStatus.Won));
            Assert.That(
                LevelSolver.Simulate(level, solution.FirstLosingLaneChoices).Status,
                Is.EqualTo(LevelSimulationStatus.Lost));
            UnityEngine.Object.DestroyImmediate(level);
        }

        [Test]
        public void ArbitraryDepthAndVerticalFrontier_AreClearedInExposureOrder()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new CellDefinition(0, 0, new[]
                    {
                        BlockColorId.Red,
                        BlockColorId.Blue,
                        BlockColorId.Green,
                        BlockColorId.Purple
                    }),
                    new CellDefinition(0, 2, new[] { BlockColorId.Yellow })
                },
                1,
                3,
                Lane(
                    Unit(BlockColorId.Purple),
                    Unit(BlockColorId.Green),
                    Unit(BlockColorId.Blue),
                    Unit(BlockColorId.Red),
                    Unit(BlockColorId.Yellow)),
                Lane(),
                Lane(),
                Lane(),
                Lane());

            LevelSolveResult solution = LevelSolver.Solve(level);

            Assert.That(solution.IsSolvable, Is.True);
            Assert.That(solution.WinningLaneChoices, Is.EqualTo(new[] { 1, 1, 1, 1, 1 }));
            Assert.That(
                LevelSolver.Simulate(level, solution.WinningLaneChoices).RemainingBlockCount,
                Is.Zero);
            UnityEngine.Object.DestroyImmediate(level);
        }

        [Test]
        public void ExhaustedUnit_LeavesAHoleThatTheNextSelectionReuses()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new CellDefinition(0, 0, new[] { BlockColorId.Red }),
                    new CellDefinition(1, 0, new[] { BlockColorId.Blue })
                },
                2,
                1,
                Lane(Unit(BlockColorId.Green)),
                Lane(Unit(BlockColorId.Red)),
                Lane(Unit(BlockColorId.Purple)),
                Lane(Unit(BlockColorId.Blue)),
                Lane());

            LevelSimulationResult replay = LevelSolver.Simulate(level, new[] { 1, 2, 3 });

            Assert.That(replay.Status, Is.EqualTo(LevelSimulationStatus.AwaitingChoice));
            Assert.That(replay.RemainingBlockCount, Is.EqualTo(1));
            Assert.That(replay.Slots[0].IsOccupied, Is.True);
            Assert.That(replay.Slots[0].Color, Is.EqualTo(BlockColorId.Green));
            Assert.That(replay.Slots[1].IsOccupied, Is.True,
                "The third unit must reuse the slot-one hole left by the exhausted red unit.");
            Assert.That(replay.Slots[1].Color, Is.EqualTo(BlockColorId.Purple));
            Assert.That(replay.Slots[2].IsOccupied, Is.False,
                "Fixed slots must not compact or append past an earlier hole.");
            UnityEngine.Object.DestroyImmediate(level);
        }

        private static LevelDefinition CreateLevel(
            IEnumerable<CellDefinition> cells,
            int width,
            int height,
            params UnitLaneDefinition[] lanes)
        {
            Assert.That(lanes.Length, Is.EqualTo(LevelSolver.LaneCount));
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(1, width, height, cells, lanes);
            return level;
        }

        private static UnitLaneDefinition Lane(params UnitDefinition[] units)
        {
            return new UnitLaneDefinition(units);
        }

        private static UnitDefinition Unit(BlockColorId color, int charges = 1)
        {
            return new UnitDefinition(color, charges);
        }
    }
}
