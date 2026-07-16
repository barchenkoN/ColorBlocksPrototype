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
        private readonly MeshRenderer _bandRenderer;
        private readonly MeshRenderer _bandInsetRenderer;
        private readonly Color _bodyColor;
        private readonly Color _faceColor;
        private readonly Color _bandColor;
        private readonly float _fallOvershoot;
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private Vector3 _exposedPosition;
        private Vector3 _stackOffset;
        private int _animationVersion;

        public BlockView(
            Transform parent,
            BlockNode node,
            Vector3 coveredPosition,
            Vector3 exposedPosition,
            Vector3 scale,
            bool startsCovered,
            VisualTheme theme,
            GameFeelProfile feel)
        {
            Node = node;
            _exposedPosition = new Vector3(exposedPosition.x, exposedPosition.y, 0f);
            _stackOffset = coveredPosition - _exposedPosition;
            _baseScale = scale;
            _bodyColor = ColorPalette.Get(node.Color);
            _faceColor = ColorPalette.GetLight(node.Color);
            _bandColor = ColorPalette.GetDark(node.Color);
            _fallOvershoot = feel.FallBounceHeight;

            GameObject root = new($"Block_{node.Position.X}_{node.Position.Y}_{node.LayerIndex}_{node.Color}");
            root.transform.SetParent(parent, false);
            root.transform.position = coveredPosition;
            root.transform.localScale = scale;
            _transform = root.transform;

            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = ChamferedCubeMesh.Get();
            _bodyRenderer = root.AddComponent<MeshRenderer>();
            ConfigureRenderer(_bodyRenderer, theme.Body(node.Color));

            GameObject contactShadow = CreatePart(
                "ContactShadow",
                root.transform,
                new Vector3(0f, -feel.BlockShadowOffset / Mathf.Max(0.01f, scale.y), 0.53f),
                new Vector3(0.96f, 0.96f, 0.08f),
                theme.Body(node.Color));
            MeshRenderer shadowRenderer = contactShadow.GetComponent<MeshRenderer>();
            SetRendererColor(shadowRenderer, Color.Lerp(_bandColor, Color.black, 0.28f));

            GameObject band = CreatePart(
                "FrontBand",
                root.transform,
                new Vector3(0f, -feel.FrontBandOffset, 0f),
                new Vector3(feel.FrontBandScale.x, feel.FrontBandScale.y, 0.34f),
                theme.Body(node.Color));
            _bandRenderer = band.GetComponent<MeshRenderer>();
            SetRendererColor(_bandRenderer, _bandColor);

            GameObject bandInset = CreatePart(
                "FrontBandInset",
                root.transform,
                new Vector3(0f, -feel.FrontBandOffset, -0.18f),
                new Vector3(feel.FaceScale.x * 0.90f, 0.18f, 0.10f),
                theme.Body(node.Color));
            _bandInsetRenderer = bandInset.GetComponent<MeshRenderer>();
            SetRendererColor(_bandInsetRenderer, Color.Lerp(_bandColor, Color.black, 0.18f));

            GameObject face = CreatePart(
                "FaceInset",
                root.transform,
                new Vector3(0f, 0f, -0.54f),
                new Vector3(feel.FaceScale.x, feel.FaceScale.y, 0.10f),
                theme.Face(node.Color));
            _faceRenderer = face.GetComponent<MeshRenderer>();
            _faceRenderer.gameObject.SetActive(!startsCovered);
        }

        public BlockNode Node { get; }
        public Transform Transform => _transform;
        public Vector3 TargetPosition => _transform.position + new Vector3(0f, 0f, -0.28f);

        /// <summary>
        /// Reference fall is an authored, single-axis ease-out-back: fast descent, one 9-10%
        /// positional overshoot, then a monotonic return. It intentionally has no rigidbody drift,
        /// rotation, or oscillating scale bounce.
        /// </summary>
        public IEnumerator FallTo(Vector3 exposedPosition, float duration, float delay)
        {
            int animationVersion = ++_animationVersion;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
                if (animationVersion != _animationVersion) yield break;
            }

            _exposedPosition = new Vector3(exposedPosition.x, exposedPosition.y, 0f);
            Vector3 target = _exposedPosition + _stackOffset;
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
            _stackOffset = Vector3.zero;
            Vector3 target = _exposedPosition;
            _faceRenderer.gameObject.SetActive(true);
            _transform.localScale = _baseScale * 0.96f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (animationVersion != _animationVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                float eased = EaseOutBack(t, 0.72f);
                _transform.position = Vector3.LerpUnclamped(start, target, eased);
                _transform.localScale = Vector3.LerpUnclamped(_baseScale * 0.96f, _baseScale, t);
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
            filter.sharedMesh = ChamferedCubeMesh.Get();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return part;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void SetRendererColor(MeshRenderer renderer, Color color)
        {
            if (renderer == null || !renderer.gameObject.activeSelf) return;
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
