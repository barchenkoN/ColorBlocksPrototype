using System;
using System.Collections.Generic;
using ColorBlocks.Core;
using NUnit.Framework;
using UnityEditor;

namespace ColorBlocks.Tests
{
    public sealed class GeneratedLevelTests
    {
        [Test]
        public void Catalog_ContainsFiveValidLevelsWithExactAmmoBalance()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/_Game/Resources/LevelCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Levels.Count, Is.EqualTo(5));

            for (int i = 0; i < catalog.Levels.Count; i++)
            {
                LevelDefinition level = catalog.Levels[i];
                Assert.That(level.LevelNumber, Is.EqualTo(i + 1));
                Assert.That(LevelValidator.Validate(level), Is.Empty);
                AssertExactBalance(level);
                Assert.That(MaxLayerCount(level), Is.LessThanOrEqualTo(2));
                AssertBoardUsesDeclaredBounds(level);
                AssertGravityStable(level);
            }
        }

        [Test]
        public void LevelThree_IsFirstLevelThatUsesTwoLayers()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/_Game/Resources/LevelCatalog.asset");
            Assert.That(MaxLayerCount(catalog.GetLevel(1)), Is.EqualTo(1));
            Assert.That(MaxLayerCount(catalog.GetLevel(2)), Is.EqualTo(1));
            Assert.That(MaxLayerCount(catalog.GetLevel(3)), Is.EqualTo(2));
        }

        [Test]
        public void CanonicalLevels_KeepDenseReferenceScaleAndLayerProgression()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/_Game/Resources/LevelCatalog.asset");
            int[] widths = { 8, 9, 9, 10, 10 };
            int[] heights = { 7, 7, 8, 8, 10 };
            int[] cells = { 50, 57, 64, 72, 92 };
            int[] doubleCells = { 0, 0, 13, 18, 30 };
            int[] blockLayers = { 50, 57, 77, 90, 122 };
            int[] units = { 6, 7, 10, 10, 15 };

            for (int levelIndex = 0; levelIndex < catalog.Levels.Count; levelIndex++)
            {
                LevelDefinition level = catalog.Levels[levelIndex];
                int doubles = 0;
                int layers = 0;
                for (int cellIndex = 0; cellIndex < level.Cells.Count; cellIndex++)
                {
                    int layerCount = level.Cells[cellIndex].Layers.Count;
                    layers += layerCount;
                    if (layerCount == 2)
                    {
                        doubles++;
                        Assert.That(
                            level.Cells[cellIndex].Layers[0].Color,
                            Is.Not.EqualTo(level.Cells[cellIndex].Layers[1].Color),
                            $"L{level.LevelNumber} has an unreadable same-color double layer.");
                    }
                }

                int unitCount = 0;
                for (int lane = 0; lane < level.Lanes.Count; lane++) unitCount += level.Lanes[lane].Units.Count;
                Assert.That(level.Width, Is.EqualTo(widths[levelIndex]));
                Assert.That(level.Height, Is.EqualTo(heights[levelIndex]));
                Assert.That(level.Cells.Count, Is.EqualTo(cells[levelIndex]));
                Assert.That(doubles, Is.EqualTo(doubleCells[levelIndex]));
                Assert.That(layers, Is.EqualTo(blockLayers[levelIndex]));
                Assert.That(unitCount, Is.EqualTo(units[levelIndex]));
            }
        }

        [Test]
        public void EveryGeneratedLevel_IsSolvableByAValidFrontQueueStrategy()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/_Game/Resources/LevelCatalog.asset");

            for (int levelIndex = 0; levelIndex < catalog.Levels.Count; levelIndex++)
            {
                LevelDefinition level = catalog.Levels[levelIndex];
                LevelSolveResult solution = LevelSolver.Solve(level);
                Assert.That(solution.SearchLimitReached, Is.False,
                    $"Level {level.LevelNumber} exceeded the solver state limit.");
                Assert.That(solution.IsSolvable, Is.True,
                    $"Level {level.LevelNumber} has no winning lane-choice sequence. " +
                    $"Explored {solution.ExploredStateCount} canonical states.");

                LevelSimulationResult replay = LevelSolver.Simulate(level, solution.WinningLaneChoices);
                Assert.That(replay.Status, Is.EqualTo(LevelSimulationStatus.Won),
                    $"Level {level.LevelNumber} solver path did not replay to a win.");
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void StrategicOpening_PunishesFiveIncorrectSelections(int levelNumber)
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/_Game/Resources/LevelCatalog.asset");
            int repeatedBlockerLane = levelNumber == 2 ? 2 : 3;
            LevelSimulationResult result = LevelSolver.Simulate(
                catalog.GetLevel(levelNumber),
                new[] { 2, 3, 4, 5, repeatedBlockerLane });
            Assert.That(result.Status, Is.EqualTo(LevelSimulationStatus.Lost));
            Assert.That(result.RemainingBlockCount, Is.GreaterThan(0));
        }

        private static void AssertExactBalance(LevelDefinition level)
        {
            Dictionary<BlockColorId, int> blocks = new();
            Dictionary<BlockColorId, int> charges = new();
            for (int cell = 0; cell < level.Cells.Count; cell++)
            {
                for (int layer = 0; layer < level.Cells[cell].Layers.Count; layer++)
                {
                    Add(blocks, level.Cells[cell].Layers[layer].Color, 1);
                }
            }

            for (int lane = 0; lane < level.Lanes.Count; lane++)
            {
                for (int unit = 0; unit < level.Lanes[lane].Units.Count; unit++)
                {
                    UnitDefinition definition = level.Lanes[lane].Units[unit];
                    Add(charges, definition.Color, definition.Charges);
                }
            }

            foreach (BlockColorId color in Enum.GetValues(typeof(BlockColorId)))
            {
                blocks.TryGetValue(color, out int blockCount);
                charges.TryGetValue(color, out int chargeCount);
                Assert.That(chargeCount, Is.EqualTo(blockCount), $"{level.name}: {color}");
            }
        }

        private static int MaxLayerCount(LevelDefinition level)
        {
            int maximum = 0;
            for (int i = 0; i < level.Cells.Count; i++)
            {
                maximum = Math.Max(maximum, level.Cells[i].Layers.Count);
            }
            return maximum;
        }

        private static void AssertBoardUsesDeclaredBounds(LevelDefinition level)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int i = 0; i < level.Cells.Count; i++)
            {
                minX = Math.Min(minX, level.Cells[i].X);
                minY = Math.Min(minY, level.Cells[i].Y);
                maxX = Math.Max(maxX, level.Cells[i].X);
                maxY = Math.Max(maxY, level.Cells[i].Y);
            }

            Assert.That(minX, Is.EqualTo(0), $"{level.name} has horizontal padding.");
            Assert.That(minY, Is.EqualTo(0), $"{level.name} has vertical padding.");
            Assert.That(maxX, Is.EqualTo(level.Width - 1), $"{level.name} does not use its declared width.");
            Assert.That(maxY, Is.EqualTo(level.Height - 1), $"{level.name} does not use its declared height.");
        }

        private static void AssertGravityStable(LevelDefinition level)
        {
            for (int x = 0; x < level.Width; x++)
            {
                List<int> occupiedY = new();
                for (int cellIndex = 0; cellIndex < level.Cells.Count; cellIndex++)
                {
                    CellDefinition cell = level.Cells[cellIndex];
                    if (cell.X == x) occupiedY.Add(cell.Y);
                }

                occupiedY.Sort();
                for (int index = 0; index < occupiedY.Count; index++)
                {
                    Assert.That(occupiedY[index], Is.EqualTo(index),
                        $"{level.name} column {x} has an unsupported gap below y={occupiedY[index]}.");
                }
            }
        }

        private static void Add(Dictionary<BlockColorId, int> values, BlockColorId color, int amount)
        {
            values.TryGetValue(color, out int current);
            values[color] = current + amount;
        }
    }
}
