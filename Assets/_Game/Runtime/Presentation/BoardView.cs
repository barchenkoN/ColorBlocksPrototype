using System.Collections.Generic;
using ColorBlocks.Core;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    /// <summary>
    /// Pure presentation mapping for the board. Logical cells remain integer grid positions,
    /// while the measured reference layout deliberately uses different horizontal/vertical pitches.
    /// </summary>
    public sealed class BoardView
    {
        private readonly Dictionary<BlockNode, BlockView> _blocks = new();
        private readonly GameObject _root;
        private readonly GameplayLayout _layout;

        public BoardView(
            BoardModel model,
            VisualTheme theme,
            GameFeelProfile feel,
            GameplayLayout layout)
        {
            _layout = layout;
            _root = new GameObject("BoardView");

            CreateBox(
                "BoardShadow",
                _root.transform,
                new Vector3(0f, layout.BoardCenterY - 0.10f, 0.64f),
                new Vector3(feel.BoardOuterWidth + 0.20f, feel.BoardOuterHeight + 0.22f, 0.30f),
                theme.Board);
            CreateBox(
                "BoardFrame",
                _root.transform,
                new Vector3(0f, layout.BoardCenterY, 0.48f),
                new Vector3(feel.BoardOuterWidth, feel.BoardOuterHeight, 0.34f),
                theme.Board);
            CreateBox(
                "BoardInner",
                _root.transform,
                new Vector3(0f, layout.BoardCenterY + 0.02f, 0.28f),
                new Vector3(feel.BoardInnerWidth, feel.BoardInnerHeight, 0.16f),
                theme.BoardInner);

            Vector3 blockScale = new(
                layout.CellPitchX * feel.BlockFill.x,
                layout.CellPitchY * feel.BlockFill.y,
                feel.BlockDepth);

            for (int stackIndex = 0; stackIndex < model.Stacks.Count; stackIndex++)
            {
                BlockStackModel stack = model.Stacks[stackIndex];
                int topIndex = stack.Layers.Count - 1;
                for (int layer = 0; layer < stack.Layers.Count; layer++)
                {
                    int coveredDepth = topIndex - layer;
                    Vector3 exposedPosition = ToWorld(stack.Position);
                    Vector3 coveredPosition = exposedPosition + new Vector3(
                        0f,
                        -feel.StackLayerStep * coveredDepth,
                        feel.HiddenLayerDepth * coveredDepth);
                    BlockView view = new(
                        _root.transform,
                        stack.Layers[layer],
                        coveredPosition,
                        exposedPosition,
                        blockScale,
                        coveredDepth > 0,
                        theme,
                        feel);
                    _blocks.Add(stack.Layers[layer], view);
                }
            }
        }

        public GameObject Root => _root;
        public float CellPitchX => _layout.CellPitchX;
        public float CellPitchY => _layout.CellPitchY;

        public Vector3 ToWorld(GridPosition position)
        {
            return _layout.GridOrigin + new Vector3(
                position.X * _layout.CellPitchX,
                position.Y * _layout.CellPitchY,
                0f);
        }

        public BlockView Get(BlockNode node) => _blocks[node];

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private static void CreateBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject box = new(name);
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            MeshFilter filter = box.AddComponent<MeshFilter>();
            filter.sharedMesh = ChamferedCubeMesh.Get();
            MeshRenderer renderer = box.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
