using System;
using System.Collections.Generic;

namespace ColorBlocks.Core
{
    public static class LevelValidator
    {
        /// <summary>Runs allocation-light structural checks suitable for runtime level loading.</summary>
        public static List<string> Validate(LevelDefinition level)
        {
            return Validate(level, false);
        }

        /// <summary>
        /// Runs structural checks plus an exhaustive, memoized solvability search. Keep this in
        /// editor tooling, tests, and pre-build validation rather than the gameplay load path.
        /// </summary>
        public static List<string> ValidateForAuthoring(
            LevelDefinition level,
            int maximumSolverStates = LevelSolver.DefaultMaximumStates)
        {
            return Validate(level, true, maximumSolverStates);
        }

        public static List<string> Validate(
            LevelDefinition level,
            bool validateSolvability,
            int maximumSolverStates = LevelSolver.DefaultMaximumStates)
        {
            List<string> errors = ValidateStructure(level);
            if (!validateSolvability || errors.Count > 0)
            {
                return errors;
            }

            LevelSolveResult solution = LevelSolver.Solve(level, maximumSolverStates);
            if (solution.SearchLimitReached)
            {
                errors.Add(
                    $"Solvability search reached its {maximumSolverStates:N0}-state limit; " +
                    "the level has not been proven solvable.");
            }
            else if (!solution.IsSolvable)
            {
                errors.Add("No legal sequence of front-lane unit selections can clear the board.");
            }

            return errors;
        }

        private static List<string> ValidateStructure(LevelDefinition level)
        {
            List<string> errors = new();
            if (level == null)
            {
                errors.Add("Level asset is null.");
                return errors;
            }

            if (level.LevelNumber <= 0)
            {
                errors.Add("Level number must be positive.");
            }

            if (level.Width <= 0 || level.Height <= 0)
            {
                errors.Add("Board dimensions must be positive.");
            }

            IReadOnlyList<CellDefinition> cells = level.Cells;
            IReadOnlyList<UnitLaneDefinition> lanes = level.Lanes;
            if (cells == null)
            {
                errors.Add("Board cell collection is null.");
            }
            else if (cells.Count == 0)
            {
                errors.Add("Board must contain at least one non-empty cell.");
            }

            if (lanes == null)
            {
                errors.Add("Exactly five unit lanes are required; the lane collection is null.");
            }
            else if (lanes.Count != LevelSolver.LaneCount)
            {
                errors.Add($"Exactly five unit lanes are required; found {lanes.Count}.");
            }

            HashSet<GridPosition> coordinates = new();
            Dictionary<BlockColorId, long> blocks = new();
            Dictionary<BlockColorId, long> charges = new();

            if (cells != null)
            {
                ValidateCells(level, cells, coordinates, blocks, errors);
            }

            if (lanes != null)
            {
                ValidateLanes(lanes, charges, errors);
            }

            foreach ((BlockColorId color, long blockCount) in blocks)
            {
                charges.TryGetValue(color, out long chargeCount);
                if (chargeCount < blockCount)
                {
                    errors.Add($"{color}: {blockCount} blocks but only {chargeCount} charges.");
                }
            }

            return errors;
        }

        private static void ValidateCells(
            LevelDefinition level,
            IReadOnlyList<CellDefinition> cells,
            HashSet<GridPosition> coordinates,
            Dictionary<BlockColorId, long> blocks,
            List<string> errors)
        {
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                CellDefinition cell = cells[cellIndex];
                if (cell == null)
                {
                    errors.Add($"Cell entry {cellIndex + 1} is null.");
                    continue;
                }

                GridPosition position = new(cell.X, cell.Y);
                if (!coordinates.Add(position))
                {
                    errors.Add($"Duplicate cell at {position}.");
                }

                if (cell.X < 0 || cell.X >= level.Width || cell.Y < 0 || cell.Y >= level.Height)
                {
                    errors.Add($"Cell {position} is outside the board.");
                }

                IReadOnlyList<BlockLayerDefinition> layers = cell.Layers;
                if (layers == null)
                {
                    errors.Add($"Cell {position} has a null layer collection.");
                    continue;
                }

                if (layers.Count == 0)
                {
                    errors.Add($"Cell {position} has no layers.");
                    continue;
                }

                for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                {
                    BlockLayerDefinition layer = layers[layerIndex];
                    if (layer == null)
                    {
                        errors.Add($"Cell {position}, layer {layerIndex + 1} is null.");
                        continue;
                    }

                    if (!IsDefinedColor(layer.Color))
                    {
                        errors.Add($"Cell {position}, layer {layerIndex + 1} has invalid color value {(int)layer.Color}.");
                        continue;
                    }

                    Add(blocks, layer.Color, 1L);
                }
            }
        }

        private static void ValidateLanes(
            IReadOnlyList<UnitLaneDefinition> lanes,
            Dictionary<BlockColorId, long> charges,
            List<string> errors)
        {
            for (int laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
            {
                UnitLaneDefinition lane = lanes[laneIndex];
                if (lane == null)
                {
                    errors.Add($"Lane {laneIndex + 1} is null.");
                    continue;
                }

                IReadOnlyList<UnitDefinition> units = lane.Units;
                if (units == null)
                {
                    errors.Add($"Lane {laneIndex + 1} has a null unit collection.");
                    continue;
                }

                for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
                {
                    UnitDefinition unit = units[unitIndex];
                    if (unit == null)
                    {
                        errors.Add($"Lane {laneIndex + 1}, unit {unitIndex + 1} is null.");
                        continue;
                    }

                    if (!IsDefinedColor(unit.Color))
                    {
                        errors.Add(
                            $"Lane {laneIndex + 1}, unit {unitIndex + 1} has invalid color value {(int)unit.Color}.");
                    }

                    if (unit.Charges <= 0)
                    {
                        errors.Add($"Lane {laneIndex + 1}, unit {unitIndex + 1} must have positive charges.");
                    }

                    if (IsDefinedColor(unit.Color) && unit.Charges > 0)
                    {
                        Add(charges, unit.Color, unit.Charges);
                    }
                }
            }
        }

        private static bool IsDefinedColor(BlockColorId color)
        {
            return Enum.IsDefined(typeof(BlockColorId), color);
        }

        private static void Add(Dictionary<BlockColorId, long> counts, BlockColorId color, long amount)
        {
            counts.TryGetValue(color, out long current);
            counts[color] = current + amount;
        }
    }
}
