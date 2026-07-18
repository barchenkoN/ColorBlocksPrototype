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
        [SerializeField] private float cameraFieldOfView = 6f;
        [SerializeField] private float cameraOverscan = 1.00f;
        // Camera sits toward the lower edge of the tray and looks across it by 29.5 degrees.
        // This exposes 43 px of a physical lower cube at the supplied iPhone resolution.
        [SerializeField] private Vector2 cameraViewOffset = new(0f, -0.5653f);
        [SerializeField] private float cameraTargetY = 0f;
        [SerializeField] private float cameraTargetZ = 0.30f;
        [SerializeField] private float boardTopMargin = 1.48f;
        [SerializeField] private float boardOuterWidth = 9.00f;
        [SerializeField] private float boardOuterHeight = 8.21f;
        [SerializeField] private float boardInnerWidth = 8.48f;
        [SerializeField] private float boardInnerHeight = 7.89f;
        [SerializeField] private float gridWidth = 8.00f;
        [SerializeField] private float gridHeight = 7.48f;
        [SerializeField] private float gridTopPadding = 0.614f;
        [SerializeField] private int referenceColumns = 10;
        [SerializeField] private int referenceRows = 10;

        [Header("Blocks")]
        [SerializeField] private Vector2 blockFill = new(0.990f, 0.990f);
        [SerializeField] private float blockDepth = 0.7257f;
        // Centre depth of a base cube resting on BoardInner. Higher layers subtract one
        // hiddenLayerDepth, moving toward the camera without any screen-space offset.
        [SerializeField] private float blockBoardDepth = -0.04f;
        [SerializeField] private Vector2 faceScale = new(0.58f, 0.58f);
        // Layers share the same board-space X/Y. Their visible screen separation comes from
        // real Z height and the perspective camera instead of a fake 2D vertical offset.
        [SerializeField] private float stackLayerStep = 0f;
        [SerializeField] private float stackLayerHorizontalStep = 0f;
        [SerializeField] private float hiddenLayerDepth = 0.7257f;

        [Header("Slots and queue")]
        [SerializeField] private float slotHorizontalExtent = 3.45f;
        [SerializeField] private float slotGapBelowBoard = 2.48f;
        [SerializeField] private Vector2 slotOuterSize = new(1.50f, 1.34f);
        [SerializeField] private Vector2 slotInnerSize = new(1.28f, 1.14f);
        [SerializeField] private float queueHorizontalExtent = 3.46f;
        [SerializeField] private float queueGapBelowSlots = 1.92f;
        // Allows the third queued row to compress slightly on 9:16 while the two taller
        // reference aspects continue to use their authored/max spacing.
        [SerializeField] private float queueMinimumSpacing = 0.75f;
        [SerializeField] private float queueMaximumSpacing = 1.74f;
        // Includes the queued unit's full tread silhouette and realtime contact shadow on
        // the shortest supported 1080x1920 framing instead of fitting only its pivot.
        [SerializeField] private float queueBottomMargin = 1.10f;
        [SerializeField] private float queuedUnitScale = 1.31f;
        [SerializeField] private float selectableUnitScale = 1.31f;
        [SerializeField] private float activeUnitScale = 1.33f;

        [Header("Motion timing")]
        [SerializeField] private float unitMoveDuration = 0.18f;
        [SerializeField] private float unitMoveArc = 0.20f;
        [SerializeField] private float queueSlideDuration = 0.18f;
        [SerializeField] private float unitExitDuration = 0.40f;
        [SerializeField] private float shotInterval = 0.067f;
        [SerializeField] private float recoilDuration = 0.065f;
        [SerializeField] private float projectileSpeed = 30f;
        [SerializeField] private float projectileMinimumDuration = 0.10f;
        [SerializeField] private float projectileMaximumDuration = 0.30f;
        [SerializeField] private float projectileArc = 0.055f;
        [SerializeField] private float projectileScale = 0.18f;
        [SerializeField] private int maximumProjectilesInFlight = 6;
        [SerializeField] private float revealDuration = 0.10f;
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
        public float CameraFieldOfView => cameraFieldOfView;
        public float CameraOverscan => cameraOverscan;
        public Vector2 CameraViewOffset => cameraViewOffset;
        public float CameraPlaneVerticalScale => Mathf.Sqrt(1f + cameraViewOffset.y * cameraViewOffset.y);
        public float CameraTargetY => cameraTargetY;
        public float CameraTargetZ => cameraTargetZ;
        public float BoardOuterWidth => boardOuterWidth;
        public float BoardOuterHeight => boardOuterHeight;
        public float BoardInnerWidth => boardInnerWidth;
        public float BoardInnerHeight => boardInnerHeight;
        public Vector2 BlockFill => blockFill;
        public float BlockDepth => blockDepth;
        public float BlockBoardDepth => blockBoardDepth;
        public Vector2 FaceScale => faceScale;
        public float StackLayerStep => stackLayerStep;
        public float StackLayerHorizontalStep => stackLayerHorizontalStep;
        public float HiddenLayerDepth => hiddenLayerDepth;
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
            float verticalScale = CameraPlaneVerticalScale;
            float halfHeight = ResolveCameraHalfHeight(aspect) * verticalScale;
            float boardTop = halfHeight - boardTopMargin * verticalScale;
            float boardBottom = boardTop - boardOuterHeight * verticalScale;
            float cellPitchX = gridWidth / Mathf.Max(1, referenceColumns);
            float cellPitchY = gridHeight * verticalScale / Mathf.Max(1, referenceRows);
            float actualGridWidth = boardWidth * cellPitchX;
            float gridTop = boardTop - gridTopPadding * verticalScale;
            Vector3 origin = new(
                -actualGridWidth * 0.5f + cellPitchX * 0.5f,
                gridTop - referenceRows * cellPitchY + cellPitchY * 0.5f,
                0f);

            float slotY = boardBottom - slotGapBelowBoard * verticalScale;
            float queueFrontY = slotY - queueGapBelowSlots * verticalScale;
            float bottomLimit = -halfHeight + queueBottomMargin * verticalScale;
            float fitThreeRows = Mathf.Max(0f, (queueFrontY - bottomLimit) * 0.5f);
            float queueSpacing = Mathf.Clamp(
                fitThreeRows,
                queueMinimumSpacing * verticalScale,
                queueMaximumSpacing * verticalScale);

            return new GameplayLayout(
                halfHeight,
                boardTop,
                boardBottom,
                boardTop - boardOuterHeight * verticalScale * 0.5f,
                cellPitchX,
                cellPitchY,
                origin,
                slotY,
                queueFrontY,
                queueSpacing,
                slotHorizontalExtent,
                queueHorizontalExtent,
                verticalScale);
        }

    }

    /// <summary>
    /// Configures a long-lens perspective portrait camera while preserving the measured gameplay
    /// framing at the board plane. The supplied block pitch ratio resolves to a 29.5-degree
    /// across-tray view, so physical Z separation reveals lower stack faces without Y cheating.
    /// A narrow FOV keeps this a real perspective camera while holding grid pitch variation below
    /// one percent across the complete board.
    /// </summary>
    public static class PresentationCameraRig
    {
        public static void Configure(Camera camera, GameFeelProfile feel, float aspect)
        {
            if (camera == null || feel == null) return;

            float safeAspect = Mathf.Max(0.1f, aspect);
            // The narrow lens preserves almost parallel grid lines while the independently
            // authored 29.5-degree pitch supplies the visible physical depth.
            float fieldOfView = Mathf.Clamp(feel.CameraFieldOfView, 4f, 65f);
            float tangentHalfVertical = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfHeight = feel.ResolveCameraHalfHeight(safeAspect);
            float verticalDistance = halfHeight / tangentHalfVertical;
            float horizontalDistance = feel.ReferenceHalfWidth / (tangentHalfVertical * safeAspect);
            float distance = Mathf.Max(verticalDistance, horizontalDistance) *
                Mathf.Clamp(feel.CameraOverscan, 1f, 1.25f);
            Vector2 authoredOffset = feel.CameraViewOffset;
            Vector3 viewOffset = new Vector3(authoredOffset.x, authoredOffset.y, -1f).normalized * distance;
            Vector3 target = new(0f, feel.CameraTargetY, feel.CameraTargetZ);

            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
            camera.transform.SetPositionAndRotation(
                target + viewOffset,
                Quaternion.LookRotation(-viewOffset, Vector3.up));
            camera.nearClipPlane = 0.3f;
            // The narrow FOV moves the camera well away from the board on
            // the reference aspect. Keep the complete layered board and input colliders
            // comfortably inside the frustum instead of using the old 80-unit limit.
            camera.farClipPlane = Mathf.Max(100f, distance + 20f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
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
            float queueHorizontalExtent,
            float planeVerticalScale)
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
            PlaneVerticalScale = planeVerticalScale;
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
        public float PlaneVerticalScale { get; }

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
