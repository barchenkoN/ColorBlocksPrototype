using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlocks.Core
{
    [Serializable]
    public sealed class BlockLayerDefinition
    {
        [SerializeField] private BlockColorId color;

        public BlockColorId Color => color;

        public BlockLayerDefinition(BlockColorId color)
        {
            this.color = color;
        }
    }

    [Serializable]
    public sealed class CellDefinition
    {
        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private List<BlockLayerDefinition> layers = new();

        public int X => x;
        public int Y => y;
        public IReadOnlyList<BlockLayerDefinition> Layers => layers;

        public CellDefinition(int x, int y, IEnumerable<BlockColorId> colorsBottomToTop)
        {
            this.x = x;
            this.y = y;
            layers = new List<BlockLayerDefinition>();
            foreach (BlockColorId color in colorsBottomToTop)
            {
                layers.Add(new BlockLayerDefinition(color));
            }
        }
    }

    [Serializable]
    public sealed class UnitDefinition
    {
        [SerializeField] private BlockColorId color;
        [Min(1)] [SerializeField] private int charges = 1;

        public BlockColorId Color => color;
        public int Charges => charges;

        public UnitDefinition(BlockColorId color, int charges)
        {
            this.color = color;
            this.charges = charges;
        }
    }

    [Serializable]
    public sealed class UnitLaneDefinition
    {
        [SerializeField] private List<UnitDefinition> units = new();

        public IReadOnlyList<UnitDefinition> Units => units;

        public UnitLaneDefinition(IEnumerable<UnitDefinition> units)
        {
            this.units = new List<UnitDefinition>(units);
        }
    }

    [CreateAssetMenu(fileName = "Level", menuName = "Color Blocks/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;
        [SerializeField] private List<CellDefinition> cells = new();
        [SerializeField] private List<UnitLaneDefinition> lanes = new();

        public int LevelNumber => levelNumber;
        public int Width => width;
        public int Height => height;
        public IReadOnlyList<CellDefinition> Cells => cells;
        public IReadOnlyList<UnitLaneDefinition> Lanes => lanes;

#if UNITY_EDITOR
        public void Configure(
            int number,
            int boardWidth,
            int boardHeight,
            IEnumerable<CellDefinition> boardCells,
            IEnumerable<UnitLaneDefinition> unitLanes)
        {
            levelNumber = number;
            width = boardWidth;
            height = boardHeight;
            cells = new List<CellDefinition>(boardCells);
            lanes = new List<UnitLaneDefinition>(unitLanes);
        }
#endif
    }

}
