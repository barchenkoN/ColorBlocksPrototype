using System.Collections;
using ColorBlocks.Core;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    public sealed class BlockView
    {
        private readonly Transform _transform;
        private readonly Vector3 _baseScale;
        private readonly MeshRenderer _bodyRenderer;
        private readonly MeshRenderer _faceRenderer;
        private readonly Color _faceColor;
        private readonly float _fallOvershoot;
        private readonly Vector3 _layerOffset;
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private Vector3 _gridPosition;
        private int _animationVersion;

        public BlockView(
            Transform parent,
            BlockNode node,
            Vector3 gridPosition,
            Vector3 layerOffset,
            Vector3 scale,
            bool startsCovered,
            VisualTheme theme,
            GameFeelProfile feel)
        {
            Node = node;
            _gridPosition = gridPosition;
            _layerOffset = layerOffset;
            _baseScale = scale;
            Color bodyColor = ColorPalette.Get(node.Color);
            _faceColor = Color.Lerp(bodyColor, ColorPalette.GetDark(node.Color), 0.22f);
            _fallOvershoot = feel.FallBounceHeight * feel.CameraPlaneVerticalScale;

            GameObject root = new($"Block_{node.Position.X}_{node.Position.Y}_{node.LayerIndex}_{node.Color}");
            root.transform.SetParent(parent, false);
            root.transform.position = _gridPosition + _layerOffset;
            root.transform.localScale = scale;
            _transform = root.transform;

            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = RoundedBoxMesh.Get();
            _bodyRenderer = root.AddComponent<MeshRenderer>();
            ConfigureRenderer(_bodyRenderer, theme.Body(node.Color), true);

            GameObject face = CreatePart(
                "FaceInset",
                root.transform,
                new Vector3(0f, 0.035f, -0.46f),
                new Vector3(feel.FaceScale.x, feel.FaceScale.y, 0.10f),
                theme.Face(node.Color));
            _faceRenderer = face.GetComponent<MeshRenderer>();
            SetRendererColor(_faceRenderer, _faceColor);
            // The top inset sits on the camera-facing Z surface. For a covered cube that
            // surface is the exact contact plane with the cube above, so rendering it would
            // place decorative geometry inside the upper cube. The rounded Y side remains
            // visible as the lower physical band until this layer is exposed.
            _faceRenderer.gameObject.SetActive(!startsCovered);

            // A shallow inset on the camera-facing Y side is the molded detail visible on
            // every physical depth band in the reference. Unlike the top inset, this side
            // remains exposed when another cube is stacked directly above along Z.
            GameObject sideFace = CreatePart(
                "SideInset",
                root.transform,
                new Vector3(0f, -0.505f, 0f),
                new Vector3(feel.FaceScale.x, feel.FaceScale.y * 0.74f, 0.055f),
                theme.WhiteUnlit);
            sideFace.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            SetRendererColor(
                sideFace.GetComponent<MeshRenderer>(),
                Color.Lerp(ColorPalette.GetDark(node.Color), Color.black, 0.20f));
        }

        public BlockNode Node { get; }
        public Transform Transform => _transform;
        public Vector3 TargetPosition => _transform.TransformPoint(new Vector3(0f, 0f, -0.62f));

        /// <summary>
        /// Reference fall is an authored, single-axis ease-out-back: fast descent, one 9-10%
        /// positional overshoot, then a monotonic return. It intentionally has no rigidbody drift,
        /// rotation, or oscillating scale bounce.
        /// </summary>
        public IEnumerator FallTo(Vector3 gridPosition, float duration, float delay)
        {
            int animationVersion = ++_animationVersion;
            // Commit the logical destination before the authored fall delay. A projectile can
            // destroy the exposed cube while its stack is waiting to fall; Reveal then cancels
            // this animation and must still settle the newly exposed cube at the stack's new
            // cell instead of restoring the old (now unsupported) grid position.
            _gridPosition = gridPosition;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
                if (animationVersion != _animationVersion) yield break;
            }

            Vector3 target = _gridPosition + _layerOffset;
            Vector3 start = _transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (animationVersion != _animationVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                _transform.position = GameFeelMotion.EvaluateFall(start, target, _fallOvershoot, t);
                _transform.localScale = _baseScale;
                yield return null;
            }

            _transform.position = target;
            _transform.localScale = _baseScale;
        }

        public IEnumerator Reveal(float duration)
        {
            int animationVersion = ++_animationVersion;
            Vector3 start = _transform.position;
            Vector3 target = _gridPosition + _layerOffset;
            SetRendererColor(_faceRenderer, _faceColor);
            _faceRenderer.gameObject.SetActive(true);
            Vector3 revealStartScale = new(
                _baseScale.x * 0.98f,
                _baseScale.y * 0.98f,
                _baseScale.z);
            _transform.localScale = revealStartScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (animationVersion != _animationVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                float eased = EaseOutBack(t, 0.45f);
                _transform.position = Vector3.LerpUnclamped(start, target, eased);
                _transform.localScale = Vector3.LerpUnclamped(revealStartScale, _baseScale, t);
                yield return null;
            }

            _transform.position = target;
            _transform.localScale = _baseScale;
        }

        public void HideDestroyed()
        {
            ++_animationVersion;
            _transform.gameObject.SetActive(false);
        }

        private static float EaseOutBack(float t, float overshoot)
        {
            float shifted = t - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        private static GameObject CreatePart(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = new(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = RoundedBoxMesh.Get();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, false);
            return part;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, Material material, bool castsShadow)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private void SetRendererColor(MeshRenderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

    }
}
