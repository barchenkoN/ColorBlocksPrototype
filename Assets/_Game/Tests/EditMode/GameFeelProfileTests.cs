using ColorBlocks.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlocks.Tests
{
    public sealed class GameFeelProfileTests
    {
        [Test]
        public void SuppliedReferenceAspect_ResolvesMeasuredPixelGeometry()
        {
            GameFeelProfile feel = new();
            const int width = 1170;
            const int height = 2532;
            float aspect = width / (float)height;
            GameplayLayout layout = feel.ResolveLayout(10, 10, aspect);
            GameObject cameraObject = new("ReferenceGeometryCameraTest");

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.aspect = aspect;
                PresentationCameraRig.Configure(camera, feel, aspect);

                // Measure the same upper-board region as the supplied reference. Perspective
                // deliberately makes the lower pitch slightly larger, so a world-units-only
                // conversion would be inaccurate for this real 3D camera.
                Vector3 upperCell = new(
                    layout.GridOrigin.x + layout.CellPitchX * 4f,
                    layout.GridOrigin.y + layout.CellPitchY * 9f,
                    feel.BlockBoardDepth);
                Vector2 upperPixel = ToTopLeftPixels(camera, upperCell, width, height);
                Vector2 rightPixel = ToTopLeftPixels(
                    camera,
                    upperCell + Vector3.right * layout.CellPitchX,
                    width,
                    height);
                Vector2 lowerPixel = ToTopLeftPixels(
                    camera,
                    upperCell - Vector3.up * layout.CellPitchY,
                    width,
                    height);
                Assert.That(Mathf.Abs(rightPixel.x - upperPixel.x), Is.InRange(97.0f, 98.5f));
                Assert.That(Mathf.Abs(lowerPixel.y - upperPixel.y), Is.InRange(88.0f, 90.5f));

                Vector2 boardTop = ToTopLeftPixels(
                    camera,
                    new Vector3(0f, layout.BoardTop, feel.BlockBoardDepth),
                    width,
                    height);
                Vector2 slotLeft = ToTopLeftPixels(
                    camera,
                    new Vector3(
                        layout.SlotX(0, 5),
                        layout.SlotY + 0.032f * layout.PlaneVerticalScale,
                        0.18f),
                    width,
                    height);
                Vector2 slotRight = ToTopLeftPixels(
                    camera,
                    new Vector3(
                        layout.SlotX(4, 5),
                        layout.SlotY + 0.032f * layout.PlaneVerticalScale,
                        0.18f),
                    width,
                    height);
                Vector2 queueFront = ToTopLeftPixels(
                    camera,
                    new Vector3(0f, layout.QueueFrontY, 0.08f),
                    width,
                    height);

                Assert.That(boardTop.y, Is.InRange(186f, 192f));
                Assert.That(slotLeft.y, Is.InRange(1502f, 1508f));
                Assert.That(queueFront.y, Is.InRange(1742f, 1749f));
                Assert.That(Mathf.Min(slotLeft.x, slotRight.x), Is.InRange(148f, 158f));
                Assert.That(Mathf.Max(slotLeft.x, slotRight.x), Is.InRange(1010f, 1022f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void TilePitch_DoesNotInflateOnSmallerLevels()
        {
            GameFeelProfile feel = new();
            GameplayLayout small = feel.ResolveLayout(5, 4, 9f / 19.5f);
            GameplayLayout dense = feel.ResolveLayout(10, 10, 9f / 19.5f);

            Assert.That(small.CellPitchX, Is.EqualTo(dense.CellPitchX).Within(0.0001f));
            Assert.That(small.CellPitchY, Is.EqualTo(dense.CellPitchY).Within(0.0001f));
            Assert.That(small.CellPitchX, Is.EqualTo(0.80f).Within(0.0001f));
            Assert.That(small.CellPitchY, Is.InRange(0.85f, 0.87f));
            Assert.That(small.CellPitchY / small.CellPitchX, Is.InRange(1.06f, 1.08f),
                "Board-space Y is intentionally expanded to compensate the physical camera pitch.");
        }

        [TestCase(1170, 2532)]
        [TestCase(1080, 1920)]
        [TestCase(720, 1600)]
        public void PerspectiveCamera_FitsGameplayAtSupportedPortraitAspects(int width, int height)
        {
            GameFeelProfile feel = new();
            float aspect = width / (float)height;
            GameplayLayout layout = feel.ResolveLayout(10, 10, aspect);
            GameObject cameraObject = new("PerspectiveCameraTest");

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.aspect = aspect;
                PresentationCameraRig.Configure(camera, feel, aspect);

                Assert.That(camera.orthographic, Is.False);
                Assert.That(camera.fieldOfView, Is.EqualTo(feel.CameraFieldOfView).Within(0.001f));
                Assert.That(camera.transform.forward.z, Is.InRange(0.86f, 0.88f));
                Assert.That(camera.transform.forward.y, Is.EqualTo(Mathf.Sin(29.5f * Mathf.Deg2Rad)).Within(0.003f));
                Assert.That(camera.nearClipPlane, Is.GreaterThan(0f));
                Assert.That(camera.farClipPlane, Is.GreaterThan(camera.nearClipPlane));

                float lowestQueueCenter = layout.QueueFrontY - layout.QueueSpacing * 2f;
                float queuedUnitBottom = lowestQueueCenter -
                    feel.QueuedUnitScale * feel.CameraPlaneVerticalScale * 0.48f;
                Vector3[] requiredContentPoints =
                {
                    new(-feel.BoardOuterWidth * 0.5f, layout.BoardTop, feel.BlockBoardDepth),
                    new(feel.BoardOuterWidth * 0.5f, layout.BoardTop, feel.BlockBoardDepth),
                    new(-feel.BoardOuterWidth * 0.5f, layout.BoardBottom, feel.BlockBoardDepth),
                    new(feel.BoardOuterWidth * 0.5f, layout.BoardBottom, feel.BlockBoardDepth),
                    new(layout.QueueX(0, 5), lowestQueueCenter, 0.26f),
                    new(layout.QueueX(4, 5), lowestQueueCenter, 0.26f),
                    // Test the visible tread/depth corner, not only the queue pivot. The
                    // negative Z matches the camera-facing unit geometry that projects lowest.
                    new(layout.QueueX(0, 5), queuedUnitBottom, -0.40f),
                    new(layout.QueueX(4, 5), queuedUnitBottom, -0.40f)
                };

                for (int i = 0; i < requiredContentPoints.Length; i++)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(requiredContentPoints[i]);
                    Assert.That(viewport.z, Is.GreaterThan(0f), $"Content point {i} is behind the camera.");
                    Assert.That(viewport.x, Is.InRange(0f, 1f), $"Content point {i} is clipped horizontally.");
                    Assert.That(viewport.y, Is.InRange(0.01f, 1f),
                        $"Content point {i} is clipped vertically or lacks a clean edge margin.");
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void LayerPresentation_UsesVolumetricBlocksAndProjectedDepthSeparation()
        {
            const int width = 1170;
            const int height = 2532;
            GameFeelProfile feel = new();
            float aspect = width / (float)height;
            GameObject cameraObject = new("LayerDepthCameraTest");

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.aspect = aspect;
                PresentationCameraRig.Configure(camera, feel, aspect);

                Vector3 baseLayer = new(0f, 0f, feel.BlockBoardDepth);
                Vector3 topLayer = new(
                    -feel.StackLayerHorizontalStep,
                    -feel.StackLayerStep,
                    feel.BlockBoardDepth - feel.HiddenLayerDepth);

                Assert.That(feel.BlockDepth, Is.GreaterThan(0.5f));
                Assert.That(feel.HiddenLayerDepth, Is.EqualTo(feel.BlockDepth).Within(0.0001f),
                    "Adjacent physical stack layers must touch exactly without overlap or an air gap.");
                Assert.That(
                    Mathf.Abs(topLayer.z - baseLayer.z),
                    Is.EqualTo(feel.HiddenLayerDepth).Within(0.0001f));

                Vector3 baseViewport = camera.WorldToViewportPoint(baseLayer);
                Vector3 topViewport = camera.WorldToViewportPoint(topLayer);
                Vector2 projectedPixelDelta = new(
                    (topViewport.x - baseViewport.x) * width,
                    (topViewport.y - baseViewport.y) * height);

                Assert.That(topViewport.z, Is.LessThan(baseViewport.z),
                    "A higher cube must be physically closer to the perspective camera.");
                Assert.That(projectedPixelDelta.magnitude, Is.InRange(8f, 80f),
                    "Physical layer depth must remain visibly separated at the supplied iPhone resolution.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ReferenceTiming_UsesFastShotsAndSingleSettleWindow()
        {
            GameFeelProfile feel = new();
            Assert.That(feel.ShotInterval, Is.EqualTo(0.067f).Within(0.001f));
            Assert.That(feel.UnitMoveDuration, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(feel.RecoilDuration, Is.LessThanOrEqualTo(feel.ShotInterval));
            Assert.That(feel.RevealDuration, Is.EqualTo(0.10f).Within(0.001f));
            Assert.That(feel.ResolveFallDuration(1), Is.InRange(0.30f, 0.33f));
            Assert.That(feel.ResolveFallDuration(4), Is.InRange(0.34f, 0.38f));
            Assert.That(feel.ResolveProjectileDuration(4f), Is.InRange(0.13f, 0.14f));
        }

        [Test]
        public void ShotCadence_PreservesPhaseAtSixtyHertzAndResetsAfterIdle()
        {
            const float interval = 0.067f;
            const float frame = 1f / 60f;
            float deadline = 0.05f;
            float firstShot = -1f;
            float lastShot = -1f;
            int shots = 0;

            for (int frameIndex = 0; frameIndex < 90 && shots < 10; frameIndex++)
            {
                float now = frameIndex * frame;
                if (now + 0.00001f < deadline) continue;
                if (shots == 0) firstShot = now;
                lastShot = now;
                shots++;
                deadline = GameFeelMotion.AdvanceCadence(deadline, now, interval);
            }

            Assert.That(shots, Is.EqualTo(10));
            Assert.That((lastShot - firstShot) / (shots - 1), Is.EqualTo(interval).Within(frame));
            Assert.That(
                GameFeelMotion.AdvanceCadence(float.NegativeInfinity, 2f, interval),
                Is.EqualTo(2.067f).Within(0.0001f));
        }

        [Test]
        public void FallCurve_HasOneAbsoluteOvershootAndNoHorizontalDrift()
        {
            Vector3 start = new(1.25f, 2f, 0.2f);
            Vector3 target = new(1.25f, 1f, 0.2f);
            Vector3 impact = GameFeelMotion.EvaluateFall(start, target, 0.068f, 0.55f);
            Vector3 lateSettle = GameFeelMotion.EvaluateFall(start, target, 0.068f, 0.80f);
            Vector3 settled = GameFeelMotion.EvaluateFall(start, target, 0.068f, 1f);

            Assert.That(impact.y, Is.EqualTo(0.932f).Within(0.001f));
            Assert.That(lateSettle.y, Is.GreaterThan(impact.y));
            Assert.That(lateSettle.y, Is.LessThan(target.y));
            Assert.That(settled, Is.EqualTo(target));
            Assert.That(impact.x, Is.EqualTo(start.x).Within(0.0001f));
            Assert.That(impact.z, Is.EqualTo(start.z).Within(0.0001f));
        }

        private static Vector2 ToTopLeftPixels(Camera camera, Vector3 worldPosition, int width, int height)
        {
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            return new Vector2(viewport.x * width, (1f - viewport.y) * height);
        }
    }
}
