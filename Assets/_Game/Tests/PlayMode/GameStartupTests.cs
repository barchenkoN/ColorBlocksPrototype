using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlocks.Core;
using ColorBlocks.Gameplay;
using ColorBlocks.Presentation;
using NUnit.Framework;
using UnityEngine;
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
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(Screen.orientation, Is.EqualTo(ScreenOrientation.Portrait));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
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
            Assert.That(firstSixAverage, Is.InRange(0.070f, 0.080f));
            Assert.That(maximumBarrelDisplacement, Is.LessThanOrEqualTo(0.151f));
            Assert.That(maximumRootAngle, Is.LessThanOrEqualTo(10.1f));

            yield return new WaitForSeconds(0.11f);
            Assert.That(Vector3.Distance(barrel.localPosition, barrelRest), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(unitRoot.localRotation, rotationRest), Is.LessThan(0.01f));
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
            best.NotifyClick();
            return best;
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
