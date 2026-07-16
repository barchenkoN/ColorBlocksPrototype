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
            GameplayLayout layout = feel.ResolveLayout(10, 10, width / (float)height);
            float pixelsPerWorldUnit = width / (feel.ReferenceHalfWidth * 2f);

            Assert.That(layout.CellPitchX * pixelsPerWorldUnit, Is.InRange(97.9f, 98.5f));
            Assert.That(layout.CellPitchY * pixelsPerWorldUnit, Is.InRange(87.7f, 88.3f));
            Assert.That(feel.StackLayerStep * pixelsPerWorldUnit, Is.InRange(42f, 46f));

            float boardTopPixels = (layout.CameraHalfHeight - layout.BoardTop) * pixelsPerWorldUnit;
            float slotCenterPixels = (layout.CameraHalfHeight - layout.SlotY) * pixelsPerWorldUnit;
            float queueCenterPixels = (layout.CameraHalfHeight - layout.QueueFrontY) * pixelsPerWorldUnit;
            Assert.That(boardTopPixels, Is.InRange(182f, 187f));
            Assert.That(slotCenterPixels, Is.InRange(1503f, 1507f));
            Assert.That(queueCenterPixels, Is.InRange(1742f, 1746f));

            Assert.That(ToPixelX(layout.SlotX(0, 5), width, pixelsPerWorldUnit), Is.InRange(148f, 158f));
            Assert.That(ToPixelX(layout.SlotX(4, 5), width, pixelsPerWorldUnit), Is.InRange(1010f, 1022f));
        }

        [Test]
        public void TilePitch_DoesNotInflateOnSmallerLevels()
        {
            GameFeelProfile feel = new();
            GameplayLayout small = feel.ResolveLayout(5, 4, 9f / 19.5f);
            GameplayLayout dense = feel.ResolveLayout(10, 10, 9f / 19.5f);

            Assert.That(small.CellPitchX, Is.EqualTo(dense.CellPitchX).Within(0.0001f));
            Assert.That(small.CellPitchY, Is.EqualTo(dense.CellPitchY).Within(0.0001f));
            Assert.That(small.CellPitchY / small.CellPitchX, Is.InRange(0.88f, 0.91f));
        }

        [Test]
        public void ReferenceTiming_UsesFastShotsAndSingleSettleWindow()
        {
            GameFeelProfile feel = new();
            Assert.That(feel.ShotInterval, Is.EqualTo(0.075f).Within(0.001f));
            Assert.That(feel.ResolveFallDuration(1), Is.InRange(0.30f, 0.33f));
            Assert.That(feel.ResolveFallDuration(4), Is.InRange(0.34f, 0.38f));
            Assert.That(feel.ResolveProjectileDuration(4f), Is.InRange(0.20f, 0.24f));
        }

        [Test]
        public void ShotCadence_PreservesPhaseAtSixtyHertzAndResetsAfterIdle()
        {
            const float interval = 0.075f;
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
                Is.EqualTo(2.075f).Within(0.0001f));
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

        private static float ToPixelX(float worldX, int width, float pixelsPerWorldUnit)
        {
            return width * 0.5f + worldX * pixelsPerWorldUnit;
        }
    }
}
