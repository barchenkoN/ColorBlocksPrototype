using System;
using System.Collections.Generic;
using System.Text;

namespace ColorBlocks.Core
{
    public enum LevelSimulationStatus
    {
        AwaitingChoice,
        Won,
        Lost,
        InvalidChoice
    }

    public readonly struct SolverSlotSnapshot
    {
        internal SolverSlotSnapshot(BlockColorId color, int charges)
        {
            IsOccupied = true;
            Color = color;
            Charges = charges;
        }

        public bool IsOccupied { get; }
        public BlockColorId Color { get; }
        public int Charges { get; }
    }

    public sealed class LevelSimulationResult
    {
        internal LevelSimulationResult(
            LevelSimulationStatus status,
            int remainingBlockCount,
            SolverSlotSnapshot[] slots,
            int[] laneFrontIndices,
            string error)
        {
            Status = status;
            RemainingBlockCount = remainingBlockCount;
            Slots = Array.AsReadOnly(slots);
            LaneFrontIndices = Array.AsReadOnly(laneFrontIndices);
            Error = error;
        }

        public LevelSimulationStatus Status { get; }
        public int RemainingBlockCount { get; }
        public IReadOnlyList<SolverSlotSnapshot> Slots { get; }
        public IReadOnlyList<int> LaneFrontIndices { get; }
        public string Error { get; }
    }

    public sealed class LevelSolveResult
    {
        internal LevelSolveResult(
            bool isSolvable,
            bool searchLimitReached,
            int exploredStateCount,
            int[] winningLaneChoices,
            bool hasLosingPath,
            int[] firstLosingLaneChoices)
        {
            IsSolvable = isSolvable;
            SearchLimitReached = searchLimitReached;
            ExploredStateCount = exploredStateCount;
            WinningLaneChoices = Array.AsReadOnly(winningLaneChoices);
            HasLosingPath = hasLosingPath;
            FirstLosingLaneChoices = Array.AsReadOnly(firstLosingLaneChoices);
        }

        public bool IsSolvable { get; }
        public bool SearchLimitReached { get; }
        public int ExploredStateCount { get; }

        /// <summary>One-based lane numbers for a proven winning selection sequence.</summary>
        public IReadOnlyList<int> WinningLaneChoices { get; }

        public bool HasLosingPath { get; }

        /// <summary>One-based lane numbers for the first terminal losing sequence seen during search.</summary>
        public IReadOnlyList<int> FirstLosingLaneChoices { get; }
    }

    /// <summary>
    /// Pure deterministic level solver. Each column is represented bottom-to-top; when a stack is
    /// cleared, the next stack becomes the bottom frontier, which is equivalent to runtime gravity
    /// compaction without simulating presentation coordinates or animation.
    /// </summary>
    public static class LevelSolver
    {
        // Five queue columns align with the five active slots in the supplied gameplay reference.
        public const int LaneCount = 5;
        public const int SlotCount = 5;
        public const int DefaultMaximumStates = 250000;

        public static LevelSolveResult Solve(
            LevelDefinition level,
            int maximumStates = DefaultMaximumStates)
        {
            if (maximumStates <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumStates), "State limit must be positive.");
            }

            Rules rules = CreateValidatedRules(level);
            Search search = new(rules, maximumStates);
            return search.Run();
        }

        /// <summary>
        /// Replays one-based lane choices under the same deterministic combat phases used by Solve.
        /// This is intended for authoring diagnostics and regression tests.
        /// </summary>
        public static LevelSimulationResult Simulate(
            LevelDefinition level,
            IReadOnlyList<int> oneBasedLaneChoices)
        {
            if (oneBasedLaneChoices == null)
            {
                throw new ArgumentNullException(nameof(oneBasedLaneChoices));
            }

            Rules rules = CreateValidatedRules(level);
            SolverState state = rules.CreateInitialState();
            rules.StabilizeCombat(state);

            for (int choiceIndex = 0; choiceIndex < oneBasedLaneChoices.Count; choiceIndex++)
            {
                int oneBasedLane = oneBasedLaneChoices[choiceIndex];
                if (state.RemainingBlockCount == 0)
                {
                    return rules.CreateSimulationResult(
                        state,
                        LevelSimulationStatus.InvalidChoice,
                        $"Choice {choiceIndex + 1} cannot be applied because the level is already won.");
                }

                int laneIndex = oneBasedLane - 1;
                if (laneIndex < 0 || laneIndex >= LaneCount)
                {
                    return rules.CreateSimulationResult(
                        state,
                        LevelSimulationStatus.InvalidChoice,
                        $"Choice {choiceIndex + 1} uses lane {oneBasedLane}; valid lane numbers are 1-{LaneCount}.");
                }

                int freeSlot = state.FindLeftmostFreeSlot();
                if (freeSlot < 0)
                {
                    return rules.CreateSimulationResult(
                        state,
                        LevelSimulationStatus.InvalidChoice,
                        $"Choice {choiceIndex + 1} cannot be applied because all {SlotCount} slots are occupied.");
                }

                if (!rules.HasFrontUnit(state, laneIndex))
                {
                    return rules.CreateSimulationResult(
                        state,
                        LevelSimulationStatus.InvalidChoice,
                        $"Choice {choiceIndex + 1} cannot be applied because lane {oneBasedLane} is empty.");
                }

                rules.SelectFrontUnitAndStabilize(state, laneIndex, freeSlot);
            }

            return rules.CreateSimulationResult(state, rules.GetStatus(state), null);
        }

        private static Rules CreateValidatedRules(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            List<string> errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    "Cannot solve an invalid level:\n" + string.Join("\n", errors),
                    nameof(level));
            }

            return new Rules(level);
        }

        private readonly struct UnitState
        {
            public UnitState(BlockColorId color, int charges)
            {
                Color = color;
                Charges = charges;
            }

            public BlockColorId Color { get; }
            public int Charges { get; }

            public UnitState SpendCharge()
            {
                return new UnitState(Color, Charges - 1);
            }
        }

        private sealed class CellRules
        {
            public CellRules(CellDefinition definition)
            {
                X = definition.X;
                Y = definition.Y;
                LayersBottomToTop = new BlockColorId[definition.Layers.Count];
                for (int i = 0; i < definition.Layers.Count; i++)
                {
                    LayersBottomToTop[i] = definition.Layers[i].Color;
                }
            }

            public int X { get; }
            public int Y { get; }
            public BlockColorId[] LayersBottomToTop { get; }
        }

        private sealed class ColumnRules
        {
            public ColumnRules(int x, int[] cellIndicesLowestFirst)
            {
                X = x;
                CellIndicesLowestFirst = cellIndicesLowestFirst;
            }

            public int X { get; }
            public int[] CellIndicesLowestFirst { get; }
        }

        private sealed class SolverState
        {
            public SolverState(int cellCount)
            {
                RemainingLayers = new int[cellCount];
                LaneFrontIndices = new int[LaneCount];
                Slots = new UnitState?[SlotCount];
            }

            private SolverState(
                int[] remainingLayers,
                int remainingBlockCount,
                int[] laneFrontIndices,
                UnitState?[] slots)
            {
                RemainingLayers = remainingLayers;
                RemainingBlockCount = remainingBlockCount;
                LaneFrontIndices = laneFrontIndices;
                Slots = slots;
            }

            public int[] RemainingLayers { get; }
            public int RemainingBlockCount { get; set; }
            public int[] LaneFrontIndices { get; }
            public UnitState?[] Slots { get; }

            public SolverState Clone()
            {
                return new SolverState(
                    (int[])RemainingLayers.Clone(),
                    RemainingBlockCount,
                    (int[])LaneFrontIndices.Clone(),
                    (UnitState?[])Slots.Clone());
            }

            public int FindLeftmostFreeSlot()
            {
                for (int slotIndex = 0; slotIndex < Slots.Length; slotIndex++)
                {
                    if (!Slots[slotIndex].HasValue)
                    {
                        return slotIndex;
                    }
                }

                return -1;
            }
        }

        private sealed class Rules
        {
            private readonly int _width;
            private readonly CellRules[] _cells;
            private readonly ColumnRules[] _columns;
            private readonly UnitState[][] _lanes;

            public Rules(LevelDefinition level)
            {
                _width = level.Width;
                _cells = new CellRules[level.Cells.Count];
                Dictionary<int, List<int>> columnCells = new();

                for (int cellIndex = 0; cellIndex < level.Cells.Count; cellIndex++)
                {
                    CellRules cell = new(level.Cells[cellIndex]);
                    _cells[cellIndex] = cell;
                    if (!columnCells.TryGetValue(cell.X, out List<int> indices))
                    {
                        indices = new List<int>();
                        columnCells.Add(cell.X, indices);
                    }

                    indices.Add(cellIndex);
                }

                List<ColumnRules> columns = new(columnCells.Count);
                foreach ((int x, List<int> indices) in columnCells)
                {
                    indices.Sort((left, right) =>
                    {
                        int yComparison = _cells[left].Y.CompareTo(_cells[right].Y);
                        return yComparison != 0 ? yComparison : left.CompareTo(right);
                    });
                    columns.Add(new ColumnRules(x, indices.ToArray()));
                }

                columns.Sort((left, right) => left.X.CompareTo(right.X));
                _columns = columns.ToArray();

                _lanes = new UnitState[LaneCount][];
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    IReadOnlyList<UnitDefinition> definitions = level.Lanes[laneIndex].Units;
                    UnitState[] units = new UnitState[definitions.Count];
                    for (int unitIndex = 0; unitIndex < definitions.Count; unitIndex++)
                    {
                        units[unitIndex] = new UnitState(
                            definitions[unitIndex].Color,
                            definitions[unitIndex].Charges);
                    }

                    _lanes[laneIndex] = units;
                }
            }

            public SolverState CreateInitialState()
            {
                SolverState state = new(_cells.Length);
                for (int cellIndex = 0; cellIndex < _cells.Length; cellIndex++)
                {
                    int layerCount = _cells[cellIndex].LayersBottomToTop.Length;
                    state.RemainingLayers[cellIndex] = layerCount;
                    state.RemainingBlockCount += layerCount;
                }

                return state;
            }

            public bool HasFrontUnit(SolverState state, int laneIndex)
            {
                return state.LaneFrontIndices[laneIndex] < _lanes[laneIndex].Length;
            }

            public void SelectFrontUnitAndStabilize(SolverState state, int laneIndex, int freeSlot)
            {
                int unitIndex = state.LaneFrontIndices[laneIndex];
                state.LaneFrontIndices[laneIndex] = unitIndex + 1;
                state.Slots[freeSlot] = _lanes[laneIndex][unitIndex];
                StabilizeCombat(state);
            }

            public void StabilizeCombat(SolverState state)
            {
                int[] targetsBySlot = new int[SlotCount];
                bool[] reservedCells = new bool[_cells.Length];

                while (state.RemainingBlockCount > 0)
                {
                    Array.Fill(targetsBySlot, -1);
                    Array.Clear(reservedCells, 0, reservedCells.Length);
                    bool anyUnitFired = false;

                    for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
                    {
                        UnitState? optionalUnit = state.Slots[slotIndex];
                        if (!optionalUnit.HasValue)
                        {
                            continue;
                        }

                        UnitState unit = optionalUnit.Value;
                        if (unit.Charges <= 0)
                        {
                            state.Slots[slotIndex] = null;
                            continue;
                        }

                        int targetCell = FindTargetCell(
                            state,
                            reservedCells,
                            unit.Color,
                            slotIndex);
                        if (targetCell < 0)
                        {
                            continue;
                        }

                        reservedCells[targetCell] = true;
                        targetsBySlot[slotIndex] = targetCell;
                        state.Slots[slotIndex] = unit.SpendCharge();
                        anyUnitFired = true;
                    }

                    if (!anyUnitFired)
                    {
                        return;
                    }

                    for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
                    {
                        int targetCell = targetsBySlot[slotIndex];
                        if (targetCell >= 0)
                        {
                            state.RemainingLayers[targetCell]--;
                            state.RemainingBlockCount--;
                        }
                    }

                    for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
                    {
                        UnitState? unit = state.Slots[slotIndex];
                        if (unit.HasValue && unit.Value.Charges == 0)
                        {
                            state.Slots[slotIndex] = null;
                        }
                    }
                }
            }

            public LevelSimulationStatus GetStatus(SolverState state)
            {
                if (state.RemainingBlockCount == 0)
                {
                    return LevelSimulationStatus.Won;
                }

                if (state.FindLeftmostFreeSlot() < 0 || !HasAnyFrontUnit(state))
                {
                    return LevelSimulationStatus.Lost;
                }

                return LevelSimulationStatus.AwaitingChoice;
            }

            public LevelSimulationResult CreateSimulationResult(
                SolverState state,
                LevelSimulationStatus status,
                string error)
            {
                SolverSlotSnapshot[] slots = new SolverSlotSnapshot[SlotCount];
                for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
                {
                    UnitState? unit = state.Slots[slotIndex];
                    if (unit.HasValue)
                    {
                        slots[slotIndex] = new SolverSlotSnapshot(unit.Value.Color, unit.Value.Charges);
                    }
                }

                return new LevelSimulationResult(
                    status,
                    state.RemainingBlockCount,
                    slots,
                    (int[])state.LaneFrontIndices.Clone(),
                    error);
            }

            public string CreateCanonicalKey(SolverState state)
            {
                StringBuilder key = new(64 + state.RemainingLayers.Length * 3);
                for (int cellIndex = 0; cellIndex < state.RemainingLayers.Length; cellIndex++)
                {
                    key.Append(state.RemainingLayers[cellIndex]).Append(',');
                }

                key.Append('|');
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    key.Append(state.LaneFrontIndices[laneIndex]).Append(',');
                }

                key.Append('|');
                for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
                {
                    UnitState? unit = state.Slots[slotIndex];
                    if (!unit.HasValue)
                    {
                        key.Append('-');
                    }
                    else
                    {
                        key.Append((int)unit.Value.Color)
                            .Append(':')
                            .Append(unit.Value.Charges);
                    }

                    key.Append(',');
                }

                return key.ToString();
            }

            private int FindTargetCell(
                SolverState state,
                bool[] reservedCells,
                BlockColorId color,
                int slotIndex)
            {
                int bestCell = -1;
                long bestScaledDistance = long.MaxValue;
                int bestX = int.MaxValue;
                long scaledSlotX = (long)(_width - 1) * slotIndex;

                for (int columnIndex = 0; columnIndex < _columns.Length; columnIndex++)
                {
                    ColumnRules column = _columns[columnIndex];
                    int frontierCell = FindFrontierCell(state, column);
                    if (frontierCell < 0 || reservedCells[frontierCell])
                    {
                        continue;
                    }

                    int remainingLayerCount = state.RemainingLayers[frontierCell];
                    BlockColorId exposedColor =
                        _cells[frontierCell].LayersBottomToTop[remainingLayerCount - 1];
                    if (exposedColor != color)
                    {
                        continue;
                    }

                    long scaledDistance = Math.Abs((long)column.X * 4L - scaledSlotX);
                    if (scaledDistance < bestScaledDistance ||
                        (scaledDistance == bestScaledDistance && column.X < bestX))
                    {
                        bestCell = frontierCell;
                        bestScaledDistance = scaledDistance;
                        bestX = column.X;
                    }
                }

                return bestCell;
            }

            private static int FindFrontierCell(SolverState state, ColumnRules column)
            {
                for (int index = 0; index < column.CellIndicesLowestFirst.Length; index++)
                {
                    int cellIndex = column.CellIndicesLowestFirst[index];
                    if (state.RemainingLayers[cellIndex] > 0)
                    {
                        return cellIndex;
                    }
                }

                return -1;
            }

            private bool HasAnyFrontUnit(SolverState state)
            {
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (HasFrontUnit(state, laneIndex))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class Search
        {
            private readonly Rules _rules;
            private readonly int _maximumStates;
            private readonly HashSet<string> _visited = new();
            private int[] _winningPath = Array.Empty<int>();
            private int[] _firstLosingPath = Array.Empty<int>();
            private bool _hasLosingPath;

            public Search(Rules rules, int maximumStates)
            {
                _rules = rules;
                _maximumStates = maximumStates;
            }

            public bool SearchLimitReached { get; private set; }
            public int ExploredStateCount { get; private set; }

            public LevelSolveResult Run()
            {
                SolverState initial = _rules.CreateInitialState();
                _rules.StabilizeCombat(initial);
                List<int> path = new();
                bool isSolvable = SearchState(initial, path);
                return new LevelSolveResult(
                    isSolvable,
                    SearchLimitReached,
                    ExploredStateCount,
                    _winningPath,
                    _hasLosingPath,
                    _firstLosingPath);
            }

            private bool SearchState(SolverState state, List<int> path)
            {
                if (state.RemainingBlockCount == 0)
                {
                    _winningPath = path.ToArray();
                    return true;
                }

                string key = _rules.CreateCanonicalKey(state);
                if (_visited.Contains(key))
                {
                    return false;
                }

                if (ExploredStateCount >= _maximumStates)
                {
                    SearchLimitReached = true;
                    return false;
                }

                _visited.Add(key);
                ExploredStateCount++;

                int freeSlot = state.FindLeftmostFreeSlot();
                bool hasLegalChoice = false;
                if (freeSlot >= 0)
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (!_rules.HasFrontUnit(state, laneIndex))
                        {
                            continue;
                        }

                        hasLegalChoice = true;
                        SolverState child = state.Clone();
                        _rules.SelectFrontUnitAndStabilize(child, laneIndex, freeSlot);
                        path.Add(laneIndex + 1);
                        bool childWins = SearchState(child, path);
                        path.RemoveAt(path.Count - 1);

                        if (childWins)
                        {
                            return true;
                        }

                        if (SearchLimitReached)
                        {
                            return false;
                        }
                    }
                }

                if (!hasLegalChoice && !_hasLosingPath)
                {
                    _hasLosingPath = true;
                    _firstLosingPath = path.ToArray();
                }

                return false;
            }
        }
    }
}
