using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlocks.Core;
using ColorBlocks.Gameplay;
using ColorBlocks.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorBlocks.Tests
{
    public sealed class GameStartupTests
    {
        [UnityTest]
        public IEnumerator GameplayScene_StartsInPlayableStateWithoutErrors()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.State, Is.EqualTo(ColorBlocks.Core.GameFlowState.Playing));
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.False);
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.ProjectileLitTemplate, Is.Not.Null);
            Assert.That(presentation.ProjectileLitTemplate.IsKeywordEnabled("_EMISSION"), Is.True,
                "The serialized projectile template must anchor the Lit emission shader variant for iOS builds.");
            Assert.That(camera.fieldOfView, Is.EqualTo(presentation.FeelProfile.CameraFieldOfView).Within(0.01f));
            Assert.That(camera.transform.forward.z, Is.InRange(0.86f, 0.88f));
            Assert.That(camera.transform.forward.y, Is.EqualTo(Mathf.Sin(29.5f * Mathf.Deg2Rad)).Within(0.003f),
                "The camera must retain the measured across-tray pitch so physical stack height reads in 3D.");

            Light mainLight = GameObject.Find("Main Light")?.GetComponent<Light>();
            Assert.That(mainLight, Is.Not.Null);
            Assert.That(mainLight.type, Is.EqualTo(LightType.Directional));
            Assert.That(mainLight.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(mainLight.shadowStrength, Is.GreaterThan(0.25f));

            Light fillLight = GameObject.Find("Fill Light")?.GetComponent<Light>();
            Assert.That(fillLight, Is.Not.Null);
            Assert.That(fillLight.type, Is.EqualTo(LightType.Directional));
            Assert.That(fillLight.shadows, Is.EqualTo(LightShadows.None));

            MeshRenderer backdrop = GameObject.Find("SceneBackdrop")?.GetComponent<MeshRenderer>();
            MeshRenderer boardInner = GameObject.Find("BoardInner")?.GetComponent<MeshRenderer>();
            MeshRenderer blockBody = FindFirstVisibleBlock()?.GetComponent<MeshRenderer>();
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(backdrop.receiveShadows, Is.True);
            Assert.That(boardInner, Is.Not.Null);
            Assert.That(boardInner.receiveShadows, Is.True);
            Assert.That(blockBody, Is.Not.Null);
            Assert.That(blockBody.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(blockBody.receiveShadows, Is.True);
            Assert.That(Screen.orientation, Is.EqualTo(ScreenOrientation.Portrait));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SceneLighting_EnsuresMainAndFillIndependentlyOfOtherLights()
        {
            GameObject preexistingFill = new("Fill Light");
            Object.DontDestroyOnLoad(preexistingFill);
            Light fill = preexistingFill.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.enabled = false;

            GameObject unrelatedObject = new("Unrelated Test Light");
            Object.DontDestroyOnLoad(unrelatedObject);
            Light unrelated = unrelatedObject.AddComponent<Light>();
            unrelated.type = LightType.Spot;

            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            Light main = GameObject.Find("Main Light")?.GetComponent<Light>();
            Light configuredFill = GameObject.Find("Fill Light")?.GetComponent<Light>();
            Assert.That(main, Is.Not.Null,
                "An unrelated Light must not prevent creation of the role-specific Main Light.");
            Assert.That(configuredFill, Is.SameAs(fill),
                "An existing named Fill Light should be repaired rather than duplicated.");
            Assert.That(main.type, Is.EqualTo(LightType.Directional));
            Assert.That(main.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(configuredFill.enabled, Is.True);
            Assert.That(configuredFill.type, Is.EqualTo(LightType.Directional));
            Assert.That(configuredFill.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(unrelated.type, Is.EqualTo(LightType.Spot));

            Object.Destroy(preexistingFill);
            Object.Destroy(unrelatedObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitAimAt_AlignsAuthoredBarrelAxisWithWorldSpace3dTarget()
        {
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.IsValid(out string validationError), Is.True, validationError);

            VisualTheme theme = new(presentation);
            GameObject parent = new("UnitAim3DTestRoot");
            parent.transform.rotation = Quaternion.Euler(7f, 21f, -9f);
            UnitView unit = new(
                parent.transform,
                BlockColorId.Red,
                5,
                new Vector3(0.4f, -3.8f, 0.15f),
                theme,
                presentation.FeelProfile);

            Transform pivot = unit.Transform.Find("TurretPivot");
            Assert.That(pivot, Is.Not.Null);
            Vector3 targetDirection = new Vector3(1.25f, 4.2f, 1.75f).normalized;
            unit.AimAt(pivot.position + targetDirection * 5f);
            yield return null;

            Vector3 aimedAxis = pivot.TransformDirection(Vector3.up).normalized;
            Assert.That(Vector3.Dot(aimedAxis, targetDirection), Is.GreaterThan(0.9999f),
                "The model's local +Y barrel axis must point at the full Vector3 target.");
            Assert.That(Mathf.Abs(aimedAxis.z), Is.GreaterThan(0.25f),
                "The aim must include visible depth pitch rather than only an XY rotation.");

            foreach (BlockColorId id in System.Enum.GetValues(typeof(BlockColorId)))
            {
                Assert.That(theme.Projectile(id).IsKeywordEnabled("_EMISSION"), Is.True,
                    $"{id} projectile must inherit the serialized emission variant.");
            }

            unit.Destroy();
            theme.Dispose();
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PerspectiveCamera_RaycastHitsSelectableFrontUnit()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            UnitClickTarget[] targets = Object.FindObjectsByType<UnitClickTarget>(FindObjectsSortMode.None);
            UnitClickTarget selectable = null;
            Collider selectableCollider = null;
            for (int i = 0; i < targets.Length; i++)
            {
                Collider candidate = targets[i].GetComponent<Collider>();
                if (candidate == null || !candidate.enabled) continue;
                selectable = targets[i];
                selectableCollider = candidate;
                break;
            }

            Assert.That(selectable, Is.Not.Null);
            Assert.That(selectableCollider, Is.Not.Null);
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Physics.SyncTransforms();
            Vector3 screenPoint = camera.WorldToScreenPoint(selectableCollider.bounds.center);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));
            Ray ray = camera.ScreenPointToRay(screenPoint);
            Assert.That(Physics.Raycast(ray, out RaycastHit hit, camera.farClipPlane), Is.True);
            Assert.That(hit.collider.GetComponentInParent<UnitClickTarget>(), Is.SameAs(selectable));
        }

        [UnityTest]
        public IEnumerator LevelThree_RendersCoveredLayersAndProjectilesWithRealDepth()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(controller, Is.Not.Null);
            Assert.That(loadLevel, Is.Not.Null);
            loadLevel.Invoke(controller, new object[] { 3 });
            yield return null;

            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            LevelDefinition level = catalog.GetLevel(3);
            CellDefinition layeredCell = null;
            for (int i = 0; i < level.Cells.Count; i++)
            {
                if (level.Cells[i].Layers.Count <= 1) continue;
                layeredCell = level.Cells[i];
                break;
            }

            Assert.That(layeredCell, Is.Not.Null);
            int topLayerIndex = layeredCell.Layers.Count - 1;
            string coveredName = $"Block_{layeredCell.X}_{layeredCell.Y}_0_{layeredCell.Layers[0].Color}";
            string exposedName = $"Block_{layeredCell.X}_{layeredCell.Y}_{topLayerIndex}_{layeredCell.Layers[topLayerIndex].Color}";
            Transform coveredLayer = GameObject.Find(coveredName)?.transform;
            Transform exposedLayer = GameObject.Find(exposedName)?.transform;
            Assert.That(coveredLayer, Is.Not.Null);
            Assert.That(exposedLayer, Is.Not.Null);

            float physicalDepth = coveredLayer.position.z - exposedLayer.position.z;
            GameFeelProfile feel = presentation.FeelProfile;
            Assert.That(physicalDepth, Is.EqualTo(feel.HiddenLayerDepth * topLayerIndex).Within(0.01f));
            Assert.That(physicalDepth, Is.GreaterThanOrEqualTo(feel.BlockDepth * 0.75f));
            Assert.That(exposedLayer.position.y, Is.EqualTo(coveredLayer.position.y).Within(0.001f),
                "Stack layers share board-space Y; the camera must reveal their real Z separation.");
            Assert.That(exposedLayer.lossyScale.z, Is.EqualTo(feel.BlockDepth).Within(0.01f));
            MeshRenderer coveredBody = coveredLayer.GetComponent<MeshRenderer>();
            MeshRenderer exposedBody = exposedLayer.GetComponent<MeshRenderer>();
            MeshRenderer boardInner = GameObject.Find("BoardInner")?.GetComponent<MeshRenderer>();
            Assert.That(coveredBody, Is.Not.Null);
            Assert.That(exposedBody, Is.Not.Null);
            Assert.That(boardInner, Is.Not.Null);
            Assert.That(exposedBody.bounds.max.z, Is.EqualTo(coveredBody.bounds.min.z).Within(0.002f),
                "The upper cube must sit directly on the base cube without overlap or an air gap.");
            Assert.That(coveredBody.bounds.max.z, Is.EqualTo(boardInner.bounds.min.z).Within(0.002f),
                "The base cube must rest directly on the tray surface instead of floating.");
            Vector3 exposedViewport = Camera.main.WorldToViewportPoint(exposedLayer.position);
            Vector3 coveredViewport = Camera.main.WorldToViewportPoint(coveredLayer.position);
            float suppliedIPhonePixelSeparation =
                Mathf.Abs(exposedViewport.y - coveredViewport.y) * 2532f;
            Assert.That(suppliedIPhonePixelSeparation, Is.GreaterThan(35f),
                "Physical stack height must expose a clearly visible lower cube band.");

            Transform coveredFace = coveredLayer.Find("FaceInset");
            Transform exposedFace = exposedLayer.Find("FaceInset");
            Assert.That(coveredFace, Is.Not.Null);
            Assert.That(exposedFace, Is.Not.Null);
            Assert.That(coveredFace.gameObject.activeSelf, Is.False,
                "A covered cube's top inset shares the contact plane and must not intersect the cube above it.");
            Assert.That(exposedFace.gameObject.activeSelf, Is.True);
            Assert.That(exposedBody.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(exposedBody.receiveShadows, Is.True);

            Transform projectilePool = GameObject.Find("ProjectilePool")?.transform;
            Assert.That(projectilePool, Is.Not.Null);
            Assert.That(projectilePool.childCount, Is.GreaterThan(0));
            Transform projectile = projectilePool.GetChild(0);
            MeshFilter projectileMesh = projectile.GetComponent<MeshFilter>();
            Transform tracer = projectile.Find("Tracer");
            Assert.That(projectileMesh, Is.Not.Null);
            Assert.That(projectileMesh.sharedMesh.bounds.size.z, Is.GreaterThan(0.5f),
                "Projectile body must be volumetric rather than an XY disc.");
            Assert.That(tracer, Is.Not.Null);
            Assert.That(tracer.localScale.z, Is.GreaterThan(0.1f),
                "Projectile tracer must retain visible 3D thickness.");
        }

        [UnityTest]
        public IEnumerator PhysicalStacks_RemainSupportedDuringFallAndInterruptedReveal()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod(
                "LoadLevel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo boardField = typeof(GameController).GetField(
                "_board",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo boardViewField = typeof(GameController).GetField(
                "_boardView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(controller, Is.Not.Null);
            Assert.That(loadLevel, Is.Not.Null);
            Assert.That(boardField, Is.Not.Null);
            Assert.That(boardViewField, Is.Not.Null);

            loadLevel.Invoke(controller, new object[] { 3 });
            yield return null;

            BoardModel board = (BoardModel)boardField.GetValue(controller);
            BoardView boardView = (BoardView)boardViewField.GetValue(controller);
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            Assert.That(board, Is.Not.Null);
            Assert.That(boardView, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);

            BlockStackModel layeredStack = null;
            for (int stackIndex = 0; stackIndex < board.Stacks.Count; stackIndex++)
            {
                BlockStackModel stack = board.Stacks[stackIndex];
                if (stack.Layers.Count < 2) continue;
                layeredStack ??= stack;

                for (int layerIndex = 1; layerIndex < stack.Layers.Count; layerIndex++)
                {
                    BlockView lower = boardView.Get(stack.Layers[layerIndex - 1]);
                    BlockView upper = boardView.Get(stack.Layers[layerIndex]);
                    AssertSupportedStackPair(lower, upper, presentation.FeelProfile,
                        $"spawned stack {stack.Position}, layers {layerIndex - 1}/{layerIndex}");
                }
            }

            Assert.That(layeredStack, Is.Not.Null, "Level 3 must contain a physical double stack.");
            BlockView baseView = boardView.Get(layeredStack.Layers[0]);
            BlockView topView = boardView.Get(layeredStack.Layers[1]);
            Vector3 firstDestination = boardView.ToWorld(layeredStack.Position) +
                Vector3.down * boardView.CellPitchY;
            const float fallDelay = 0.04f;
            const float fallDuration = 0.20f;
            controller.StartCoroutine(baseView.FallTo(firstDestination, fallDuration, fallDelay));
            controller.StartCoroutine(topView.FallTo(firstDestination, fallDuration, fallDelay));

            float fallTimeout = Time.realtimeSinceStartup + fallDelay + fallDuration + 0.08f;
            while (Time.realtimeSinceStartup < fallTimeout)
            {
                AssertSupportedStackPair(
                    baseView,
                    topView,
                    presentation.FeelProfile,
                    "intact stack fall");
                yield return null;
            }

            Vector3 secondDestination = firstDestination + Vector3.down * boardView.CellPitchY;
            const float interruptedDelay = 0.20f;
            controller.StartCoroutine(baseView.FallTo(secondDestination, fallDuration, interruptedDelay));
            topView.HideDestroyed();
            controller.StartCoroutine(baseView.Reveal(0.08f));

            float revealTimeout = Time.realtimeSinceStartup + 0.14f;
            MeshRenderer boardInner = GameObject.Find("BoardInner")?.GetComponent<MeshRenderer>();
            MeshRenderer baseRenderer = baseView.Transform.GetComponent<MeshRenderer>();
            Assert.That(boardInner, Is.Not.Null);
            Assert.That(baseRenderer, Is.Not.Null);
            while (Time.realtimeSinceStartup < revealTimeout)
            {
                Assert.That(topView.Transform.gameObject.activeSelf, Is.False,
                    "The removed upper cube must never remain visible without its base.");
                Assert.That(baseView.Transform.localScale.z,
                    Is.EqualTo(presentation.FeelProfile.BlockDepth).Within(0.0001f),
                    "Reveal must not squash the support axis.");
                Assert.That(baseRenderer.bounds.max.z, Is.EqualTo(boardInner.bounds.min.z).Within(0.002f),
                    "The revealed base cube must remain on the tray throughout its transition.");
                yield return null;
            }

            Vector3 expectedBasePosition = secondDestination +
                new Vector3(0f, 0f, presentation.FeelProfile.BlockBoardDepth);
            Assert.That(baseView.Transform.position.x, Is.EqualTo(expectedBasePosition.x).Within(0.001f));
            Assert.That(baseView.Transform.position.y, Is.EqualTo(expectedBasePosition.y).Within(0.001f),
                "Reveal interrupted during the fall delay must settle at the new logical cell.");
            Assert.That(baseView.Transform.position.z, Is.EqualTo(expectedBasePosition.z).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator SelectingFrontUnit_DestroysBlocksAndRestartRestoresBoard()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            int initialBlockCount = CountVisibleBlocks();
            Dictionary<string, float> initialBlockY = CaptureVisibleBlockY();
            Assert.That(initialBlockCount, Is.GreaterThan(0));

            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            LevelSolveResult solution = LevelSolver.Solve(catalog.GetLevel(1));
            Assert.That(solution.WinningLaneChoices, Is.Not.Empty);
            SelectLaneFront(solution.WinningLaneChoices[0]);
            yield return new WaitForSeconds(1.25f);
            Assert.That(CountVisibleBlocks(), Is.LessThan(initialBlockCount));
            Assert.That(HasBlockMovedDown(initialBlockY), Is.True,
                "Clearing a bottom block must visibly compact at least one surviving block downward.");

            controller.SendMessage("RestartCurrentLevel", SendMessageOptions.RequireReceiver);
            yield return null;
            yield return new WaitForSeconds(0.10f);
            Assert.That(CountVisibleBlocks(), Is.EqualTo(initialBlockCount));
            Assert.That(controller.State, Is.EqualTo(ColorBlocks.Core.GameFlowState.Playing));
        }

        [UnityTest]
        public IEnumerator SolverSequence_CompletesLevelOneInLiveGameplay()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            LevelSolveResult solution = LevelSolver.Solve(catalog.GetLevel(1));
            for (int i = 0; i < solution.WinningLaneChoices.Count; i++)
            {
                SelectLaneFront(solution.WinningLaneChoices[i]);
                yield return new WaitForSeconds(1.05f);
            }

            GameController controller = Object.FindFirstObjectByType<GameController>();
            float timeoutAt = Time.realtimeSinceStartup + 2f;
            while (controller.State != GameFlowState.Won && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(controller.State, Is.EqualTo(GameFlowState.Won));
            Assert.That(GameObject.Find("ResultOverlay"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SolverSequences_CompleteLevelsTwoThroughFiveInLiveGameplay()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod(
                "LoadLevel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            Assert.That(controller, Is.Not.Null);
            Assert.That(loadLevel, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            for (int levelNumber = 2; levelNumber <= 5; levelNumber++)
            {
                loadLevel.Invoke(controller, new object[] { levelNumber });
                yield return null;
                Assert.That(controller.State, Is.EqualTo(GameFlowState.Playing),
                    $"Level {levelNumber} must start in the playable state.");

                LevelSolveResult solution = LevelSolver.Solve(catalog.GetLevel(levelNumber));
                Assert.That(solution.IsSolvable, Is.True,
                    $"Level {levelNumber} must have a solver-proven winning path.");
                Assert.That(solution.WinningLaneChoices, Is.Not.Empty);

                for (int choiceIndex = 0; choiceIndex < solution.WinningLaneChoices.Count; choiceIndex++)
                {
                    Assert.That(controller.State, Is.EqualTo(GameFlowState.Playing),
                        $"Level {levelNumber} ended before solver choice {choiceIndex + 1} of " +
                        $"{solution.WinningLaneChoices.Count} could be selected.");
                    SelectLaneFront(solution.WinningLaneChoices[choiceIndex]);
                    yield return WaitForLiveGameplayToSettle(
                        controller,
                        5f,
                        $"Level {levelNumber}, solver choice {choiceIndex + 1}/" +
                        $"{solution.WinningLaneChoices.Count}");
                }

                float resultTimeoutAt = Time.realtimeSinceStartup + 3f;
                while (controller.State != GameFlowState.Won && Time.realtimeSinceStartup < resultTimeoutAt)
                {
                    yield return null;
                }

                Assert.That(controller.State, Is.EqualTo(GameFlowState.Won),
                    $"Level {levelNumber} did not reach the live Won state after its complete solver path.");
                Assert.That(GameObject.Find("ResultOverlay"), Is.Not.Null,
                    $"Level {levelNumber} must show its live completion overlay.");

                if (levelNumber == 2)
                {
                    UnityEngine.UI.Button action = GameObject.Find("Action")?.GetComponent<UnityEngine.UI.Button>();
                    Assert.That(action, Is.Not.Null);
                    int sessionBeforeAction = ReadSessionVersion(controller);
                    action.onClick.Invoke();
                    action.onClick.Invoke();
                    yield return null;
                    Assert.That(ReadSessionVersion(controller), Is.EqualTo(sessionBeforeAction + 1),
                        "A rapid repeated result action must advance exactly one level.");
                    Assert.That(controller.State, Is.EqualTo(GameFlowState.Playing),
                        "A rapid repeated result action must transition only once and leave gameplay valid.");
                    Assert.That(GameObject.Find("ResultOverlay"), Is.Null,
                        "The result action must hide the completion overlay before another click can act.");
                }
            }
        }

        [UnityTest]
        public IEnumerator RapidRepeatedInput_DoesNotDuplicateSelectionAndRestartRecoversCleanly()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            Assert.That(controller, Is.Not.Null);
            int initialBlockCount = CountVisibleBlocks();
            Assert.That(CountOccupiedRuntimeSlots(controller), Is.Zero);

            UnitClickTarget selected = SelectLaneFront(1);
            selected.NotifyClick();
            Assert.That(CountOccupiedRuntimeSlots(controller), Is.EqualTo(1),
                "Two taps on the same front unit in one frame must reserve only one slot.");

            int sessionBeforeRestart = ReadSessionVersion(controller);
            controller.SendMessage("RestartCurrentLevel", SendMessageOptions.RequireReceiver);
            controller.SendMessage("RestartCurrentLevel", SendMessageOptions.RequireReceiver);
            yield return null;
            yield return new WaitForSeconds(0.05f);

            Assert.That(ReadSessionVersion(controller), Is.EqualTo(sessionBeforeRestart + 1),
                "Two restart requests in one frame must rebuild the level exactly once.");
            Assert.That(controller.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(CountVisibleBlocks(), Is.EqualTo(initialBlockCount));
            Assert.That(CountOccupiedRuntimeSlots(controller), Is.Zero,
                "Rapid repeated restart input must not retain an active unit from the previous session.");
            Assert.That(CountSelectableFrontUnits(), Is.EqualTo(LevelSolver.LaneCount),
                "Restart must restore exactly one selectable front unit in each lane.");
        }

        [UnityTest]
        public IEnumerator FiveIncorrectLevelTwoSelections_ReachOutOfSpaceInLiveGameplay()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(loadLevel, Is.Not.Null);
            loadLevel.Invoke(controller, new object[] { 2 });
            yield return null;

            int[] losingChoices = { 2, 3, 4, 5, 2 };
            for (int i = 0; i < losingChoices.Length; i++)
            {
                SelectLaneFront(losingChoices[i]);
                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(1.10f);
            Assert.That(controller.State, Is.EqualTo(GameFlowState.Lost));
            Assert.That(GameObject.Find("ResultOverlay"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RapidFire_PreservesCadenceAndReturnsRecoilToRestPose()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.15f);

            GameController controller = Object.FindFirstObjectByType<GameController>();
            MethodInfo loadLevel = typeof(GameController).GetMethod("LoadLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(loadLevel, Is.Not.Null);
            loadLevel.Invoke(controller, new object[] { 3 });
            yield return null;

            LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            LevelSolveResult solution = LevelSolver.Solve(catalog.GetLevel(3));
            UnitClickTarget selected = SelectLaneFront(solution.WinningLaneChoices[0]);
            Transform unitRoot = selected.transform;
            Transform barrel = unitRoot.Find("TurretPivot/Barrel");
            Assert.That(barrel, Is.Not.Null);
            Vector3 barrelRest = barrel.localPosition;
            Quaternion rotationRest = unitRoot.localRotation;

            Dictionary<int, bool> projectileStates = new();
            List<float> shotTimes = new();
            float maximumBarrelDisplacement = 0f;
            float maximumRootAngle = 0f;
            float timeoutAt = Time.realtimeSinceStartup + 3f;
            while (shotTimes.Count < 9 && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
                maximumBarrelDisplacement = Mathf.Max(
                    maximumBarrelDisplacement,
                    Vector3.Distance(barrel.localPosition, barrelRest));
                maximumRootAngle = Mathf.Max(
                    maximumRootAngle,
                    Quaternion.Angle(unitRoot.localRotation, rotationRest));

                GameObject poolObject = GameObject.Find("ProjectilePool");
                Assert.That(poolObject, Is.Not.Null);
                Transform pool = poolObject.transform;
                for (int i = 0; i < pool.childCount; i++)
                {
                    GameObject projectile = pool.GetChild(i).gameObject;
                    int id = projectile.GetInstanceID();
                    bool wasActive = projectileStates.TryGetValue(id, out bool active) && active;
                    if (projectile.activeSelf && !wasActive) shotTimes.Add(Time.time);
                    projectileStates[id] = projectile.activeSelf;
                }
            }

            Assert.That(shotTimes, Has.Count.EqualTo(9));
            float firstSixAverage = (shotTimes[5] - shotTimes[0]) / 5f;
            Assert.That(firstSixAverage, Is.InRange(0.060f, 0.073f));
            Assert.That(maximumBarrelDisplacement, Is.LessThanOrEqualTo(0.111f));
            Assert.That(maximumRootAngle, Is.LessThanOrEqualTo(8.1f));

            yield return new WaitForSeconds(0.11f);
            Assert.That(Vector3.Distance(barrel.localPosition, barrelRest), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(unitRoot.localRotation, rotationRest), Is.LessThan(0.01f));
        }

        private static void AssertSupportedStackPair(
            BlockView lower,
            BlockView upper,
            GameFeelProfile feel,
            string context)
        {
            Assert.That(lower, Is.Not.Null, $"{context}: missing lower support cube.");
            Assert.That(upper, Is.Not.Null, $"{context}: missing upper cube.");
            Assert.That(lower.Transform.gameObject.activeSelf, Is.True,
                $"{context}: an active upper cube cannot exist above an inactive lower cube.");
            Assert.That(upper.Transform.gameObject.activeSelf, Is.True,
                $"{context}: expected the intact upper cube to remain active.");
            Assert.That(upper.Transform.position.x, Is.EqualTo(lower.Transform.position.x).Within(0.0001f),
                $"{context}: stacked cubes must share X.");
            Assert.That(upper.Transform.position.y, Is.EqualTo(lower.Transform.position.y).Within(0.0001f),
                $"{context}: stacked cubes must share Y.");
            Assert.That(lower.Transform.position.z - upper.Transform.position.z,
                Is.EqualTo(feel.BlockDepth).Within(0.0001f),
                $"{context}: adjacent cube centres must be separated by exactly one cube depth.");
            Assert.That(lower.Transform.localScale.z, Is.EqualTo(feel.BlockDepth).Within(0.0001f),
                $"{context}: lower cube depth changed during motion.");
            Assert.That(upper.Transform.localScale.z, Is.EqualTo(feel.BlockDepth).Within(0.0001f),
                $"{context}: upper cube depth changed during motion.");

            MeshRenderer lowerRenderer = lower.Transform.GetComponent<MeshRenderer>();
            MeshRenderer upperRenderer = upper.Transform.GetComponent<MeshRenderer>();
            Assert.That(lowerRenderer, Is.Not.Null);
            Assert.That(upperRenderer, Is.Not.Null);
            Assert.That(upperRenderer.bounds.max.z, Is.EqualTo(lowerRenderer.bounds.min.z).Within(0.002f),
                $"{context}: upper cube must touch its support without a gap or overlap.");
        }

        private static UnitClickTarget SelectLaneFront(int oneBasedLane)
        {
            PresentationAssets presentation = Resources.Load<PresentationAssets>("PresentationAssets");
            float aspect = Camera.main != null ? Camera.main.aspect : 9f / 19.5f;
            GameplayLayout layout = presentation.FeelProfile.ResolveLayout(10, 10, aspect);
            float targetX = layout.QueueX(oneBasedLane - 1, LevelSolver.LaneCount);
            UnitClickTarget[] targets = Object.FindObjectsByType<UnitClickTarget>(FindObjectsSortMode.None);
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

            Assert.That(best, Is.Not.Null, $"Lane {oneBasedLane} has no selectable front unit.");
            Assert.That(bestDistance, Is.LessThan(0.10f),
                $"Lane {oneBasedLane} has no selectable front unit at its authored queue position.");
            best.NotifyClick();
            return best;
        }

        private static IEnumerator WaitForLiveGameplayToSettle(
            GameController controller,
            float timeoutSeconds,
            string context)
        {
            float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            float stableSince = -1f;
            int stableFrames = 0;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                if (controller.State != GameFlowState.Playing) yield break;

                if (HasTransientRuntimeActivity(controller))
                {
                    stableSince = -1f;
                    stableFrames = 0;
                }
                else
                {
                    if (stableSince < 0f) stableSince = Time.realtimeSinceStartup;
                    stableFrames++;
                    if (stableFrames >= 2 && Time.realtimeSinceStartup - stableSince >= 0.08f)
                    {
                        yield break;
                    }
                }

                yield return null;
            }

            Assert.Fail($"{context} did not settle within {timeoutSeconds:0.0} seconds.");
        }

        private static bool HasTransientRuntimeActivity(GameController controller)
        {
            FieldInfo slotsField = typeof(GameController).GetField(
                "_slots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(slotsField, Is.Not.Null);
            System.Array slots = (System.Array)slotsField.GetValue(controller);
            for (int index = 0; index < slots.Length; index++)
            {
                object unit = slots.GetValue(index);
                if (unit == null) continue;

                System.Type unitType = unit.GetType();
                FieldInfo stateField = unitType.GetField(
                    "State",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo projectilesField = unitType.GetField(
                    "ProjectilesInFlight",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(stateField, Is.Not.Null);
                Assert.That(projectilesField, Is.Not.Null);
                UnitRuntimeState state = (UnitRuntimeState)stateField.GetValue(unit);
                int projectilesInFlight = (int)projectilesField.GetValue(unit);
                if (projectilesInFlight > 0 ||
                    state == UnitRuntimeState.MovingToSlot ||
                    state == UnitRuntimeState.Firing ||
                    state == UnitRuntimeState.Leaving)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountOccupiedRuntimeSlots(GameController controller)
        {
            FieldInfo slotsField = typeof(GameController).GetField(
                "_slots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(slotsField, Is.Not.Null);
            System.Array slots = (System.Array)slotsField.GetValue(controller);
            int occupied = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots.GetValue(index) != null) occupied++;
            }

            return occupied;
        }

        private static int ReadSessionVersion(GameController controller)
        {
            FieldInfo field = typeof(GameController).GetField(
                "_sessionVersion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (int)field.GetValue(controller);
        }

        private static int CountSelectableFrontUnits()
        {
            UnitClickTarget[] targets = Object.FindObjectsByType<UnitClickTarget>(FindObjectsSortMode.None);
            int selectable = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                Collider collider = targets[index].GetComponent<Collider>();
                if (collider != null && collider.enabled) selectable++;
            }

            return selectable;
        }

        private static int CountVisibleBlocks()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name.StartsWith("Block_")) count++;
            }
            return count;
        }

        private static Transform FindFirstVisibleBlock()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name.StartsWith("Block_")) return transforms[i];
            }

            return null;
        }

        private static Dictionary<string, float> CaptureVisibleBlockY()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            Dictionary<string, float> positions = new();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name.StartsWith("Block_"))
                {
                    positions[transforms[i].name] = transforms[i].position.y;
                }
            }

            return positions;
        }

        private static bool HasBlockMovedDown(IReadOnlyDictionary<string, float> initialY)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (!transform.name.StartsWith("Block_") ||
                    !initialY.TryGetValue(transform.name, out float startY))
                {
                    continue;
                }

                if (transform.position.y < startY - 0.10f) return true;
            }

            return false;
        }
    }
}
