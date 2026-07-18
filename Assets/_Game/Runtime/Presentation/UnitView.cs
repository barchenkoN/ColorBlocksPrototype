using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlocks.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorBlocks.Presentation
{
    public sealed class UnitClickTarget : MonoBehaviour
    {
        public Action Clicked { get; set; }
        public void NotifyClick() => Clicked?.Invoke();
    }

    public sealed class CameraFacingLabel : MonoBehaviour
    {
        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) transform.rotation = _camera.transform.rotation;
        }
    }

    public sealed class UnitView
    {
        private readonly struct TintTarget
        {
            public TintTarget(MeshRenderer renderer, Color baseColor, bool castsShadow)
            {
                Renderer = renderer;
                BaseColor = baseColor;
                CastsShadow = castsShadow;
            }

            public MeshRenderer Renderer { get; }
            public Color BaseColor { get; }
            public bool CastsShadow { get; }
        }

        private readonly GameObject _root;
        private readonly TextMeshPro _counter;
        private readonly Transform _turretPivot;
        private readonly Transform _barrel;
        private readonly GameObject _muzzleFlash;
        private readonly GameObject _halo;
        private readonly BoxCollider _collider;
        private readonly GameFeelProfile _feel;
        private readonly Vector3 _barrelRestLocalPosition;
        private readonly Vector3 _muzzleFlashRestLocalPosition;
        private readonly Quaternion _rootRestLocalRotation;
        private readonly List<TintTarget> _tintTargets = new();
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private int _motionVersion;
        private int _recoilVersion;

        public UnitView(
            Transform parent,
            BlockColorId color,
            int charges,
            Vector3 position,
            VisualTheme theme,
            GameFeelProfile feel)
        {
            Color = color;
            _feel = feel;
            _root = new GameObject($"Unit_{color}_{charges}");
            _root.transform.SetParent(parent, false);
            _root.transform.position = position;
            _root.transform.localScale = ResolvePresentationScale(feel.QueuedUnitScale);

            Color body = ColorPalette.Get(color);
            Color face = ColorPalette.GetLight(color);
            Color dark = ColorPalette.GetDark(color);

            Color outline = UnityEngine.Color.Lerp(dark, new Color(0.02f, 0.025f, 0.04f), 0.30f);
            _halo = CreatePart("SelectionHalo", _root.transform, new Vector3(0f, -0.04f, 0.28f),
                new Vector3(0.98f, 0.82f, 0.08f), theme.WhiteUnlit,
                outline, false);
            _halo.SetActive(false);

            CreatePart("LeftTread", _root.transform, new Vector3(-0.36f, -0.03f, 0.03f),
                new Vector3(0.28f, 0.68f, 0.30f), theme.Body(color),
                UnityEngine.Color.Lerp(body, dark, 0.18f), true);
            CreatePart("RightTread", _root.transform, new Vector3(0.36f, -0.03f, 0.03f),
                new Vector3(0.28f, 0.68f, 0.30f), theme.Body(color),
                UnityEngine.Color.Lerp(body, dark, 0.18f), true);
            CreatePart("Body", _root.transform, new Vector3(0f, -0.01f, -0.06f),
                new Vector3(0.84f, 0.64f, 0.38f), theme.Body(color), body, true, 0.44f);
            CreatePart("FacePlate", _root.transform, new Vector3(0f, -0.02f, -0.31f),
                new Vector3(0.80f, 0.56f, 0.08f), theme.Face(color), face, false, 0.48f);

            GameObject pivot = new("TurretPivot");
            pivot.transform.SetParent(_root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0.34f, -0.30f);
            _turretPivot = pivot.transform;
            CreatePart("Turret", _turretPivot, Vector3.zero,
                new Vector3(0.43f, 0.22f, 0.24f), theme.Face(color), face, true, 0.44f);
            _barrel = CreatePart("Barrel", _turretPivot, new Vector3(0f, 0.12f, 0f),
                new Vector3(0.14f, 0.15f, 0.18f), theme.Face(color), face, true, 0.44f).transform;

            _muzzleFlash = CreatePart("MuzzleFlash", _turretPivot, new Vector3(0f, 0.23f, -0.05f),
                new Vector3(0.28f, 0.28f, 0.10f), theme.Projectile(color), face, false);
            _muzzleFlash.SetActive(false);

            GameObject textObject = new("Charges", typeof(RectTransform));
            textObject.transform.SetParent(_root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, -0.015f, -0.47f);
            textObject.transform.localScale = new Vector3(0.052f, 0.035f, 0.04f);
            if (Camera.main != null) textObject.transform.rotation = Camera.main.transform.rotation;
            textObject.AddComponent<CameraFacingLabel>();
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.sizeDelta = new Vector2(22f, 14f);
            _counter = textObject.AddComponent<TextMeshPro>();
            _counter.font = theme.Assets.UnitCounterFont;
            _counter.fontSharedMaterial = theme.Assets.UnitCounterMaterial;
            _counter.text = charges.ToString();
            _counter.fontSize = 112f;
            _counter.fontStyle = FontStyles.Normal;
            _counter.fontWeight = FontWeight.Regular;
            _counter.characterSpacing = -2f;
            _counter.alignment = TextAlignmentOptions.Center;
            _counter.color = UnityEngine.Color.white;
            _counter.textWrappingMode = TextWrappingModes.NoWrap;
            _counter.raycastTarget = false;
            _counter.renderer.shadowCastingMode = ShadowCastingMode.Off;

            _collider = _root.AddComponent<BoxCollider>();
            _collider.size = new Vector3(1.30f, 1.48f, 0.75f);
            _collider.center = new Vector3(0f, 0.12f, 0f);
            ClickTarget = _root.AddComponent<UnitClickTarget>();
            _barrelRestLocalPosition = _barrel.localPosition;
            _muzzleFlashRestLocalPosition = _muzzleFlash.transform.localPosition;
            _rootRestLocalRotation = _root.transform.localRotation;
        }

        public BlockColorId Color { get; }
        public UnitClickTarget ClickTarget { get; }
        public Transform Transform => _root.transform;
        public Vector3 MuzzlePosition => _turretPivot.TransformPoint(new Vector3(0f, 0.25f, -0.12f));

        public void SetCharges(int charges) => _counter.text = charges.ToString();

        public void SetQueueState(int depth, bool selectable)
        {
            _collider.enabled = selectable;
            _halo.SetActive(selectable);
            float brightness = selectable ? 1f : depth <= 0 ? 0.86f : depth == 1 ? 0.70f : 0.56f;
            float textAlpha = selectable ? 1f : depth <= 0 ? 0.88f : depth == 1 ? 0.68f : 0.50f;
            ApplyTint(brightness, textAlpha);
            SetShadowCasting(selectable);
            _root.transform.localScale = ResolvePresentationScale(
                selectable ? _feel.SelectableUnitScale : _feel.QueuedUnitScale);
        }

        public void SetActiveState()
        {
            _collider.enabled = false;
            _halo.SetActive(false);
            ApplyTint(1f, 1f);
            SetShadowCasting(true);
            _root.transform.localScale = ResolvePresentationScale(_feel.ActiveUnitScale);
        }

        public void AimAt(Vector3 target)
        {
            Vector3 direction = target - _turretPivot.position;
            if (direction.sqrMagnitude <= 0.000001f) return;

            // The authored barrel points along the pivot's local +Y axis. Convert the
            // world-space target direction into the root's space so yaw and depth pitch
            // both remain correct even when the unit/root has a parent rotation.
            Transform aimingSpace = _turretPivot.parent;
            Vector3 localDirection = aimingSpace != null
                ? aimingSpace.InverseTransformDirection(direction.normalized)
                : direction.normalized;
            _turretPivot.localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);
        }

        public IEnumerator MoveTo(Vector3 destination, float duration, Action completed)
        {
            int token = ++_motionVersion;
            _collider.enabled = false;
            Vector3 start = _root.transform.position;
            Vector3 startScale = _root.transform.localScale;
            Vector3 overshoot = destination +
                Vector3.up * (_feel.UnitMoveArc * _feel.CameraPlaneVerticalScale);
            const float travelFraction = 0.87f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (token != _motionVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (t < travelFraction)
                {
                    float travel = t / travelFraction;
                    _root.transform.position = Vector3.LerpUnclamped(start, overshoot, travel);
                    _root.transform.localScale = Vector3.LerpUnclamped(
                        startScale,
                        ResolvePresentationScale(_feel.ActiveUnitScale * 1.035f),
                        travel);
                }
                else
                {
                    float settle = Mathf.InverseLerp(travelFraction, 1f, t);
                    float easedSettle = 1f - Mathf.Pow(1f - settle, 2f);
                    _root.transform.position = Vector3.LerpUnclamped(overshoot, destination, easedSettle);
                    _root.transform.localScale = Vector3.LerpUnclamped(
                        ResolvePresentationScale(_feel.ActiveUnitScale * 1.035f),
                        ResolvePresentationScale(_feel.ActiveUnitScale),
                        easedSettle);
                }
                yield return null;
            }

            if (token != _motionVersion) yield break;
            _root.transform.position = destination;
            _root.transform.localScale = ResolvePresentationScale(_feel.ActiveUnitScale);
            completed?.Invoke();
        }

        public IEnumerator SlideTo(Vector3 destination, float duration)
        {
            int token = ++_motionVersion;
            Vector3 start = _root.transform.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (token != _motionVersion) yield break;
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
                _root.transform.position = Vector3.Lerp(start, destination, t);
                yield return null;
            }
            if (token == _motionVersion) _root.transform.position = destination;
        }

        public IEnumerator Recoil(float duration)
        {
            int token = ++_recoilVersion;
            _barrel.localPosition = _barrelRestLocalPosition;
            _muzzleFlash.transform.localPosition = _muzzleFlashRestLocalPosition;
            _root.transform.localRotation = _rootRestLocalRotation;
            float turretAngle = Mathf.DeltaAngle(0f, _turretPivot.localEulerAngles.z);
            float tiltDirection = Mathf.Abs(turretAngle) < 0.5f ? -1f : -Mathf.Sign(turretAngle);
            _muzzleFlash.SetActive(true);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (token != _recoilVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float recoil = Mathf.Sin(t * Mathf.PI) * 0.075f;
                _barrel.localPosition = _barrelRestLocalPosition + Vector3.down * recoil;
                _muzzleFlash.transform.localPosition = _muzzleFlashRestLocalPosition + Vector3.down * recoil;
                _root.transform.localRotation = _rootRestLocalRotation * Quaternion.Euler(
                    0f,
                    0f,
                    tiltDirection * Mathf.Sin(t * Mathf.PI) * 8f);
                float flash = 1f - Mathf.Clamp01(t / 0.45f);
                _muzzleFlash.transform.localScale = Vector3.one * (0.22f + flash * 0.36f);
                yield return null;
            }
            _barrel.localPosition = _barrelRestLocalPosition;
            _muzzleFlash.transform.localPosition = _muzzleFlashRestLocalPosition;
            _root.transform.localRotation = _rootRestLocalRotation;
            _muzzleFlash.SetActive(false);
        }

        public IEnumerator Leave(float duration, Action completed)
        {
            int token = ++_motionVersion;
            Vector3 start = _root.transform.position;
            Vector3 liftEnd = start + new Vector3(
                0f,
                1.42f * _feel.CameraPlaneVerticalScale,
                0.20f);
            Vector3 end = liftEnd + new Vector3(-6.0f, 0f, 0.30f);
            Vector3 startScale = _root.transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (token != _motionVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                const float liftFraction = 0.25f;
                if (t <= liftFraction)
                {
                    float lift = t / liftFraction;
                    _root.transform.position = Vector3.LerpUnclamped(start, liftEnd, lift);
                    _root.transform.localRotation = Quaternion.Euler(0f, 0f, -8f * lift);
                }
                else
                {
                    float exit = Mathf.InverseLerp(liftFraction, 1f, t);
                    _root.transform.position = Vector3.LerpUnclamped(liftEnd, end, exit);
                    _root.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                }
                _root.transform.localScale = startScale;
                yield return null;
            }
            UnityEngine.Object.Destroy(_root);
            completed?.Invoke();
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        private void ApplyTint(float brightness, float textAlpha)
        {
            Color shade = new(0.16f, 0.20f, 0.30f);
            for (int i = 0; i < _tintTargets.Count; i++)
            {
                TintTarget target = _tintTargets[i];
                Color color = UnityEngine.Color.Lerp(shade, target.BaseColor, brightness);
                target.Renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _propertyBlock.SetColor("_Color", color);
                target.Renderer.SetPropertyBlock(_propertyBlock);
            }
            Color counterColor = UnityEngine.Color.white;
            if (textAlpha < 0.999f)
            {
                float colorAmount = Mathf.Clamp01((1f - textAlpha) * 1.8f);
                counterColor = UnityEngine.Color.Lerp(
                    UnityEngine.Color.white,
                    ColorPalette.GetLight(Color),
                    colorAmount);
                // Queued labels stay readable, but inherit their unit color instead of
                // looking like a washed-out white overlay in the deeper rows.
                counterColor.a = Mathf.Lerp(0.62f, 0.94f, textAlpha);
            }
            _counter.color = counterColor;
        }

        private void SetShadowCasting(bool enabled)
        {
            for (int i = 0; i < _tintTargets.Count; i++)
            {
                TintTarget target = _tintTargets[i];
                target.Renderer.shadowCastingMode = enabled && target.CastsShadow
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
            }
        }

        private Vector3 ResolvePresentationScale(float uniformScale)
        {
            return new Vector3(
                uniformScale,
                uniformScale * _feel.CameraPlaneVerticalScale,
                uniformScale);
        }

        private GameObject CreatePart(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Color baseColor,
            bool castsShadow,
            float cornerRadius = RoundedBoxMesh.CornerRadius)
        {
            GameObject part = new(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = RoundedBoxMesh.Get(cornerRadius);
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castsShadow;
            _tintTargets.Add(new TintTarget(renderer, baseColor, castsShadow));
            return part;
        }
    }
}
