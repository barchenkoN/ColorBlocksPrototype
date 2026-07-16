using System.Collections.Generic;
using ColorBlocks.Core;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlocks.Tests
{
    public sealed class BoardModelTests
    {
        [Test]
        public void CoveredLayer_CannotBeTargetedUntilTopLayerIsDestroyed()
        {
            LevelDefinition level = CreateLevel(new[]
            {
                new CellDefinition(0, 0, new[]
                {
                    BlockColorId.Red,
                    BlockColorId.Green,
                    BlockColorId.Blue
                }),
                new CellDefinition(0, 1, new[] { BlockColorId.Red })
            });
            BoardModel board = new(level);

            Assert.That(board.HasAvailableTarget(BlockColorId.Red), Is.False);
            Assert.That(board.HasAvailableTarget(BlockColorId.Green), Is.False);
            Assert.That(board.TryReserveTarget(BlockColorId.Blue, 0f, out TargetReservation top), Is.True);
            BoardMutation firstMutation = board.Destroy(top);
            BlockNode firstReveal = firstMutation.RevealedNode;

            Assert.That(firstReveal, Is.Not.Null);
            Assert.That(firstReveal.Color, Is.EqualTo(BlockColorId.Green));
            Assert.That(board.HasAvailableTarget(BlockColorId.Green), Is.True);
            Assert.That(board.HasAvailableTarget(BlockColorId.Red), Is.False,
                "Both the lower depth layer and the cell above must remain occluded.");

            Assert.That(board.TryReserveTarget(BlockColorId.Green, 0f, out TargetReservation middle), Is.True);
            BoardMutation secondMutation = board.Destroy(middle);
            BlockNode secondReveal = secondMutation.RevealedNode;
            Assert.That(secondReveal, Is.Not.Null);
            Assert.That(secondReveal.Color, Is.EqualTo(BlockColorId.Red));
            Assert.That(secondReveal.LayerIndex, Is.EqualTo(0));
            Object.DestroyImmediate(level);
        }

        [Test]
        public void VerticalOcclusion_OnlyLowestLivingCellInEachColumnIsAttackable()
        {
            LevelDefinition level = CreateLevel(new[]
            {
                new CellDefinition(0, 0, new[] { BlockColorId.Blue }),
                new CellDefinition(0, 3, new[] { BlockColorId.Yellow }),
                new CellDefinition(1, 2, new[] { BlockColorId.Yellow })
            }, 2, 4);
            BoardModel board = new(level);

            Assert.That(board.HasAvailableTarget(BlockColorId.Yellow), Is.True,
                "The independent frontier in column 1 should remain targetable.");
            Assert.That(board.TryReserveTarget(BlockColorId.Yellow, 0f, out TargetReservation target), Is.True);
            Assert.That(target.Position, Is.EqualTo(new GridPosition(1, 0)),
                "The independent yellow column must settle to the bottom; the yellow stack above " +
                "the blue frontier in column 0 must remain untargetable.");
            Object.DestroyImmediate(level);
        }

        [Test]
        public void Reservation_PreventsTwoUnitsFromChoosingTheSameBlock()
        {
            LevelDefinition level = CreateLevel(new[]
            {
                new CellDefinition(0, 0, new[] { BlockColorId.Green }),
                new CellDefinition(0, 1, new[] { BlockColorId.Green })
            });
            BoardModel board = new(level);

            Assert.That(board.TryReserveTarget(BlockColorId.Green, 0f, out TargetReservation first), Is.True);
            Assert.That(first.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(board.HasAvailableTarget(BlockColorId.Green), Is.False,
                "A reserved frontier must continue blocking all cells above it in that column.");
            Assert.That(board.TryReserveTarget(BlockColorId.Green, 0f, out _), Is.False);
            board.Release(first);
            Assert.That(board.TryReserveTarget(BlockColorId.Green, 0f, out TargetReservation retried), Is.True);
            Assert.That(retried.Position, Is.EqualTo(new GridPosition(0, 0)));
            Object.DestroyImmediate(level);
        }

        [Test]
        public void DestroyingFinalLayer_CompactsHigherStacksToTheBottom()
        {
            LevelDefinition level = CreateLevel(new[]
            {
                new CellDefinition(0, 0, new[] { BlockColorId.Blue }),
                new CellDefinition(0, 3, new[] { BlockColorId.Yellow })
            }, 1, 4);
            BoardModel board = new(level);

            Assert.That(board.HasAvailableTarget(BlockColorId.Yellow), Is.False);
            Assert.That(board.TryReserveTarget(BlockColorId.Blue, 0f, out TargetReservation lower), Is.True);
            BoardMutation mutation = board.Destroy(lower);
            Assert.That(mutation.RevealedNode, Is.Null,
                "Destroying a stack's final layer must not report an in-stack reveal.");
            Assert.That(mutation.Falls, Has.Count.EqualTo(1));
            Assert.That(mutation.Falls[0].From, Is.EqualTo(new GridPosition(0, 1)),
                "The model must settle authored gaps before gameplay begins.");
            Assert.That(mutation.Falls[0].To, Is.EqualTo(new GridPosition(0, 0)));

            Assert.That(board.TryReserveTarget(BlockColorId.Yellow, 0f, out TargetReservation upper), Is.True);
            Assert.That(upper.Position, Is.EqualTo(new GridPosition(0, 0)),
                "The next stack must fall into the cleared bottom position before it is targeted.");
            Object.DestroyImmediate(level);
        }

        [Test]
        public void InitialBoard_CompactsEveryColumnWithoutChangingStackOrder()
        {
            LevelDefinition level = CreateLevel(new[]
            {
                new CellDefinition(0, 2, new[] { BlockColorId.Red }),
                new CellDefinition(0, 5, new[] { BlockColorId.Green }),
                new CellDefinition(0, 8, new[] { BlockColorId.Blue })
            }, 1, 9);
            BoardModel board = new(level);

            Assert.That(board.Stacks[0].Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(board.Stacks[1].Position, Is.EqualTo(new GridPosition(0, 1)));
            Assert.That(board.Stacks[2].Position, Is.EqualTo(new GridPosition(0, 2)));
            Assert.That(board.TryReserveTarget(BlockColorId.Red, 0f, out _), Is.True,
                "Compaction must preserve bottom-to-top authored order.");
            Object.DestroyImmediate(level);
        }

        [Test]
        public void Targeting_PrefersNearestColumnThenLeftmostOnEqualDistance()
        {
            LevelDefinition level = CreateLevel(new[]
            {
                new CellDefinition(0, 1, new[] { BlockColorId.Yellow }),
                new CellDefinition(2, 0, new[] { BlockColorId.Yellow }),
                new CellDefinition(4, 4, new[] { BlockColorId.Yellow })
            }, 5, 5);
            BoardModel board = new(level);

            Assert.That(board.TryReserveTarget(BlockColorId.Yellow, 3f, out TargetReservation tied), Is.True);
            Assert.That(tied.Position, Is.EqualTo(new GridPosition(2, 0)),
                "Equal horizontal distance must resolve to the leftmost column, regardless of Y.");
            board.Release(tied);

            Assert.That(board.TryReserveTarget(BlockColorId.Yellow, 3.8f, out TargetReservation nearest), Is.True);
            Assert.That(nearest.Position, Is.EqualTo(new GridPosition(4, 0)),
                "Horizontal distance has priority after each independent column settles to the bottom.");
            Object.DestroyImmediate(level);
        }

        private static LevelDefinition CreateLevel(
            IEnumerable<CellDefinition> cells,
            int width = 2,
            int height = 2)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            List<UnitLaneDefinition> lanes = new();
            for (int i = 0; i < 5; i++) lanes.Add(new UnitLaneDefinition(System.Array.Empty<UnitDefinition>()));
            level.Configure(1, width, height, cells, lanes);
            return level;
        }
    }
}
