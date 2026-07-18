using UnityEngine;

namespace ColorBlocks.Presentation
{
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        // The supplied 1170 x 2532 reference places the header at y=122..207.
        // Keep that authored full-screen position, then move it down only when a
        // real device cutout would overlap it. Scaling the whole header inside the
        // safe rect made it narrower and caused it to collide with the board.
        private const float AuthoredHeaderTop = 122f / 2532f;

        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;
#if UNITY_EDITOR
        private bool _hasEditorOverride;
        private Rect _editorNormalizedSafeArea;
#endif

        public static float CurrentTopInsetNormalized { get; private set; }

        private void OnEnable() => Apply();

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea || _lastResolution.x != Screen.width || _lastResolution.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            Rect normalizedSafe;
#if UNITY_EDITOR
            if (_hasEditorOverride)
            {
                normalizedSafe = _editorNormalizedSafeArea;
            }
            else
#endif
            {
                float width = Mathf.Max(1f, Screen.width);
                float height = Mathf.Max(1f, Screen.height);
                normalizedSafe = new Rect(
                    safe.xMin / width,
                    safe.yMin / height,
                    safe.width / width,
                    safe.height / height);
            }

            float topInset = Mathf.Clamp01(1f - normalizedSafe.yMax);
            float verticalShift = Mathf.Max(0f, topInset - AuthoredHeaderTop);
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0f, -verticalShift);
            rect.anchorMax = new Vector2(1f, 1f - verticalShift);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            CurrentTopInsetNormalized = topInset;
            _lastSafeArea = safe;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);
        }

#if UNITY_EDITOR
        public void SetEditorNormalizedSafeArea(Rect normalizedSafeArea)
        {
            _hasEditorOverride = true;
            _editorNormalizedSafeArea = normalizedSafeArea;
            Apply();
        }

        public void ClearEditorNormalizedSafeArea()
        {
            _hasEditorOverride = false;
            Apply();
        }
#endif
    }
}
