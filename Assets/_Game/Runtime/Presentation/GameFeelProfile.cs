using System;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    /// <summary>
    /// One source of truth for reference-matched layout and authored gameplay motion.
    /// Values are expressed in world units so the camera keeps the same horizontal framing
    /// across portrait aspect ratios.
    /// </summary>
    [Serializable]
    public sealed class GameFeelProfile
    {
        [Header("Camera and board")]
        [SerializeField] private float referenceHalfWidth = 4.70f;
        [SerializeField] private float minimumCameraHalfHeight = 8.35f;
        [SerializeField] private float boardTopMargin = 1.48f;
        [SerializeField] private float boardOuterWidth = 9.10f;
        [SerializeField] private float boardOuterHeight = 8.55f;
        [SerializeField] private float boardInnerWidth = 8.48f;
        [SerializeField] private float boardInnerHeight = 8.03f;
        [SerializeField] private float gridWidth = 7.89f;
        [SerializeField] private float gridHeight = 7.07f;
        [SerializeField] private float gridTopPadding = -0.04f;
        [SerializeField] private int referenceColumns = 10;
        [SerializeField] private int referenceRows = 10;

        [Header("Blocks")]
        [SerializeField] private Vector2 blockFill = new(0.985f, 0.985f);
        [SerializeField] private float blockDepth = 0.36f;
        [SerializeField] private Vector2 faceScale = new(0.56f, 0.48f);
        [SerializeField] private float stackLayerStep = 0.3535f;
        [SerializeField] private float hiddenLayerDepth = 0.22f;
        [SerializeField] private float blockShadowOffset = 0.045f;
        [SerializeField] private Vector2 frontBandScale = new(0.91f, 0.46f);
        [SerializeField] private float frontBandOffset = 0.70f;

        [Header("Slots and queue")]
        [SerializeField] private float slotHorizontalExtent = 3.46f;
        [SerializeField] private float slotGapBelowBoard = 2.062f;
        [SerializeField] private Vector2 slotOuterSize = new(1.47f, 1.39f);
        [SerializeField] private Vector2 slotInnerSize = new(1.18f, 1.10f);
        [SerializeField] private float queueHorizontalExtent = 3.46f;
        [SerializeField] private float queueGapBelowSlots = 1.92f;
        [SerializeField] private float queueMinimumSpacing = 1.08f;
        [SerializeField] private float queueMaximumSpacing = 1.74f;
        [SerializeField] private float queueBottomMargin = 0.62f;
        [SerializeField] private float queuedUnitScale = 1.18f;
        [SerializeField] private float selectableUnitScale = 1.18f;
        [SerializeField] private float activeUnitScale = 1.20f;

        [Header("Motion timing")]
        [SerializeField] private float unitMoveDuration = 0.267f;
        [SerializeField] private float unitMoveArc = 0.24f;
        [SerializeField] private float queueSlideDuration = 0.18f;
        [SerializeField] private float unitExitDuration = 0.22f;
        [SerializeField] private float shotInterval = 0.075f;
        [SerializeField] private float recoilDuration = 0.085f;
        [SerializeField] private float projectileSpeed = 18.5f;
        [SerializeField] private float projectileMinimumDuration = 0.16f;
        [SerializeField] private float projectileMaximumDuration = 0.44f;
        [SerializeField] private float projectileArc = 0.055f;
        [SerializeField] private float projectileScale = 0.52f;
        [SerializeField] private int maximumProjectilesInFlight = 6;
        [SerializeField] private float revealDuration = 0.13f;
        [SerializeField] private float fallDelay = 0.025f;
        [SerializeField] private float fallBaseDuration = 0.28f;
        [SerializeField] private float fallDistanceDuration = 0.04f;
        [SerializeField] private float fallBounceHeight = 0.068f;

        [Header("Impact layers")]
        [SerializeField] private int fragmentCount = 10;
        [SerializeField] private float fragmentDuration = 0.42f;
        [SerializeField] private float fragmentSpeed = 2.65f;
        [SerializeField] private float fragmentGravity = 8.4f;
        [SerializeField] private float impactFlashDuration = 0.04f;

        public float ReferenceHalfWidth => referenceHalfWidth;
        public float MinimumCameraHalfHeight => minimumCameraHalfHeight;
        public float BoardOuterWidth => boardOuterWidth;
        public float BoardOuterHeight => boardOuterHeight;
        public float BoardInnerWidth => boardInnerWidth;
        public float BoardInnerHeight => boardInnerHeight;
        public Vector2 BlockFill => blockFill;
        public float BlockDepth => blockDepth;
        public Vector2 FaceScale => faceScale;
        public float StackLayerStep => stackLayerStep;
        public float HiddenLayerDepth => hiddenLayerDepth;
        public float BlockShadowOffset => blockShadowOffset;
        public Vector2 FrontBandScale => frontBandScale;
        public float FrontBandOffset => frontBandOffset;
        public Vector2 SlotOuterSize => slotOuterSize;
        public Vector2 SlotInnerSize => slotInnerSize;
        public float QueuedUnitScale => queuedUnitScale;
        public float SelectableUnitScale => selectableUnitScale;
        public float ActiveUnitScale => activeUnitScale;
        public float UnitMoveDuration => unitMoveDuration;
        public float UnitMoveArc => unitMoveArc;
        public float QueueSlideDuration => queueSlideDuration;
        public float UnitExitDuration => unitExitDuration;
        public float ShotInterval => shotInterval;
        public float RecoilDuration => recoilDuration;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileMinimumDuration => projectileMinimumDuration;
        public float ProjectileMaximumDuration => projectileMaximumDuration;
        public float ProjectileArc => projectileArc;
        public float ProjectileScale => projectileScale;
        public int MaximumProjectilesInFlight => maximumProjectilesInFlight;
        public float RevealDuration => revealDuration;
        public float FallDelay => fallDelay;
        public float FallBaseDuration => fallBaseDuration;
        public float FallDistanceDuration => fallDistanceDuration;
        public float FallBounceHeight => fallBounceHeight;
        public int FragmentCount => fragmentCount;
        public float FragmentDuration => fragmentDuration;
        public float FragmentSpeed => fragmentSpeed;
        public float FragmentGravity => fragmentGravity;
        public float ImpactFlashDuration => impactFlashDuration;

        public float ResolveProjectileDuration(float distance)
        {
            return Mathf.Clamp(
                distance / Mathf.Max(0.1f, projectileSpeed),
                projectileMinimumDuration,
                projectileMaximumDuration);
        }

        public float ResolveFallDuration(int cellDistance)
        {
            return fallBaseDuration + fallDistanceDuration * Mathf.Sqrt(Mathf.Max(1, cellDistance));
        }

        public float ResolveCameraHalfHeight(float aspect)
        {
            return Mathf.Max(minimumCameraHalfHeight, referenceHalfWidth / Mathf.Max(0.1f, aspect));
        }

        public GameplayLayout ResolveLayout(int boardWidth, int boardHeight, float aspect)
        {
            float halfHeight = ResolveCameraHalfHeight(aspect);
            float boardTop = halfHeight - boardTopMargin;
            float boardBottom = boardTop - boardOuterHeight;
            float cellPitchX = gridWidth / Mathf.Max(1, referenceColumns);
            float cellPitchY = gridHeight / Mathf.Max(1, referenceRows);
            float actualGridWidth = boardWidth * cellPitchX;
            float gridTop = boardTop - gridTopPadding;
            Vector3 origin = new(
                -actualGridWidth * 0.5f + cellPitchX * 0.5f,
                gridTop - referenceRows * cellPitchY + cellPitchY * 0.5f,
                0f);

            float slotY = boardBottom - slotGapBelowBoard;
            float queueFrontY = slotY - queueGapBelowSlots;
            float bottomLimit = -halfHeight + queueBottomMargin;
            float fitThreeRows = Mathf.Max(0f, (queueFrontY - bottomLimit) * 0.5f);
            float queueSpacing = Mathf.Clamp(fitThreeRows, queueMinimumSpacing, queueMaximumSpacing);

            return new GameplayLayout(
                halfHeight,
                boardTop,
                boardBottom,
                boardTop - boardOuterHeight * 0.5f,
                cellPitchX,
                cellPitchY,
                origin,
                slotY,
                queueFrontY,
                queueSpacing,
                slotHorizontalExtent,
                queueHorizontalExtent);
        }

    }

    public static class GameFeelMotion
    {
        private const float ImpactTime = 0.55f;

        /// <summary>
        /// Advances an absolute cadence deadline instead of restarting from the rendered
        /// frame time. At 60 Hz this naturally alternates four- and five-frame gaps for a
        /// 0.075 s cadence, while an idle or badly stale schedule restarts without bursts.
        /// </summary>
        public static float AdvanceCadence(float previousDeadline, float currentTime, float interval)
        {
            float safeInterval = Mathf.Max(0.001f, interval);
            if (float.IsNaN(previousDeadline) || float.IsInfinity(previousDeadline))
            {
                return currentTime + safeInterval;
            }

            float nextDeadline = previousDeadline + safeInterval;
            return nextDeadline < currentTime - safeInterval
                ? currentTime + safeInterval
                : nextDeadline;
        }

        /// <summary>
        /// Measured two-phase fall: near-linear/eased descent into one absolute overshoot,
        /// followed by a monotonic settle. X/Z remain deterministic and unchanged.
        /// </summary>
        public static Vector3 EvaluateFall(
            Vector3 start,
            Vector3 target,
            float overshootDistance,
            float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            Vector3 overshootTarget = target + Vector3.down * overshootDistance;
            if (t <= ImpactTime)
            {
                float phase = t / ImpactTime;
                float phase2 = phase * phase;
                float phase3 = phase2 * phase;
                float smooth = -2f * phase3 + 3f * phase2;
                float startTangent = phase3 - 2f * phase2 + phase;
                float progress = smooth + startTangent * 1.5f;
                return Vector3.LerpUnclamped(start, overshootTarget, progress);
            }

            float settle = Mathf.InverseLerp(ImpactTime, 1f, t);
            settle = settle * settle * (3f - 2f * settle);
            return Vector3.LerpUnclamped(overshootTarget, target, settle);
        }
    }

    public readonly struct GameplayLayout
    {
        public GameplayLayout(
            float cameraHalfHeight,
            float boardTop,
            float boardBottom,
            float boardCenterY,
            float cellPitchX,
            float cellPitchY,
            Vector3 gridOrigin,
            float slotY,
            float queueFrontY,
            float queueSpacing,
            float slotHorizontalExtent,
            float queueHorizontalExtent)
        {
            CameraHalfHeight = cameraHalfHeight;
            BoardTop = boardTop;
            BoardBottom = boardBottom;
            BoardCenterY = boardCenterY;
            CellPitchX = cellPitchX;
            CellPitchY = cellPitchY;
            GridOrigin = gridOrigin;
            SlotY = slotY;
            QueueFrontY = queueFrontY;
            QueueSpacing = queueSpacing;
            SlotHorizontalExtent = slotHorizontalExtent;
            QueueHorizontalExtent = queueHorizontalExtent;
        }

        public float CameraHalfHeight { get; }
        public float BoardTop { get; }
        public float BoardBottom { get; }
        public float BoardCenterY { get; }
        public float CellPitchX { get; }
        public float CellPitchY { get; }
        public Vector3 GridOrigin { get; }
        public float SlotY { get; }
        public float QueueFrontY { get; }
        public float QueueSpacing { get; }
        public float SlotHorizontalExtent { get; }
        public float QueueHorizontalExtent { get; }

        public float SlotX(int index, int count)
        {
            return count <= 1 ? 0f : Mathf.Lerp(-SlotHorizontalExtent, SlotHorizontalExtent, index / (count - 1f));
        }

        public float QueueX(int index, int count)
        {
            return count <= 1 ? 0f : Mathf.Lerp(-QueueHorizontalExtent, QueueHorizontalExtent, index / (count - 1f));
        }
    }
}
