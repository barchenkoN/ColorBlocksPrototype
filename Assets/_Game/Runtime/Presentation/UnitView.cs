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

    public sealed class UnitView
    {
        private readonly struct TintTarget
        {
            public TintTarget(MeshRenderer renderer, Color baseColor)
            {
                Renderer = renderer;
                BaseColor = baseColor;
            }

            public MeshRenderer Renderer { get; }
            public Color BaseColor { get; }
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
            _root.transform.localScale = Vector3.one * feel.QueuedUnitScale;

            Color body = ColorPalette.Get(color);
            Color face = ColorPalette.GetLight(color);
            Color dark = ColorPalette.GetDark(color);

            _halo = CreatePart("SelectionHalo", _root.transform, new Vector3(0f, 0.05f, 0.28f),
                new Vector3(1.12f, 1.19f, 0.10f), theme.WhiteUnlit,
                UnityEngine.Color.Lerp(dark, face, 0.16f), false);
            _halo.SetActive(false);

            CreatePart("LeftTread", _root.transform, new Vector3(-0.39f, -0.03f, 0.03f),
                new Vector3(0.28f, 0.78f, 0.30f), theme.Body(color), dark, true);
            CreatePart("RightTread", _root.transform, new Vector3(0.39f, -0.03f, 0.03f),
                new Vector3(0.28f, 0.78f, 0.30f), theme.Body(color), dark, true);
            CreatePart("Body", _root.transform, new Vector3(0f, 0.04f, -0.06f),
                new Vector3(0.76f, 0.70f, 0.38f), theme.Body(color), body, true);
            CreatePart("FacePlate", _root.transform, new Vector3(0f, -0.04f, -0.31f),
                new Vector3(0.61f, 0.39f, 0.10f), theme.Face(color), face, false);

            GameObject pivot = new("TurretPivot");
            pivot.transform.SetParent(_root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0.40f, -0.08f);
            _turretPivot = pivot.transform;
            CreatePart("Turret", _turretPivot, Vector3.zero,
                new Vector3(0.48f, 0.31f, 0.40f), theme.Face(color), face, true);
            _barrel = CreatePart("Barrel", _turretPivot, new Vector3(0f, 0.30f, 0f),
                new Vector3(0.17f, 0.30f, 0.22f), theme.Face(color), face, true).transform;

            _muzzleFlash = CreatePart("MuzzleFlash", _turretPivot, new Vector3(0f, 0.48f, -0.05f),
                new Vector3(0.28f, 0.28f, 0.10f), theme.Projectile(color), UnityEngine.Color.white, false);
            _muzzleFlash.SetActive(false);

            GameObject textObject = new("Charges", typeof(RectTransform));
            textObject.transform.SetParent(_root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, -0.09f, -0.47f);
            textObject.transform.localScale = Vector3.one * 0.04f;
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.sizeDelta = new Vector2(20f, 14f);
            _counter = textObject.AddComponent<TextMeshPro>();
            _counter.font = theme.Assets.PrimaryFont;
            _counter.text = charges.ToString();
            _counter.fontSize = 64f;
            _counter.fontStyle = FontStyles.Bold;
            _counter.alignment = TextAlignmentOptions.Center;
            _counter.color = UnityEngine.Color.white;
            _counter.outlineWidth = 0.14f;
            _counter.outlineColor = new Color32(40, 35, 55, 255);
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
        public Vector3 MuzzlePosition => _turretPivot.TransformPoint(new Vector3(0f, 0.50f, -0.12f));

        public void SetCharges(int charges) => _counter.text = charges.ToString();

        public void SetQueueState(int depth, bool selectable)
        {
            _collider.enabled = selectable;
            _halo.SetActive(selectable);
            float brightness = selectable ? 1f : depth <= 0 ? 0.86f : depth == 1 ? 0.70f : 0.56f;
            float textAlpha = selectable ? 1f : depth <= 0 ? 0.88f : depth == 1 ? 0.68f : 0.50f;
            ApplyTint(brightness, textAlpha);
            _root.transform.localScale = Vector3.one *
                (selectable ? _feel.SelectableUnitScale : _feel.QueuedUnitScale);
        }

        public void SetActiveState()
        {
            _collider.enabled = false;
            _halo.SetActive(false);
            ApplyTint(1f, 1f);
            _root.transform.localScale = Vector3.one * _feel.ActiveUnitScale;
        }

        public void AimAt(Vector3 target)
        {
            Vector3 direction = target - _turretPivot.position;
            float angle = -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            _turretPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public IEnumerator MoveTo(Vector3 destination, float duration, Action completed)
        {
            int token = ++_motionVersion;
            _collider.enabled = false;
            Vector3 start = _root.transform.position;
            Vector3 startScale = _root.transform.localScale;
            Vector3 overshoot = destination + Vector3.up * _feel.UnitMoveArc;
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
                        Vector3.one * (_feel.ActiveUnitScale * 1.035f),
                        travel);
                }
                else
                {
                    float settle = Mathf.InverseLerp(travelFraction, 1f, t);
                    float easedSettle = 1f - Mathf.Pow(1f - settle, 2f);
                    _root.transform.position = Vector3.LerpUnclamped(overshoot, destination, easedSettle);
                    _root.transform.localScale = Vector3.LerpUnclamped(
                        Vector3.one * (_feel.ActiveUnitScale * 1.035f),
                        Vector3.one * _feel.ActiveUnitScale,
                        easedSettle);
                }
                yield return null;
            }

            if (token != _motionVersion) yield break;
            _root.transform.position = destination;
            _root.transform.localScale = Vector3.one * _feel.ActiveUnitScale;
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
                float recoil = Mathf.Sin(t * Mathf.PI) * 0.15f;
                _barrel.localPosition = _barrelRestLocalPosition + Vector3.down * recoil;
                _muzzleFlash.transform.localPosition = _muzzleFlashRestLocalPosition + Vector3.down * recoil;
                _root.transform.localRotation = _rootRestLocalRotation * Quaternion.Euler(
                    0f,
                    0f,
                    tiltDirection * Mathf.Sin(t * Mathf.PI) * 10f);
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
            Vector3 end = start + new Vector3(-0.65f, -1.0f, 0.6f);
            Vector3 startScale = _root.transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (token != _motionVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t;
                _root.transform.position = Vector3.Lerp(start, end, eased);
                _root.transform.localRotation = Quaternion.Euler(0f, 0f, -20f * eased);
                _root.transform.localScale = startScale * (1f - eased);
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
            counterColor.a = textAlpha;
            _counter.color = counterColor;
        }

        private GameObject CreatePart(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Color baseColor,
            bool castsShadow)
        {
            GameObject part = new(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = ChamferedCubeMesh.Get();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Depth is carried by the chamfered mesh and color values. Realtime shadows from a
            // moving unit project across the large background plane and cause black screen-sized
            // artifacts at some animation poses.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _tintTargets.Add(new TintTarget(renderer, baseColor));
            return part;
        }
    }
}
