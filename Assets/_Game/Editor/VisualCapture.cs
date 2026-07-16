using System;
using System.IO;
using System.Reflection;
using ColorBlocks.Core;
using ColorBlocks.Gameplay;
using ColorBlocks.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorBlocks.Editor
{
    /// <summary>
    /// Batch-friendly visual QA capture of the live Game scene. The command enters
    /// Play Mode, lets layout and animation settle, and renders the world plus the
    /// overlay canvas into exact-size portrait images.
    /// </summary>
    [InitializeOnLoad]
    public static class VisualCapture
    {
        private readonly struct CaptureSpec
        {
            public CaptureSpec(int width, int height, string fileName)
            {
                Width = width;
                Height = height;
                FileName = fileName;
            }

            public int Width { get; }
            public int Height { get; }
            public string FileName { get; }
        }

        private const string ActiveKey = "ColorBlocks.VisualCapture.Active";
        private const string OutputKey = "ColorBlocks.VisualCapture.Output";
        private const string FinishingKey = "ColorBlocks.VisualCapture.Finishing";
        private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";
        private static readonly CaptureSpec[] Specs =
        {
            new(1080, 1920, "initial-1080x1920.png"),
            new(720, 1600, "initial-720x1600.png"),
            new(1170, 2532, "initial-1170x2532.png")
        };

        private static int _captureIndex;
        private static int _settleFrames;
        private static bool _preparedExtendedStage;
        private static bool _layoutRefreshPending;
        private static float _requestedAspect;
        private static double _stageStartedAt;
        private static string _pendingCapturePath;
        private static RenderTexture _pendingTarget;
        private static Camera _pendingCamera;
        private static Canvas _pendingCanvas;
        private static RenderMode _pendingCanvasMode;
        private static Camera _pendingCanvasCamera;
        private static float _pendingCanvasDistance;
        private static RenderTexture _pendingPreviousTarget;
        private static float _pendingPreviousAspect;
        private static float _pendingPreviousOrthographicSize;
        private static AsyncGPUReadbackRequest _pendingReadback;
        private static bool _pendingReadbackActive;

        static VisualCapture()
        {
            if (SessionState.GetBool(ActiveKey, false)) AttachStateHandler();
        }

        public static void Run()
        {
            string output = GetArgument("-captureOutput");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/Audit/PolishPass"));
            }

            Directory.CreateDirectory(output);
            SessionState.SetString(OutputKey, output);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(FinishingKey, false);
            _captureIndex = 0;
            _settleFrames = 0;
            _preparedExtendedStage = false;
            _layoutRefreshPending = false;
            _pendingCapturePath = null;
            _stageStartedAt = EditorApplication.timeSinceStartup;
            AttachStateHandler();
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void AttachStateHandler()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _captureIndex = 0;
                _settleFrames = 0;
                _preparedExtendedStage = false;
                _layoutRefreshPending = false;
                _pendingCapturePath = null;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                ApplyResolution(Specs[_captureIndex]);
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishingKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(FinishingKey, false);
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.Exit(0);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            _settleFrames++;
            if (_layoutRefreshPending && _settleFrames >= 3)
            {
                ReloadCurrentLevelForResolution();
                _layoutRefreshPending = false;
                _settleFrames = 0;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                return;
            }
            if (_captureIndex == Specs.Length + 2 && EditorApplication.timeSinceStartup - _stageStartedAt < 0.20d) return;
            if (_captureIndex == Specs.Length + 3 && EditorApplication.timeSinceStartup - _stageStartedAt < 0.20d) return;
            if (_captureIndex == Specs.Length + 4 && EditorApplication.timeSinceStartup - _stageStartedAt < 1.25d) return;
            if ((_captureIndex == Specs.Length + 5 ||
                 _captureIndex == Specs.Length + 6 ||
                 _captureIndex == Specs.Length + 7) &&
                EditorApplication.timeSinceStartup - _stageStartedAt < 0.75d) return;
            if (_captureIndex != Specs.Length + 2 &&
                _captureIndex != Specs.Length + 3 &&
                _captureIndex != Specs.Length + 4 &&
                _captureIndex != Specs.Length + 5 &&
                _captureIndex != Specs.Length + 6 &&
                _settleFrames < 18) return;

            if (_captureIndex < Specs.Length)
            {
                if (!Capture(Specs[_captureIndex])) return;
                _captureIndex++;
                if (_captureIndex < Specs.Length)
                {
                    _settleFrames = 0;
                    ApplyResolution(Specs[_captureIndex]);
                    return;
                }

                PrepareSafeAreaSimulation();
                return;
            }

            CaptureExtendedStage();
        }

        private static void PrepareLevelThree()
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            if (controller == null || loadLevel == null) throw new InvalidOperationException("Unable to prepare Level 3 visual capture.");
            loadLevel.Invoke(controller, new object[] { 3 });
            _preparedExtendedStage = true;
            _settleFrames = 0;
            ApplyResolution(new CaptureSpec(1080, 1920, "level3-layers-initial-1080x1920.png"));
        }

        private static void PrepareLevelFive()
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            if (controller == null || loadLevel == null) throw new InvalidOperationException("Unable to prepare Level 5 visual capture.");
            loadLevel.Invoke(controller, new object[] { 5 });
            _preparedExtendedStage = true;
            _settleFrames = 0;
            _stageStartedAt = EditorApplication.timeSinceStartup;
            ApplyResolution(new CaptureSpec(1080, 1920, "level5-dense-initial-1080x1920.png"));
        }

        private static void PrepareSafeAreaSimulation()
        {
            SafeAreaFitter fitter = UnityEngine.Object.FindFirstObjectByType<SafeAreaFitter>();
            if (fitter == null) throw new InvalidOperationException("Safe-area visual capture requires SafeAreaFitter.");
            fitter.enabled = false;
            RectTransform rect = (RectTransform)fitter.transform;
            rect.anchorMin = new Vector2(0.035f, 0.055f);
            rect.anchorMax = new Vector2(0.965f, 0.945f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _preparedExtendedStage = true;
            _settleFrames = 0;
            ApplyResolution(new CaptureSpec(1170, 2532, "safearea-sim-1170x2532.png"));
        }

        private static void RestoreSafeAreaSimulation()
        {
            SafeAreaFitter fitter = UnityEngine.Object.FindFirstObjectByType<SafeAreaFitter>();
            if (fitter == null) return;
            RectTransform rect = (RectTransform)fitter.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fitter.enabled = true;
        }

        private static void CaptureExtendedStage()
        {
            if (!_preparedExtendedStage) throw new InvalidOperationException("Extended visual capture stage was not prepared.");

            if (_captureIndex == Specs.Length)
            {
                if (!Capture(new CaptureSpec(1170, 2532, "safearea-sim-1170x2532.png"))) return;
                RestoreSafeAreaSimulation();
                _captureIndex++;
                PrepareLevelThree();
                return;
            }

            if (_captureIndex == Specs.Length + 1)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level3-layers-initial-1080x1920.png"))) return;
                SelectWinningFrontUnit(3);
                _captureIndex++;
                _settleFrames = 0;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (_captureIndex == Specs.Length + 2)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level3-action-200ms-1080x1920.png"))) return;
                _captureIndex++;
                _settleFrames = 0;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (_captureIndex == Specs.Length + 3)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level3-action-430ms-1080x1920.png"))) return;
                _captureIndex++;
                _settleFrames = 0;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (_captureIndex == Specs.Length + 4)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level3-gravity-settled-1080x1920.png"))) return;
                _captureIndex++;
                PrepareLevelFive();
                return;
            }

            if (_captureIndex == Specs.Length + 5)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level5-dense-initial-1080x1920.png"))) return;
                ShowResultOverlay(true);
                _captureIndex++;
                _settleFrames = 0;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (_captureIndex == Specs.Length + 6)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "win-popup-1080x1920.png"))) return;
                ShowResultOverlay(false);
                _captureIndex++;
                _settleFrames = 0;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (_captureIndex != Specs.Length + 7)
            {
                throw new InvalidOperationException($"Unexpected visual capture stage {_captureIndex}.");
            }

            if (!Capture(new CaptureSpec(1080, 1920, "loss-popup-1080x1920.png"))) return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(FinishingKey, true);
            EditorApplication.ExitPlaymode();
        }

        private static void SelectWinningFrontUnit(int levelNumber)
        {
            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            LevelSolveResult solution = LevelSolver.Solve(catalog.GetLevel(levelNumber));
            if (!solution.IsSolvable || solution.WinningLaneChoices.Count == 0)
            {
                throw new InvalidOperationException($"Level {levelNumber} has no visual-capture selection path.");
            }

            int laneIndex = solution.WinningLaneChoices[0] - 1;
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            Camera camera = Camera.main;
            LevelDefinition level = catalog.GetLevel(levelNumber);
            if (presentation == null || camera == null) throw new InvalidOperationException("Reference layout is unavailable.");
            GameplayLayout layout = presentation.FeelProfile.ResolveLayout(level.Width, level.Height, camera.aspect);
            float targetX = layout.QueueX(laneIndex, LevelSolver.LaneCount);
            UnitClickTarget[] targets = UnityEngine.Object.FindObjectsByType<UnitClickTarget>(FindObjectsSortMode.None);
            UnitClickTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < targets.Length; i++)
            {
                Collider collider = targets[i].GetComponent<Collider>();
                if (collider == null || !collider.enabled) continue;
                float distance = Mathf.Abs(targets[i].transform.position.x - targetX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = targets[i];
                }
            }

            if (best == null) throw new InvalidOperationException($"No selectable front unit exists in lane {laneIndex + 1}.");
            best.NotifyClick();
        }

        private static void ShowResultOverlay(bool won)
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            FieldInfo hudField = typeof(GameController).GetField("_hud", BindingFlags.Instance | BindingFlags.NonPublic);
            HudView hud = hudField?.GetValue(controller) as HudView;
            if (hud == null) throw new InvalidOperationException("Unable to access the HUD for result-state capture.");
            hud.ShowResult(won, null);
        }

        private static void ApplyResolution(CaptureSpec spec)
        {
            Screen.SetResolution(spec.Width, spec.Height, FullScreenMode.Windowed);
            _requestedAspect = spec.Width / (float)spec.Height;
            _layoutRefreshPending = true;
            Debug.Log($"[VisualCapture] Preparing {spec.Width}x{spec.Height} render.");
        }

        private static void ReloadCurrentLevelForResolution()
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            FieldInfo levelField = typeof(GameController).GetField(
                "_currentLevel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo loadLevel = typeof(GameController).GetMethod(
                "LoadLevel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            LevelDefinition current = levelField?.GetValue(controller) as LevelDefinition;
            Camera camera = Camera.main;
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            if (controller == null || current == null || loadLevel == null || camera == null || presentation == null)
            {
                throw new InvalidOperationException("Unable to refresh the live layout after a resolution change.");
            }

            controller.SetEditorLayoutAspect(_requestedAspect);
            loadLevel.Invoke(controller, new object[] { current.LevelNumber });
        }

        private static bool Capture(CaptureSpec spec)
        {
            string path = Path.Combine(SessionState.GetString(OutputKey, string.Empty), spec.FileName);
            if (_pendingTarget != null)
            {
                if (!string.Equals(_pendingCapturePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A different visual capture is already pending.");
                }

                if (!_pendingReadbackActive || !_pendingReadback.done) return false;
                if (_pendingReadback.hasError)
                {
                    ReleasePendingCapture();
                    throw new InvalidOperationException("GPU readback failed for the visual QA frame.");
                }

                RenderTexture previousActive = RenderTexture.active;
                Texture2D image = new(spec.Width, spec.Height, TextureFormat.RGB24, false, false);
                try
                {
                    RenderTexture.active = _pendingTarget;
                    image.ReadPixels(new Rect(0f, 0f, spec.Width, spec.Height), 0, 0, false);
                    image.Apply(false, false);
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    Debug.Log($"[VisualCapture] Captured completed URP frame {path}.");
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    UnityEngine.Object.DestroyImmediate(image);
                    ReleasePendingCapture();
                }

                return true;
            }

            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Visual capture requires a Main Camera.");
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();

            RenderTexture target = new(spec.Width, spec.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 1,
                name = $"VisualCapture_{spec.Width}x{spec.Height}"
            };
            if (!target.Create())
            {
                UnityEngine.Object.DestroyImmediate(target);
                throw new InvalidOperationException($"Could not create {spec.Width}x{spec.Height} capture target.");
            }

            if (File.Exists(path)) File.Delete(path);
            _pendingCapturePath = path;
            _pendingTarget = target;
            _pendingCamera = camera;
            _pendingPreviousTarget = camera.targetTexture;
            _pendingPreviousAspect = camera.aspect;
            _pendingPreviousOrthographicSize = camera.orthographicSize;
            camera.aspect = spec.Width / (float)spec.Height;
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            camera.orthographicSize = presentation != null
                ? presentation.FeelProfile.ResolveCameraHalfHeight(camera.aspect)
                : Mathf.Max(8.35f, 4.7f / camera.aspect);
            camera.targetTexture = target;

            _pendingCanvas = canvas;
            if (canvas != null)
            {
                _pendingCanvasMode = canvas.renderMode;
                _pendingCanvasCamera = canvas.worldCamera;
                _pendingCanvasDistance = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
            }

            RenderTexture requestPreviousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, camera.backgroundColor);
                camera.Render();
                _pendingReadback = AsyncGPUReadback.Request(target);
                _pendingReadbackActive = true;

                // Do not leave the live camera targeting this texture while the async request
                // completes. The normal player loop could otherwise begin another render and
                // ReadPixels may observe a partially submitted frame (large black tiles).
                RestoreRenderOverrides();
            }
            finally
            {
                RenderTexture.active = requestPreviousActive;
            }

            return false;
        }

        private static void ReleasePendingCapture()
        {
            RestoreRenderOverrides();

            if (_pendingTarget != null)
            {
                _pendingTarget.Release();
                UnityEngine.Object.DestroyImmediate(_pendingTarget);
            }

            _pendingCapturePath = null;
            _pendingTarget = null;
            _pendingCamera = null;
            _pendingCanvas = null;
            _pendingReadbackActive = false;
        }

        private static void RestoreRenderOverrides()
        {
            if (_pendingCamera != null)
            {
                _pendingCamera.targetTexture = _pendingPreviousTarget;
                _pendingCamera.aspect = _pendingPreviousAspect;
                _pendingCamera.orthographicSize = _pendingPreviousOrthographicSize;
                _pendingCamera = null;
            }

            if (_pendingCanvas != null)
            {
                _pendingCanvas.renderMode = _pendingCanvasMode;
                _pendingCanvas.worldCamera = _pendingCanvasCamera;
                _pendingCanvas.planeDistance = _pendingCanvasDistance;
                _pendingCanvas = null;
            }
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }
    }
}
