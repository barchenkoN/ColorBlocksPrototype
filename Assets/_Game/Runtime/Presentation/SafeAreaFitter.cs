using UnityEngine;

namespace ColorBlocks.Presentation
{
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

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
            RectTransform rect = (RectTransform)transform;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _lastSafeArea = safe;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
