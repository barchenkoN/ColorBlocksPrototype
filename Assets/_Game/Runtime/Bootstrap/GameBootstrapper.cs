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
        private GameFeelProfile _feel;
        private float _configuredAspect = -1f;

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

            _feel = presentationAssets.FeelProfile;
            ConfigureScene(_feel);

            GameController controller = gameObject.GetComponent<GameController>();
            if (controller == null) controller = gameObject.AddComponent<GameController>();
            controller.Initialize(catalog, presentationAssets);
        }

        private void Update()
        {
            if (_camera == null || _feel == null) return;
            if (Mathf.Abs(_configuredAspect - _camera.aspect) < 0.0001f) return;
            ConfigureCamera(_camera.aspect);
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

            ConfigureCamera(_camera.aspect);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.365f, 0.50f, 0.75f);

            if (_camera.GetComponent<AudioListener>() == null)
            {
                _camera.gameObject.AddComponent<AudioListener>();
            }

            Light main = EnsureNamedLight("Main Light");
            main.type = LightType.Directional;
            main.color = new Color(1.0f, 0.94f, 0.84f);
            // The steeper 40-degree key shortens shadows but contributes less frontal diffuse
            // light than the old 18-degree angle. Compensate intensity so calibrated palette
            // values remain unchanged on the board-facing surfaces.
            main.intensity = 1.42f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.40f;
            main.shadowBias = 0.055f;
            main.shadowNormalBias = 0.28f;
            main.shadowNearPlane = 0.15f;
            main.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            main.transform.rotation = Quaternion.Euler(40f, -24f, 0f);

            Light fill = EnsureNamedLight("Fill Light");
            fill.type = LightType.Directional;
            fill.color = new Color(0.82f, 0.86f, 1.0f);
            fill.intensity = 0.52f;
            fill.shadows = LightShadows.None;
            // Aim the fill across the board plane so it lifts only the camera-facing Y
            // depth bands. This keeps the top faces modeled by the key light while making
            // a supported lower cube readable beneath the cube resting on it.
            fill.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.27f, 0.38f);
            RenderSettings.reflectionIntensity = 0.38f;
            RenderSettings.fog = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            // The 6-degree long lens sits about 195 units from the board. Keep the complete
            // tray inside the realtime shadow range on the Mobile URP path.
            QualitySettings.shadowDistance = 230f;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 2;
        }

        private static Light EnsureNamedLight(string objectName)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light candidate = lights[i];
                if (candidate == null || candidate.gameObject.name != objectName) continue;
                candidate.gameObject.SetActive(true);
                candidate.enabled = true;
                return candidate;
            }

            GameObject lightObject = new(objectName);
            return lightObject.AddComponent<Light>();
        }

        private void ConfigureCamera(float aspect)
        {
            if (_camera == null || _feel == null) return;
            _configuredAspect = Mathf.Max(0.1f, aspect);
            PresentationCameraRig.Configure(_camera, _feel, _configuredAspect);
        }
    }
}
