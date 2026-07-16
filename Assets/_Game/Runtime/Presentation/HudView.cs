using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ColorBlocks.Presentation
{
    public sealed class HudView
    {
        private readonly MonoBehaviour _host;
        private readonly GameObject _root;
        private readonly TMP_FontAsset _font;
        private readonly TMP_Text _levelText;
        private readonly GameObject _resultOverlay;
        private readonly RectTransform _resultCard;
        private readonly TMP_Text _resultTitle;
        private readonly TMP_Text _resultMessage;
        private readonly TMP_Text _actionLabel;
        private readonly Button _actionButton;
        private Action _action;

        public HudView(MonoBehaviour host, PresentationAssets assets, Action restartRequested)
        {
            _host = host;
            _font = assets.PrimaryFont;

            _root = new GameObject("GameHUD");
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.44f;
            _root.AddComponent<GraphicRaycaster>();

            GameObject safeObject = CreateRect("SafeArea", _root.transform);
            safeObject.AddComponent<SafeAreaFitter>();

            Image pillShadow = CreateRoundedImage("LevelPillShadow", safeObject.transform, new Color(0.035f, 0.075f, 0.16f, 0.38f));
            SetAnchors(pillShadow.rectTransform, new Vector2(0.16f, 0.943f), new Vector2(0.84f, 0.981f), new Vector2(0f, -7f), new Vector2(0f, -7f));

            Image pill = CreateRoundedImage("LevelPill", safeObject.transform, new Color(0.075f, 0.18f, 0.34f, 0.98f));
            SetAnchors(pill.rectTransform, new Vector2(0.16f, 0.943f), new Vector2(0.84f, 0.981f), Vector2.zero, Vector2.zero);

            Image pillInner = CreateRoundedImage("LevelPillInner", pill.transform, new Color(0.025f, 0.09f, 0.17f, 0.95f));
            Stretch(pillInner.rectTransform, 9f);

            _levelText = CreateText("LevelText", pill.transform, 42f, TextAlignmentOptions.Center, Color.white);
            Stretch(_levelText.rectTransform, 5f);

            Button restart = CreateRestartButton(safeObject.transform);
            SetAnchors(restart.GetComponent<RectTransform>(), new Vector2(0.865f, 0.943f), new Vector2(0.945f, 0.981f), Vector2.zero, Vector2.zero);
            restart.onClick.AddListener(() => restartRequested?.Invoke());

            _resultOverlay = CreateRect("ResultOverlay", _root.transform);
            Stretch((RectTransform)_resultOverlay.transform, 0f);
            Image dim = _resultOverlay.AddComponent<Image>();
            dim.color = new Color(0.025f, 0.05f, 0.12f, 0.58f);

            Image cardShadow = CreateRoundedImage("CardShadow", _resultOverlay.transform, new Color(0.02f, 0.04f, 0.10f, 0.48f));
            SetAnchors(cardShadow.rectTransform, new Vector2(0.10f, 0.335f), new Vector2(0.90f, 0.675f), new Vector2(0f, -14f), new Vector2(0f, -14f));

            Image card = CreateRoundedImage("ResultCard", _resultOverlay.transform, new Color(0.10f, 0.22f, 0.40f, 0.98f));
            _resultCard = card.rectTransform;
            SetAnchors(_resultCard, new Vector2(0.10f, 0.345f), new Vector2(0.90f, 0.685f), Vector2.zero, Vector2.zero);

            Image cardInner = CreateRoundedImage("CardInner", card.transform, new Color(0.16f, 0.31f, 0.52f, 0.92f));
            Stretch(cardInner.rectTransform, 10f);

            _resultTitle = CreateText("ResultTitle", card.transform, 76f, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.20f));
            SetAnchors(_resultTitle.rectTransform, new Vector2(0.06f, 0.58f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
            _resultTitle.enableAutoSizing = true;
            _resultTitle.fontSizeMin = 44f;
            _resultTitle.fontSizeMax = 76f;

            _resultMessage = CreateText("ResultMessage", card.transform, 31f, TextAlignmentOptions.Center, new Color(0.90f, 0.95f, 1f));
            SetAnchors(_resultMessage.rectTransform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.60f), Vector2.zero, Vector2.zero);

            _actionButton = CreateButton("Action", card.transform, "CONTINUE", new Color(0.32f, 0.76f, 0.30f), 40f);
            SetAnchors(_actionButton.GetComponent<RectTransform>(), new Vector2(0.17f, 0.10f), new Vector2(0.83f, 0.34f), Vector2.zero, Vector2.zero);
            _actionLabel = _actionButton.GetComponentInChildren<TMP_Text>();
            _actionButton.onClick.AddListener(InvokeAction);
            _resultOverlay.SetActive(false);

            EnsureEventSystem();
        }

        public bool IsOverlayVisible => _resultOverlay.activeSelf;

        public void SetLevel(int levelNumber)
        {
            _levelText.text = $"Level {levelNumber}";
        }

        public void ShowResult(bool won, Action action)
        {
            _action = action;
            _resultTitle.text = won ? "LEVEL\nCOMPLETE!" : "OUT OF\nSPACE";
            _resultTitle.color = won ? new Color(1f, 0.82f, 0.20f) : new Color(1f, 0.42f, 0.48f);
            _resultMessage.text = won ? "Board cleared!" : "No active unit matches the frontier.";
            _actionLabel.text = won ? "CONTINUE" : "TRY AGAIN";
            _resultOverlay.SetActive(true);
            _host.StartCoroutine(AnimateCard());
        }

        public void HideResult()
        {
            _resultOverlay.SetActive(false);
            _action = null;
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        private IEnumerator AnimateCard()
        {
            Vector3 start = Vector3.one * 0.72f;
            float elapsed = 0f;
            const float duration = 0.24f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                _resultCard.localScale = Vector3.LerpUnclamped(start, Vector3.one, eased) * overshoot;
                yield return null;
            }
            _resultCard.localScale = Vector3.one;
        }

        private void InvokeAction() => _action?.Invoke();

        private TMP_Text CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment, Color color)
        {
            GameObject gameObject = CreateRect(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineWidth = 0.14f;
            text.outlineColor = new Color32(20, 31, 61, 255);
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color, float fontSize)
        {
            Image image = CreateRoundedImage(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Image inner = CreateRoundedImage("Inner", image.transform, Color.Lerp(color, Color.white, 0.13f));
            Stretch(inner.rectTransform, 7f);
            inner.raycastTarget = false;
            TMP_Text text = CreateText("Label", inner.transform, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform, 8f);
            text.text = label;
            return button;
        }

        private Button CreateRestartButton(Transform parent)
        {
            Color baseColor = new(0.13f, 0.30f, 0.51f);
            Image image = CreateRoundedImage("Restart", parent, baseColor);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Image inner = CreateRoundedImage("Inner", image.transform, Color.Lerp(baseColor, Color.white, 0.13f));
            Stretch(inner.rectTransform, 6f);
            inner.raycastTarget = false;
            GameObject iconObject = CreateRect("Icon", inner.transform);
            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = UiShapeFactory.RestartIcon;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetAnchors(icon.rectTransform, new Vector2(0.19f, 0.19f), new Vector2(0.81f, 0.81f), Vector2.zero, Vector2.zero);
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystem = new("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Image CreateRoundedImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = CreateRect(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = UiShapeFactory.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }
    }
}
