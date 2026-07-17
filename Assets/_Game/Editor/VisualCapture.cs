using System;
using System.IO;
using System.Reflection;
using ColorBlocks.Core;
using ColorBlocks.Gameplay;
using ColorBlocks.Presentation;
using TMPro;
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

        private enum ScriptedOutcome
        {
            None,
            Win,
            Loss
        }

        private const string ActiveKey = "ColorBlocks.VisualCapture.Active";
        private const string OutputKey = "ColorBlocks.VisualCapture.Output";
        private const string FinishingKey = "ColorBlocks.VisualCapture.Finishing";
        private const string ExitCodeKey = "ColorBlocks.VisualCapture.ExitCode";
        private const string QualityOverrideKey = "ColorBlocks.VisualCapture.QualityOverride";
        private const string OriginalQualityKey = "ColorBlocks.VisualCapture.OriginalQuality";
        private const string RequestedQualityKey = "ColorBlocks.VisualCapture.RequestedQuality";
        private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";
        private const string MobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
        private const int ResolutionApplyPlayerFrames = 3;
        private const int StableWorldPlayerFrames = 2;
        private static readonly string[] LiveWorldRootNames =
        {
            "BoardView",
            "ProjectilePool",
            "ImpactFxPool",
            "ActiveSlots",
            "UnitQueues"
        };
        private static readonly CaptureSpec[] Specs =
        {
            new(1080, 1920, "initial-1080x1920.png"),
            new(720, 1600, "initial-720x1600.png"),
            new(1170, 2532, "initial-1170x2532.png")
        };

        private static int _captureIndex;
        private static int _settledPlayerFrames;
        private static int _stableWorldPlayerFrames;
        private static int _lastObservedPlayerFrame = -1;
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
        private static bool _pendingPreviousOrthographic;
        private static float _pendingPreviousOrthographicSize;
        private static float _pendingPreviousFieldOfView;
        private static float _pendingPreviousNearClip;
        private static float _pendingPreviousFarClip;
        private static bool _pendingPreviousHdr;
        private static bool _pendingPreviousMsaa;
        private static Vector3 _pendingPreviousPosition;
        private static Quaternion _pendingPreviousRotation;
        private static AsyncGPUReadbackRequest _pendingReadback;
        private static bool _pendingReadbackActive;
        private static ScriptedOutcome _scriptedOutcome;
        private static int[] _scriptedLaneChoices;
        private static int _scriptedChoiceIndex;
        private static double _nextScriptedChoiceAt;
        private static double _scriptedOutcomeTimeoutAt;
        private static double _scriptedOutcomeReachedAt;

        static VisualCapture()
        {
            if (SessionState.GetBool(ActiveKey, false)) AttachStateHandler();
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        public static void Run()
        {
            RunInternal(GetArgument("-captureQuality"));
        }

        /// <summary>
        /// Runs the complete audit through the Mobile quality level and its
        /// Mobile_RPAsset, then restores the Editor's original quality level.
        /// </summary>
        public static void RunMobile()
        {
            RunInternal("Mobile");
        }

        private static void RunInternal(string requestedQuality)
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                throw new InvalidOperationException("A visual capture session is already active.");
            }

            string output = GetArgument("-captureOutput");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/Audit/PolishPass"));
            }

            Directory.CreateDirectory(output);
            try
            {
                ConfigureQualityOverride(requestedQuality);
                SessionState.SetString(OutputKey, output);
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetBool(FinishingKey, false);
                SessionState.SetInt(ExitCodeKey, 0);
                ResetCaptureState();
                AttachStateHandler();
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch
            {
                SessionState.SetBool(ActiveKey, false);
                RestoreQualityOverride();
                throw;
            }
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
                try
                {
                    VerifyQualityOverride();
                    ResetCaptureState();
                    ApplyResolution(Specs[_captureIndex]);
                    EditorApplication.update -= Tick;
                    EditorApplication.update += Tick;
                }
                catch (Exception exception)
                {
                    FailCapture(exception);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishingKey, false))
            {
                int exitCode = SessionState.GetInt(ExitCodeKey, 1);
                RestoreQualityOverride();
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(FinishingKey, false);
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.Exit(exitCode);
            }
        }

        private static void Tick()
        {
            try
            {
                TickCapture();
            }
            catch (Exception exception)
            {
                FailCapture(exception);
            }
        }

        private static void TickCapture()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (!ObservePlayerFrame()) return;

            if (_layoutRefreshPending && _settledPlayerFrames >= ResolutionApplyPlayerFrames)
            {
                ReloadCurrentLevelForResolution();
                _layoutRefreshPending = false;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                BeginPlayerFrameWait(true);
                return;
            }

            // LoadLevel uses deferred Destroy for the previous world's roots. Waiting for
            // real player frames and then seeing exactly one complete root set for two
            // consecutive frames prevents old and new levels from sharing a capture.
            if (_stableWorldPlayerFrames < StableWorldPlayerFrames) return;
            if (_scriptedOutcome != ScriptedOutcome.None && !AdvanceScriptedOutcome()) return;
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
                _settledPlayerFrames < 18) return;

            if (_captureIndex < Specs.Length)
            {
                if (!Capture(Specs[_captureIndex])) return;
                _captureIndex++;
                if (_captureIndex < Specs.Length)
                {
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
            ApplyResolution(new CaptureSpec(1080, 1920, "level3-layers-initial-1080x1920.png"));
        }

        private static void PrepareLevelFive()
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            if (controller == null || loadLevel == null) throw new InvalidOperationException("Unable to prepare Level 5 visual capture.");
            loadLevel.Invoke(controller, new object[] { 5 });
            _preparedExtendedStage = true;
            _stageStartedAt = EditorApplication.timeSinceStartup;
            // The supplied IMG_5534 reference is exactly 1170 x 2532. Capture the
            // densest ten-column board at that native aspect first so camera,
            // spacing, frame clearance, and stacked-layer exposure can be compared
            // pixel-for-pixel before the regular 1080 x 1920 validation frame.
            ApplyResolution(new CaptureSpec(1170, 2532, "level5-dense-initial-1170x2532.png"));
        }

        private static void PrepareSafeAreaSimulation()
        {
            SafeAreaFitter fitter = UnityEngine.Object.FindFirstObjectByType<SafeAreaFitter>();
            if (fitter == null) throw new InvalidOperationException("Safe-area visual capture requires SafeAreaFitter.");
            fitter.SetEditorNormalizedSafeArea(new Rect(0.035f, 0.055f, 0.93f, 0.89f));
            _preparedExtendedStage = true;
            ApplyResolution(new CaptureSpec(1170, 2532, "safearea-sim-1170x2532.png"));
        }

        private static void RestoreSafeAreaSimulation()
        {
            SafeAreaFitter fitter = UnityEngine.Object.FindFirstObjectByType<SafeAreaFitter>();
            if (fitter == null) return;
            fitter.ClearEditorNormalizedSafeArea();
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
                _stageStartedAt = EditorApplication.timeSinceStartup;
                BeginPlayerFrameWait(false);
                return;
            }

            if (_captureIndex == Specs.Length + 2)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level3-action-200ms-1080x1920.png"))) return;
                _captureIndex++;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                BeginPlayerFrameWait(false);
                return;
            }

            if (_captureIndex == Specs.Length + 3)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level3-action-430ms-1080x1920.png"))) return;
                _captureIndex++;
                _stageStartedAt = EditorApplication.timeSinceStartup;
                BeginPlayerFrameWait(false);
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
                if (!Capture(new CaptureSpec(1170, 2532, "level5-dense-initial-1170x2532.png"))) return;
                _captureIndex++;
                ApplyResolution(new CaptureSpec(1080, 1920, "level5-dense-initial-1080x1920.png"));
                BeginPlayerFrameWait(false);
                return;
            }

            if (_captureIndex == Specs.Length + 6)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "level5-dense-initial-1080x1920.png"))) return;
                _captureIndex++;
                PrepareScriptedOutcome(ScriptedOutcome.Win);
                return;
            }

            if (_captureIndex == Specs.Length + 7)
            {
                if (!Capture(new CaptureSpec(1080, 1920, "win-popup-1080x1920.png"))) return;
                _captureIndex++;
                PrepareScriptedOutcome(ScriptedOutcome.Loss);
                return;
            }

            if (_captureIndex != Specs.Length + 8)
            {
                throw new InvalidOperationException($"Unexpected visual capture stage {_captureIndex}.");
            }

            if (!Capture(new CaptureSpec(1080, 1920, "loss-popup-1080x1920.png"))) return;

            FinishCapture(0);
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

        /// <summary>
        /// Produces truthful outcome screenshots by driving the same live selection path a
        /// player would use. A staged HUD overlay over an uncleared board can hide state bugs
        /// and is not useful as a final QA artifact.
        /// </summary>
        private static void PrepareScriptedOutcome(ScriptedOutcome outcome)
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            if (controller == null || loadLevel == null || catalog == null)
            {
                throw new InvalidOperationException("Unable to prepare a live result-state capture.");
            }

            int levelNumber;
            if (outcome == ScriptedOutcome.Win)
            {
                levelNumber = 1;
                LevelSolveResult solution = LevelSolver.Solve(catalog.GetLevel(levelNumber));
                if (!solution.IsSolvable || solution.WinningLaneChoices.Count == 0)
                {
                    throw new InvalidOperationException("Level 1 has no live visual-capture win path.");
                }

                _scriptedLaneChoices = new int[solution.WinningLaneChoices.Count];
                for (int i = 0; i < _scriptedLaneChoices.Length; i++)
                {
                    _scriptedLaneChoices[i] = solution.WinningLaneChoices[i];
                }
            }
            else if (outcome == ScriptedOutcome.Loss)
            {
                levelNumber = 2;
                _scriptedLaneChoices = new[] { 2, 3, 4, 5, 2 };
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }

            loadLevel.Invoke(controller, new object[] { levelNumber });
            _scriptedOutcome = outcome;
            _scriptedChoiceIndex = 0;
            _nextScriptedChoiceAt = EditorApplication.timeSinceStartup + 0.20d;
            _scriptedOutcomeTimeoutAt = EditorApplication.timeSinceStartup +
                (outcome == ScriptedOutcome.Win ? 18d : 6d);
            _scriptedOutcomeReachedAt = -1d;
            _stageStartedAt = EditorApplication.timeSinceStartup;
            BeginPlayerFrameWait(true);
        }

        private static bool AdvanceScriptedOutcome()
        {
            GameController controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller == null) throw new InvalidOperationException("Live result capture lost its GameController.");

            double now = EditorApplication.timeSinceStartup;
            GameFlowState expected = _scriptedOutcome == ScriptedOutcome.Win
                ? GameFlowState.Won
                : GameFlowState.Lost;
            if (controller.State == expected)
            {
                if (_scriptedOutcomeReachedAt < 0d) _scriptedOutcomeReachedAt = now;
                if (now - _scriptedOutcomeReachedAt < 0.45d) return false;

                _scriptedOutcome = ScriptedOutcome.None;
                _scriptedLaneChoices = null;
                _stageStartedAt = now;
                BeginPlayerFrameWait(false);
                return false;
            }

            if (now > _scriptedOutcomeTimeoutAt)
            {
                throw new TimeoutException(
                    $"Live {_scriptedOutcome} capture did not reach {expected}; " +
                    $"state is {controller.State}, choice {_scriptedChoiceIndex}/{_scriptedLaneChoices?.Length ?? 0}.");
            }

            if (controller.State != GameFlowState.Playing ||
                _scriptedLaneChoices == null ||
                _scriptedChoiceIndex >= _scriptedLaneChoices.Length ||
                now < _nextScriptedChoiceAt)
            {
                return false;
            }

            int oneBasedLane = _scriptedLaneChoices[_scriptedChoiceIndex];
            SelectLaneFront(oneBasedLane);
            _scriptedChoiceIndex++;
            _nextScriptedChoiceAt = now +
                (_scriptedOutcome == ScriptedOutcome.Win ? 1.05d : 0.08d);
            return false;
        }

        private static void SelectLaneFront(int oneBasedLane)
        {
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            Camera camera = Camera.main;
            if (presentation == null || camera == null)
            {
                throw new InvalidOperationException("Reference layout is unavailable for scripted selection.");
            }

            GameplayLayout layout = presentation.FeelProfile.ResolveLayout(10, 10, camera.aspect);
            float targetX = layout.QueueX(oneBasedLane - 1, LevelSolver.LaneCount);
            UnitClickTarget[] targets = UnityEngine.Object.FindObjectsByType<UnitClickTarget>(FindObjectsSortMode.None);
            UnitClickTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < targets.Length; i++)
            {
                Collider collider = targets[i].GetComponent<Collider>();
                if (collider == null || !collider.enabled) continue;
                float distance = Mathf.Abs(targets[i].transform.position.x - targetX);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = targets[i];
            }

            if (best == null || bestDistance >= 0.10f)
            {
                throw new InvalidOperationException($"Lane {oneBasedLane} has no selectable front unit.");
            }

            best.NotifyClick();
        }

        private static void ApplyResolution(CaptureSpec spec)
        {
            Screen.SetResolution(spec.Width, spec.Height, FullScreenMode.Windowed);
            _requestedAspect = spec.Width / (float)spec.Height;
            _layoutRefreshPending = true;
            BeginPlayerFrameWait(true);
            _stageStartedAt = EditorApplication.timeSinceStartup;
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

                Texture2D image = new(spec.Width, spec.Height, TextureFormat.RGBA32, false, false);
                try
                {
                    image.LoadRawTextureData(_pendingReadback.GetData<byte>());
                    image.Apply(false, false);
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    Debug.Log($"[VisualCapture] Captured completed URP frame {path}.");
                }
                finally
                {
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
            _pendingPreviousOrthographic = camera.orthographic;
            _pendingPreviousOrthographicSize = camera.orthographicSize;
            _pendingPreviousFieldOfView = camera.fieldOfView;
            _pendingPreviousNearClip = camera.nearClipPlane;
            _pendingPreviousFarClip = camera.farClipPlane;
            _pendingPreviousHdr = camera.allowHDR;
            _pendingPreviousMsaa = camera.allowMSAA;
            _pendingPreviousPosition = camera.transform.position;
            _pendingPreviousRotation = camera.transform.rotation;
            camera.aspect = spec.Width / (float)spec.Height;
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            if (presentation == null)
            {
                ReleasePendingCapture();
                throw new InvalidOperationException("Visual capture requires PresentationAssets.");
            }

            PresentationCameraRig.Configure(camera, presentation.FeelProfile, camera.aspect);
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
                TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int textIndex = 0; textIndex < texts.Length; textIndex++)
                {
                    if (!texts[textIndex].gameObject.activeInHierarchy) continue;
                    texts[textIndex].ForceMeshUpdate(true, true);
                    texts[textIndex].UpdateGeometry(texts[textIndex].mesh, 0);
                }
                Canvas.ForceUpdateCanvases();
            }

            RenderTexture requestPreviousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, camera.backgroundColor);
                camera.Render();
                _pendingReadback = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
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
                _pendingCamera.orthographic = _pendingPreviousOrthographic;
                _pendingCamera.orthographicSize = _pendingPreviousOrthographicSize;
                _pendingCamera.fieldOfView = _pendingPreviousFieldOfView;
                _pendingCamera.nearClipPlane = _pendingPreviousNearClip;
                _pendingCamera.farClipPlane = _pendingPreviousFarClip;
                _pendingCamera.allowHDR = _pendingPreviousHdr;
                _pendingCamera.allowMSAA = _pendingPreviousMsaa;
                _pendingCamera.transform.SetPositionAndRotation(
                    _pendingPreviousPosition,
                    _pendingPreviousRotation);
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

        private static void ResetCaptureState()
        {
            _captureIndex = 0;
            _preparedExtendedStage = false;
            _layoutRefreshPending = false;
            _pendingCapturePath = null;
            _scriptedOutcome = ScriptedOutcome.None;
            _scriptedLaneChoices = null;
            _scriptedChoiceIndex = 0;
            _nextScriptedChoiceAt = 0d;
            _scriptedOutcomeTimeoutAt = 0d;
            _scriptedOutcomeReachedAt = -1d;
            _stageStartedAt = EditorApplication.timeSinceStartup;
            BeginPlayerFrameWait(true);
        }

        private static void BeginPlayerFrameWait(bool resetWorldStability)
        {
            _settledPlayerFrames = 0;
            _lastObservedPlayerFrame = EditorApplication.isPlaying ? Time.frameCount : -1;
            if (resetWorldStability) _stableWorldPlayerFrames = 0;
        }

        private static bool ObservePlayerFrame()
        {
            int currentFrame = Time.frameCount;
            if (_lastObservedPlayerFrame < 0)
            {
                _lastObservedPlayerFrame = currentFrame;
                return false;
            }

            if (currentFrame == _lastObservedPlayerFrame) return false;

            int elapsedFrames = currentFrame > _lastObservedPlayerFrame
                ? currentFrame - _lastObservedPlayerFrame
                : 1;
            _lastObservedPlayerFrame = currentFrame;
            _settledPlayerFrames += elapsedFrames;

            if (HasExactlyOneLiveWorld())
            {
                // Count observations instead of the frame delta. This proves the world was
                // complete on separate player-loop iterations even if an Editor update was
                // skipped while the GPU or asset pipeline was busy.
                _stableWorldPlayerFrames++;
            }
            else
            {
                _stableWorldPlayerFrames = 0;
            }

            return true;
        }

        private static bool HasExactlyOneLiveWorld()
        {
            int[] counts = new int[LiveWorldRootNames.Length];
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform candidate = transforms[transformIndex];
                if (candidate.parent != null || !candidate.gameObject.scene.IsValid()) continue;

                for (int nameIndex = 0; nameIndex < LiveWorldRootNames.Length; nameIndex++)
                {
                    if (!string.Equals(candidate.name, LiveWorldRootNames[nameIndex], StringComparison.Ordinal)) continue;
                    counts[nameIndex]++;
                    break;
                }
            }

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] != 1) return false;
            }

            return true;
        }

        private static void ConfigureQualityOverride(string requestedQuality)
        {
            SessionState.SetBool(QualityOverrideKey, false);
            SessionState.SetInt(OriginalQualityKey, -1);
            SessionState.SetInt(RequestedQualityKey, -1);
            if (string.IsNullOrWhiteSpace(requestedQuality)) return;

            string[] qualityNames = QualitySettings.names;
            int requestedIndex = Array.FindIndex(
                qualityNames,
                name => string.Equals(name, requestedQuality, StringComparison.OrdinalIgnoreCase));
            if (requestedIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Unknown capture quality '{requestedQuality}'. Available levels: {string.Join(", ", qualityNames)}.");
            }

            int originalIndex = QualitySettings.GetQualityLevel();
            SessionState.SetInt(OriginalQualityKey, originalIndex);
            SessionState.SetInt(RequestedQualityKey, requestedIndex);
            SessionState.SetBool(QualityOverrideKey, true);
            QualitySettings.SetQualityLevel(requestedIndex, true);
            VerifyQualityOverride();
            Debug.Log(
                $"[VisualCapture] Quality override {qualityNames[originalIndex]} -> {qualityNames[requestedIndex]}, " +
                $"pipeline {PipelineName(GraphicsSettings.currentRenderPipeline)}.");
        }

        private static void VerifyQualityOverride()
        {
            if (!SessionState.GetBool(QualityOverrideKey, false)) return;

            int requestedIndex = SessionState.GetInt(RequestedQualityKey, -1);
            string[] qualityNames = QualitySettings.names;
            if (requestedIndex < 0 || requestedIndex >= qualityNames.Length)
            {
                throw new InvalidOperationException("The requested capture quality level is no longer available.");
            }

            if (QualitySettings.GetQualityLevel() != requestedIndex)
            {
                QualitySettings.SetQualityLevel(requestedIndex, true);
            }

            if (!string.Equals(qualityNames[requestedIndex], "Mobile", StringComparison.OrdinalIgnoreCase)) return;

            RenderPipelineAsset expectedPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MobilePipelinePath);
            if (expectedPipeline == null)
            {
                throw new InvalidOperationException($"Mobile render pipeline is missing at {MobilePipelinePath}.");
            }

            RenderPipelineAsset activePipeline = GraphicsSettings.currentRenderPipeline;
            if (activePipeline != expectedPipeline)
            {
                throw new InvalidOperationException(
                    $"Mobile QA requested {expectedPipeline.name}, but the active pipeline is {PipelineName(activePipeline)}.");
            }
        }

        private static void RestoreQualityOverride()
        {
            if (!SessionState.GetBool(QualityOverrideKey, false)) return;

            int originalIndex = SessionState.GetInt(OriginalQualityKey, -1);
            string[] qualityNames = QualitySettings.names;
            if (originalIndex >= 0 && originalIndex < qualityNames.Length)
            {
                QualitySettings.SetQualityLevel(originalIndex, true);
                Debug.Log(
                    $"[VisualCapture] Restored quality level {qualityNames[originalIndex]}, " +
                    $"pipeline {PipelineName(GraphicsSettings.currentRenderPipeline)}.");
            }
            else
            {
                Debug.LogError("[VisualCapture] Could not restore the original quality level because its index is invalid.");
            }

            SessionState.SetBool(QualityOverrideKey, false);
            SessionState.SetInt(OriginalQualityKey, -1);
            SessionState.SetInt(RequestedQualityKey, -1);
        }

        private static string PipelineName(RenderPipelineAsset pipeline)
        {
            return pipeline != null ? pipeline.name : "Built-in Render Pipeline";
        }

        private static void FinishCapture(int exitCode)
        {
            EditorApplication.update -= Tick;
            ReleasePendingCapture();
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetBool(FinishingKey, true);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
            else
            {
                RestoreQualityOverride();
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(FinishingKey, false);
                EditorApplication.Exit(exitCode);
            }
        }

        private static void FailCapture(Exception exception)
        {
            Debug.LogException(exception);
            FinishCapture(1);
        }

        private static void OnEditorQuitting()
        {
            ReleasePendingCapture();
            RestoreQualityOverride();
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
