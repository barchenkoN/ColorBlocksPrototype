using ColorBlocks.Core;
using ColorBlocks.Gameplay;
using ColorBlocks.Presentation;
using UnityEngine;

namespace ColorBlocks.Bootstrap
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private Camera _camera;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;

            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            if (catalog == null)
            {
                Debug.LogError("LevelCatalog was not found in Resources. Run Color Blocks > Rebuild Prototype Content.");
                enabled = false;
                return;
            }

            PresentationAssets presentationAssets = Resources.Load<PresentationAssets>("PresentationAssets");
            if (presentationAssets == null)
            {
                Debug.LogError("PresentationAssets are missing. Run Color Blocks > Rebuild Prototype Content.");
                enabled = false;
                return;
            }
            if (!presentationAssets.IsValid(out string presentationError))
            {
                Debug.LogError($"PresentationAssets are invalid ({presentationError}). Run Color Blocks > Rebuild Prototype Content.");
                enabled = false;
                return;
            }

            ConfigureScene(presentationAssets.FeelProfile);

            GameController controller = gameObject.GetComponent<GameController>();
            if (controller == null) controller = gameObject.AddComponent<GameController>();
            controller.Initialize(catalog, presentationAssets);
        }

        private void Update()
        {
            if (_camera == null) return;
            PresentationAssets assets = Resources.Load<PresentationAssets>("PresentationAssets");
            if (assets != null && assets.FeelProfile != null)
            {
                _camera.orthographicSize = assets.FeelProfile.ResolveCameraHalfHeight(_camera.aspect);
            }
        }

        private void ConfigureScene(GameFeelProfile feel)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject cameraObject = new("Main Camera");
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.AddComponent<Camera>();
            }

            _camera.transform.position = new Vector3(0f, 0f, -15f);
            _camera.transform.rotation = Quaternion.identity;
            _camera.orthographic = true;
            _camera.orthographicSize = feel.ResolveCameraHalfHeight(_camera.aspect);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.36f, 0.45f, 0.62f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;

            if (_camera.GetComponent<AudioListener>() == null)
            {
                _camera.gameObject.AddComponent<AudioListener>();
            }

            if (FindAnyObjectByType<Light>() == null)
            {
                GameObject lightObject = new("Main Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.88f, 0.94f, 1f);
                light.intensity = 1.12f;
                // The scene uses explicit bevels and authored fake depth. Realtime directional
                // shadows become extremely long when a unit moves towards the board plane and
                // can cover most of a portrait screen on mobile GPUs/capture cameras.
                light.shadows = LightShadows.None;
                light.shadowStrength = 0f;
                lightObject.transform.rotation = Quaternion.Euler(28f, -34f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.46f, 0.58f);
            RenderSettings.fog = false;
        }
    }
}
