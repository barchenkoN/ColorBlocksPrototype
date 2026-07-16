using System;
using System.Collections.Generic;

namespace ColorBlocks.Core
{
    public sealed class BlockNode
    {
        internal BlockNode(BlockStackModel stack, int layerIndex, BlockColorId color)
        {
            Stack = stack;
            LayerIndex = layerIndex;
            Color = color;
        }

        internal BlockStackModel Stack { get; }
        public GridPosition Position => Stack.Position;
        public int LayerIndex { get; }
        public BlockColorId Color { get; }
        public bool IsDestroyed { get; internal set; }
        public bool IsReserved { get; internal set; }
    }

    public sealed class BlockStackModel
    {
        private readonly List<BlockNode> _layers;

        internal BlockStackModel(CellDefinition definition)
        {
            InitialPosition = new GridPosition(definition.X, definition.Y);
            Position = InitialPosition;
            _layers = new List<BlockNode>(definition.Layers.Count);

            for (int i = 0; i < definition.Layers.Count; i++)
            {
                _layers.Add(new BlockNode(this, i, definition.Layers[i].Color));
            }
        }

        public GridPosition InitialPosition { get; }
        public GridPosition Position { get; internal set; }
        public IReadOnlyList<BlockNode> Layers => _layers;

        public BlockNode Exposed
        {
            get
            {
                for (int i = _layers.Count - 1; i >= 0; i--)
                {
                    if (!_layers[i].IsDestroyed)
                    {
                        return _layers[i];
                    }
                }

                return null;
            }
        }
    }

    public readonly struct TargetReservation
    {
        internal TargetReservation(BlockNode node)
        {
            Node = node;
        }

        internal BlockNode Node { get; }
        public BlockNode Target => Node;
        public bool IsValid => Node != null;
        public GridPosition Position => Node.Position;
        public int LayerIndex => Node.LayerIndex;
        public BlockColorId Color => Node.Color;
    }

    public readonly struct BlockStackFall
    {
        internal BlockStackFall(BlockStackModel stack, GridPosition from, GridPosition to)
        {
            Stack = stack;
            From = from;
            To = to;
        }

        public BlockStackModel Stack { get; }
        public GridPosition From { get; }
        public GridPosition To { get; }
        public int Distance => From.Y - To.Y;
    }

    public readonly struct BoardMutation
    {
        internal BoardMutation(BlockNode revealedNode, IReadOnlyList<BlockStackFall> falls)
        {
            RevealedNode = revealedNode;
            Falls = falls ?? Array.Empty<BlockStackFall>();
        }

        public BlockNode RevealedNode { get; }
        public IReadOnlyList<BlockStackFall> Falls { get; }
    }

    public sealed class BoardModel
    {
        private readonly List<BlockStackModel> _orderedStacks = new();
        private readonly Dictionary<int, List<BlockStackModel>> _columns = new();
        private readonly List<List<BlockStackModel>> _orderedColumns = new();

        public BoardModel(LevelDefinition level)
        {
            Width = level.Width;
            Height = level.Height;

            for (int i = 0; i < level.Cells.Count; i++)
            {
                BlockStackModel stack = new(level.Cells[i]);
                _orderedStacks.Add(stack);
                if (!_columns.TryGetValue(stack.Position.X, out List<BlockStackModel> column))
                {
                    column = new List<BlockStackModel>();
                    _columns.Add(stack.Position.X, column);
                    _orderedColumns.Add(column);
                }

                column.Add(stack);
                RemainingBlocks += stack.Layers.Count;
            }

            _orderedColumns.Sort((left, right) => left[0].Position.X.CompareTo(right[0].Position.X));
            for (int i = 0; i < _orderedColumns.Count; i++)
            {
                List<BlockStackModel> column = _orderedColumns[i];
                column.Sort((left, right) =>
                {
                    int yComparison = left.InitialPosition.Y.CompareTo(right.InitialPosition.Y);
                    return yComparison != 0
                        ? yComparison
                        : left.InitialPosition.X.CompareTo(right.InitialPosition.X);
                });
                CompactColumn(column, null);
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int RemainingBlocks { get; private set; }
        public IReadOnlyList<BlockStackModel> Stacks => _orderedStacks;

        public bool HasAvailableTarget(BlockColorId color)
        {
            for (int i = 0; i < _orderedColumns.Count; i++)
            {
                BlockNode node = FindFrontierNode(_orderedColumns[i]);
                if (node != null && !node.IsReserved && node.Color == color)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryReserveTarget(BlockColorId color, float slotBoardX, out TargetReservation reservation)
        {
            BlockNode best = null;
            float bestHorizontalDistance = float.MaxValue;

            for (int i = 0; i < _orderedColumns.Count; i++)
            {
                BlockNode candidate = FindFrontierNode(_orderedColumns[i]);
                if (candidate == null || candidate.IsReserved || candidate.Color != color)
                {
                    continue;
                }

                if (best == null || IsBetterTarget(candidate, best, slotBoardX, bestHorizontalDistance))
                {
                    best = candidate;
                    bestHorizontalDistance = Math.Abs(candidate.Position.X - slotBoardX);
                }
            }

            if (best == null)
            {
                reservation = default;
                return false;
            }

            best.IsReserved = true;
            reservation = new TargetReservation(best);
            return true;
        }

        public BoardMutation Destroy(TargetReservation reservation)
        {
            if (!reservation.IsValid ||
                reservation.Node.IsDestroyed ||
                !reservation.Node.IsReserved ||
                !IsFrontierNode(reservation.Node))
            {
                throw new InvalidOperationException("Attempted to destroy an invalid or stale target reservation.");
            }

            BlockNode node = reservation.Node;
            BlockStackModel stack = node.Stack;
            node.IsReserved = false;
            node.IsDestroyed = true;
            RemainingBlocks--;

            BlockNode revealedNode = stack.Exposed;
            if (revealedNode != null)
            {
                return new BoardMutation(revealedNode, Array.Empty<BlockStackFall>());
            }

            List<BlockStackModel> column = _columns[stack.Position.X];
            int removedIndex = column.IndexOf(stack);
            if (removedIndex < 0)
            {
                throw new InvalidOperationException("Destroyed stack was not present in its runtime column.");
            }

            column.RemoveAt(removedIndex);
            List<BlockStackFall> falls = new(Math.Max(0, column.Count - removedIndex));
            CompactColumn(column, falls);
            return new BoardMutation(null, falls);
        }

        public void Release(TargetReservation reservation)
        {
            if (reservation.IsValid && !reservation.Node.IsDestroyed)
            {
                reservation.Node.IsReserved = false;
            }
        }

        private static void CompactColumn(List<BlockStackModel> column, List<BlockStackFall> falls)
        {
            for (int index = 0; index < column.Count; index++)
            {
                BlockStackModel stack = column[index];
                GridPosition destination = new(stack.Position.X, index);
                if (stack.Position.Equals(destination))
                {
                    continue;
                }

                GridPosition origin = stack.Position;
                stack.Position = destination;
                falls?.Add(new BlockStackFall(stack, origin, destination));
            }
        }

        private static BlockNode FindFrontierNode(List<BlockStackModel> column)
        {
            return column.Count > 0 ? column[0].Exposed : null;
        }

        private bool IsFrontierNode(BlockNode node)
        {
            return _columns.TryGetValue(node.Position.X, out List<BlockStackModel> column) &&
                   ReferenceEquals(FindFrontierNode(column), node);
        }

        private static bool IsBetterTarget(
            BlockNode candidate,
            BlockNode current,
            float slotBoardX,
            float currentHorizontalDistance)
        {
            float candidateDistance = Math.Abs(candidate.Position.X - slotBoardX);
            if (Math.Abs(candidateDistance - currentHorizontalDistance) > 0.001f)
            {
                return candidateDistance < currentHorizontalDistance;
            }

            return candidate.Position.X < current.Position.X;
        }
    }
}
