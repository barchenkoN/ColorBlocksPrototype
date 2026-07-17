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
        private const float PanelCornerRadius = 0.08f;
        private static Mesh _shadowReceiverMesh;
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
            float verticalScale = layout.PlaneVerticalScale;
            // The front surface of BoardInner must meet the far face of every base cube.
            // Keep the calibrated block plane fixed and move the tray pieces as one rigid
            // assembly, rather than leaving a hidden air gap behind one-layer stacks.
            const float authoredBoardInnerCenterDepth = 0.93f;
            const float boardInnerDepth = 0.20f;
            float supportSurfaceDepth = feel.BlockBoardDepth + feel.BlockDepth * 0.5f;
            float boardDepthOffset = supportSurfaceDepth -
                (authoredBoardInnerCenterDepth - boardInnerDepth * 0.5f);
            // Keep every rounded top corner beyond the phone crop. The reference tray
            // continues as straight side rails above the visible header.
            float trayTop = layout.CameraHalfHeight + 2.50f * verticalScale;
            // The reference tray's rounded lower rail extends below the logical gameplay
            // board. Keep the grid/slots anchored to the logical bounds and extend only the
            // rendered tray so its lower silhouette matches the supplied iPhone frame.
            float frameBottom = layout.BoardBottom - 0.25f * verticalScale;
            float frameHeight = trayTop - frameBottom;
            float frameCenterY = (trayTop + frameBottom) * 0.5f;
            float innerBottom = frameBottom +
                ((feel.BoardOuterHeight - feel.BoardInnerHeight) * 0.5f + 0.02f) * verticalScale;
            float innerTop = trayTop - 0.22f * verticalScale;
            float innerHeight = innerTop - innerBottom;

            CreateBox(
                "SceneBackdrop",
                _root.transform,
                new Vector3(0f, -0.45f * verticalScale, 1.62f),
                new Vector3(20f, 30f * verticalScale, 0.18f),
                theme.Backdrop,
                false,
                true);
            float receiverTop = layout.BoardBottom - 0.24f * verticalScale;
            float receiverBottom = -layout.CameraHalfHeight - 2f * verticalScale;
            CreateShadowReceiver(
                _root.transform,
                new Vector3(0f, (receiverTop + receiverBottom) * 0.5f, 0.72f),
                new Vector2(18f, receiverTop - receiverBottom),
                theme.Backdrop);
            // The reference tray continues vertically beyond the top edge of the phone.
            // A single very tall scaled rounded box bends inward at y=0 because its corner
            // radius scales with its height. This shallow extension sits behind the existing
            // frame/inner panel and keeps the two outer rails straight to the screen edge,
            // while the original rounded box still supplies the polished bottom corners.
            CreateBox(
                "BoardTopExtension",
                _root.transform,
                new Vector3(
                    0f,
                    layout.CameraHalfHeight + 0.50f * verticalScale,
                    1.12f + boardDepthOffset),
                new Vector3(feel.BoardOuterWidth, 8.00f * verticalScale, 0.28f),
                theme.Board,
                false,
                true,
                0.001f);
            CreateBox(
                "BoardFrame",
                _root.transform,
                new Vector3(0f, frameCenterY, 1.08f + boardDepthOffset),
                new Vector3(feel.BoardOuterWidth, frameHeight, 0.38f),
                theme.Board,
                true,
                true,
                PanelCornerRadius);
            CreateBox(
                "BoardInner",
                _root.transform,
                new Vector3(
                    0f,
                    (innerTop + innerBottom) * 0.5f,
                    0.93f + boardDepthOffset),
                new Vector3(feel.BoardInnerWidth, innerHeight, 0.20f),
                theme.BoardInner,
                false,
                true,
                PanelCornerRadius);

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
                    Vector3 gridPosition = ToWorld(stack.Position);
                    // Layer definitions are bottom-to-top. Layer zero rests on the board;
                    // every higher cube advances toward the camera by exactly one cube depth.
                    // No cube changes its Z position when the cube above it is removed.
                    Vector3 layerOffset = new(
                        -feel.StackLayerHorizontalStep * layer,
                        -feel.StackLayerStep * verticalScale * layer,
                        feel.BlockBoardDepth - feel.HiddenLayerDepth * layer);
                    BlockView view = new(
                        _root.transform,
                        stack.Layers[layer],
                        gridPosition,
                        layerOffset,
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
            Material material,
            bool castsShadows,
            bool receivesShadows,
            float cornerRadius = RoundedBoxMesh.CornerRadius)
        {
            GameObject box = new(name);
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            MeshFilter filter = box.AddComponent<MeshFilter>();
            filter.sharedMesh = RoundedBoxMesh.Get(cornerRadius);
            MeshRenderer renderer = box.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = receivesShadows;
        }

        private static void CreateShadowReceiver(
            Transform parent,
            Vector3 position,
            Vector2 scale,
            Material material)
        {
            GameObject receiver = new("QueueShadowReceiver");
            receiver.transform.SetParent(parent, false);
            receiver.transform.position = position;
            receiver.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            MeshFilter filter = receiver.AddComponent<MeshFilter>();
            filter.sharedMesh = GetShadowReceiverMesh();
            MeshRenderer renderer = receiver.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Mesh GetShadowReceiverMesh()
        {
            if (_shadowReceiverMesh != null) return _shadowReceiverMesh;

            _shadowReceiverMesh = new Mesh
            {
                name = "Runtime_ShadowReceiverPlane",
                hideFlags = HideFlags.DontSave
            };
            _shadowReceiverMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            };
            _shadowReceiverMesh.normals = new[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };
            _shadowReceiverMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _shadowReceiverMesh.RecalculateBounds();
            _shadowReceiverMesh.UploadMeshData(true);
            return _shadowReceiverMesh;
        }
    }
}
