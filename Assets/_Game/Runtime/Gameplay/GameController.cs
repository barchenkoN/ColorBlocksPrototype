using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlocks.Core;
using ColorBlocks.Presentation;
using ColorBlocks.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ColorBlocks.Gameplay
{
    public sealed class GameController : MonoBehaviour
    {
        private sealed class RuntimeUnit
        {
            public UnitDefinition Definition;
            public UnitView View;
            public int LaneIndex;
            public int OrderIndex;
            public int Charges;
            public int SlotIndex = -1;
            public UnitRuntimeState State;
            public int ProjectilesInFlight;
            public float NextShotTime;
        }

        private sealed class RuntimeLane
        {
            public readonly List<RuntimeUnit> Units = new();
            public int FrontIndex;
        }

        private LevelCatalog _catalog;
        private LevelSequence _sequence;
        private LevelDefinition _currentLevel;
        private BoardModel _board;
        private BoardView _boardView;
        private VisualTheme _theme;
        private GameFeelProfile _feel;
        private GameplayLayout _layout;
        private ProjectilePool _projectiles;
        private ImpactFxPool _impactFx;
        private ProceduralAudioService _audio;
        private IHapticsService _haptics;
        private HudView _hud;
        private GameObject _unitsRoot;
        private GameObject _slotsRoot;
        private readonly RuntimeLane[] _lanes = new RuntimeLane[LevelSolver.LaneCount];
        private readonly RuntimeUnit[] _slots = new RuntimeUnit[LevelSolver.SlotCount];
        private readonly Vector3[] _slotPositions = new Vector3[LevelSolver.SlotCount];
        private int _sessionVersion;
        private float _lastImpactHapticTime = -10f;
        private bool _initialized;
#if UNITY_EDITOR
        private float _editorLayoutAspect;
#endif

        public GameFlowState State { get; private set; } = GameFlowState.Initializing;

#if UNITY_EDITOR
        public void SetEditorLayoutAspect(float aspect)
        {
            _editorLayoutAspect = aspect;
        }
#endif

        public void Initialize(LevelCatalog catalog, PresentationAssets presentationAssets)
        {
            if (_initialized) return;
            _initialized = true;
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            if (presentationAssets == null) throw new ArgumentNullException(nameof(presentationAssets));
            _sequence = new LevelSequence(Environment.TickCount);
            _feel = presentationAssets.FeelProfile;
            _theme = new VisualTheme(presentationAssets);
            _audio = new ProceduralAudioService(presentationAssets);
            _haptics = HapticsServiceFactory.Create();
            _hud = new HudView(this, presentationAssets, RestartCurrentLevel);
            LoadLevel(_sequence.Next());
        }

        private void Update()
        {
            if (!_initialized || State != GameFlowState.Playing) return;
            HandlePointerInput();
            UpdateCombat();
            CheckForLoss();
        }

        private void OnDestroy()
        {
            ClearLevelObjects();
            _hud?.Destroy();
            _audio?.Destroy();
            _theme?.Dispose();
        }

        private void HandlePointerInput()
        {
            Vector2 screenPosition;
            bool pressed = false;

            if (Touchscreen.current != null)
            {
                foreach (TouchControl touch in Touchscreen.current.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;
                    screenPosition = touch.position.ReadValue();
                    pressed = true;
                    int pointerId = touch.touchId.ReadValue();
                    if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(pointerId))
                    {
                        TrySelectAt(screenPosition);
                    }
                    break;
                }
            }

            if (!pressed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    TrySelectAt(screenPosition);
                }
            }
        }

        private void TrySelectAt(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 50f)) return;
            UnitClickTarget target = hit.collider.GetComponentInParent<UnitClickTarget>();
            target?.NotifyClick();
        }

        private void SelectUnit(RuntimeUnit unit)
        {
            if (State != GameFlowState.Playing || unit.State != UnitRuntimeState.Queued) return;
            RuntimeLane lane = _lanes[unit.LaneIndex];
            if (lane.FrontIndex >= lane.Units.Count || lane.Units[lane.FrontIndex] != unit) return;

            int slotIndex = FindFreeSlot();
            if (slotIndex < 0) return;

            _slots[slotIndex] = unit;
            unit.SlotIndex = slotIndex;
            unit.State = UnitRuntimeState.MovingToSlot;
            unit.View.SetActiveState();
            lane.FrontIndex++;
            RefreshLane(unit.LaneIndex, true);
            _audio.Tap();
            _haptics.Soft();

            int version = _sessionVersion;
            StartCoroutine(unit.View.MoveTo(_slotPositions[slotIndex], _feel.UnitMoveDuration, () =>
            {
                if (version != _sessionVersion || State != GameFlowState.Playing) return;
                unit.State = UnitRuntimeState.Waiting;
                unit.NextShotTime = Time.time + 0.05f;
                _audio.Land();
            }));
        }

        private void UpdateCombat()
        {
            for (int slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
            {
                RuntimeUnit unit = _slots[slotIndex];
                if (unit == null ||
                    unit.ProjectilesInFlight >= _feel.MaximumProjectilesInFlight ||
                    Time.time < unit.NextShotTime) continue;
                if (unit.State != UnitRuntimeState.Waiting && unit.State != UnitRuntimeState.Firing) continue;

                if (unit.Charges <= 0)
                {
                    if (unit.ProjectilesInFlight == 0) StartUnitLeaving(unit);
                    continue;
                }

                float boardX = Mathf.Lerp(0f, _board.Width - 1f, slotIndex / (float)(_slots.Length - 1));
                if (!_board.TryReserveTarget(unit.Definition.Color, boardX, out TargetReservation target))
                {
                    unit.State = UnitRuntimeState.Waiting;
                    unit.NextShotTime = float.NegativeInfinity;
                    continue;
                }

                Fire(unit, target);
            }
        }

        private void Fire(RuntimeUnit unit, TargetReservation target)
        {
            int version = _sessionVersion;
            unit.State = UnitRuntimeState.Firing;
            unit.ProjectilesInFlight++;
            unit.Charges--;
            unit.NextShotTime = GameFeelMotion.AdvanceCadence(
                unit.NextShotTime,
                Time.time,
                _feel.ShotInterval);
            unit.View.SetCharges(unit.Charges);

            BlockView targetView = _boardView.Get(target.Target);
            unit.View.AimAt(targetView.TargetPosition);
            _audio.Shot();
            StartCoroutine(unit.View.Recoil(_feel.RecoilDuration));
            float distance = Vector2.Distance(unit.View.MuzzlePosition, targetView.TargetPosition);
            float duration = _feel.ResolveProjectileDuration(distance);
            StartCoroutine(_projectiles.Play(
                unit.View.MuzzlePosition,
                () => targetView.TargetPosition,
                duration,
                () => ResolveImpact(version, unit, target, targetView)));
        }

        private void ResolveImpact(int version, RuntimeUnit unit, TargetReservation reservation, BlockView targetView)
        {
            if (version != _sessionVersion || State != GameFlowState.Playing)
            {
                _board?.Release(reservation);
                return;
            }

            BoardMutation mutation = _board.Destroy(reservation);
            unit.ProjectilesInFlight = Mathf.Max(0, unit.ProjectilesInFlight - 1);
            _audio.Impact();
            _audio.DestroyBlock();
            if (Time.unscaledTime - _lastImpactHapticTime > 0.085f)
            {
                _lastImpactHapticTime = Time.unscaledTime;
                _haptics.Light();
            }

            StartCoroutine(_impactFx.Play(targetView.Node.Color, targetView.TargetPosition));
            targetView.HideDestroyed();
            if (mutation.RevealedNode != null)
            {
                BlockView revealedView = _boardView.Get(mutation.RevealedNode);
                StartCoroutine(revealedView.Reveal(_feel.RevealDuration));
            }

            if (mutation.Falls.Count > 0)
            {
                float longestFall = 0f;
                for (int fallIndex = 0; fallIndex < mutation.Falls.Count; fallIndex++)
                {
                    BlockStackFall fall = mutation.Falls[fallIndex];
                    float duration = _feel.ResolveFallDuration(fall.Distance);
                    longestFall = Mathf.Max(longestFall, duration);
                    Vector3 destination = _boardView.ToWorld(fall.To);
                    for (int layerIndex = 0; layerIndex < fall.Stack.Layers.Count; layerIndex++)
                    {
                        BlockNode fallingNode = fall.Stack.Layers[layerIndex];
                        if (fallingNode.IsDestroyed) continue;
                        StartCoroutine(_boardView.Get(fallingNode).FallTo(destination, duration, _feel.FallDelay));
                    }
                }

                StartCoroutine(PlayGravitySettle(version, longestFall + _feel.FallDelay));
            }

            if (_board.RemainingBlocks == 0)
            {
                BeginWin();
                return;
            }

            if (unit.Charges <= 0 && unit.ProjectilesInFlight == 0)
            {
                StartUnitLeaving(unit);
            }
            else
            {
                unit.State = unit.ProjectilesInFlight > 0 ? UnitRuntimeState.Firing : UnitRuntimeState.Waiting;
            }
        }

        private IEnumerator PlayGravitySettle(int version, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (version != _sessionVersion || State != GameFlowState.Playing) yield break;
            _audio.Land();
            _haptics.Soft();
        }

        private void StartUnitLeaving(RuntimeUnit unit)
        {
            if (unit.State == UnitRuntimeState.Leaving || unit.State == UnitRuntimeState.Recycled) return;
            int version = _sessionVersion;
            int slotIndex = unit.SlotIndex;
            unit.State = UnitRuntimeState.Leaving;
            _audio.Exit();
            StartCoroutine(unit.View.Leave(_feel.UnitExitDuration, () =>
            {
                if (version != _sessionVersion) return;
                if (slotIndex >= 0 && _slots[slotIndex] == unit) _slots[slotIndex] = null;
                unit.SlotIndex = -1;
                unit.State = UnitRuntimeState.Recycled;
            }));
        }

        private void CheckForLoss()
        {
            if (State != GameFlowState.Playing || _board.RemainingBlocks == 0) return;

            bool hasTransientAction = false;
            bool allSlotsFull = true;
            bool activeUnitCanAttack = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                RuntimeUnit unit = _slots[i];
                if (unit == null)
                {
                    allSlotsFull = false;
                    continue;
                }

                if (unit.ProjectilesInFlight > 0 || unit.State == UnitRuntimeState.MovingToSlot || unit.State == UnitRuntimeState.Leaving)
                {
                    hasTransientAction = true;
                }

                if (unit.Charges > 0 && _board.HasAvailableTarget(unit.Definition.Color))
                {
                    activeUnitCanAttack = true;
                }
            }

            if (hasTransientAction || activeUnitCanAttack) return;
            bool hasSelectableUnit = false;
            for (int lane = 0; lane < _lanes.Length; lane++)
            {
                if (_lanes[lane].FrontIndex < _lanes[lane].Units.Count)
                {
                    hasSelectableUnit = true;
                    break;
                }
            }

            if (allSlotsFull || !hasSelectableUnit)
            {
                BeginLoss();
            }
        }

        private void BeginWin()
        {
            if (State != GameFlowState.Playing) return;
            State = GameFlowState.Completing;
            _audio.Win();
            _haptics.Heavy();
            StartCoroutine(ShowResultAfterDelay(true));
        }

        private void BeginLoss()
        {
            if (State != GameFlowState.Playing) return;
            State = GameFlowState.Completing;
            _audio.Loss();
            _haptics.Heavy();
            StartCoroutine(ShowResultAfterDelay(false));
        }

        private IEnumerator ShowResultAfterDelay(bool won)
        {
            yield return new WaitForSeconds(0.42f);
            State = won ? GameFlowState.Won : GameFlowState.Lost;
            _hud.ShowResult(won, won ? LoadNextLevel : RestartCurrentLevel);
        }

        private void RestartCurrentLevel()
        {
            if (!_initialized || _currentLevel == null || State == GameFlowState.Restarting) return;
            State = GameFlowState.Restarting;
            LoadLevel(_currentLevel.LevelNumber);
        }

        private void LoadNextLevel()
        {
            if (!_initialized || State == GameFlowState.Transitioning) return;
            State = GameFlowState.Transitioning;
            LoadLevel(_sequence.Next());
        }

        private void LoadLevel(int levelNumber)
        {
            _sessionVersion++;
            StopAllCoroutines();
            ClearLevelObjects();
            State = GameFlowState.LoadingLevel;
            _currentLevel = _catalog.GetLevel(levelNumber);
            List<string> errors = LevelValidator.Validate(_currentLevel);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"Level {levelNumber} is invalid:\n{string.Join("\n", errors)}");
            }

            _board = new BoardModel(_currentLevel);
            float aspect = Camera.main != null ? Camera.main.aspect : 9f / 19.5f;
#if UNITY_EDITOR
            if (_editorLayoutAspect > 0f) aspect = _editorLayoutAspect;
#endif
            _layout = _feel.ResolveLayout(_board.Width, _board.Height, aspect);
            _boardView = new BoardView(_board, _theme, _feel, _layout);
            _projectiles = new ProjectilePool(_theme, _feel);
            _impactFx = new ImpactFxPool(_theme, _feel);
            CreateSlots();
            CreateUnitQueues();
            _hud.HideResult();
            _hud.SetLevel(levelNumber);
            State = GameFlowState.Playing;
            for (int laneIndex = 0; laneIndex < _lanes.Length; laneIndex++)
            {
                RefreshLane(laneIndex, false);
            }
        }

        private void CreateSlots()
        {
            _slotsRoot = new GameObject("ActiveSlots");
            for (int i = 0; i < _slots.Length; i++)
            {
                float x = _layout.SlotX(i, _slots.Length);
                Vector3 position = new(x, _layout.SlotY, 0.18f);
                _slotPositions[i] = new Vector3(x, _layout.SlotY + 0.14f, -0.08f);
                CreateSlotPart(
                    $"Slot_{i + 1}_Outer",
                    position,
                    new Vector3(_feel.SlotOuterSize.x, _feel.SlotOuterSize.y, 0.22f),
                    _theme.Slot);
                CreateSlotPart(
                    $"Slot_{i + 1}_Inner",
                    position + new Vector3(0f, 0f, -0.15f),
                    new Vector3(_feel.SlotInnerSize.x, _feel.SlotInnerSize.y, 0.14f),
                    _theme.SlotInner);
            }
        }

        private void CreateUnitQueues()
        {
            _unitsRoot = new GameObject("UnitQueues");

            for (int laneIndex = 0; laneIndex < _lanes.Length; laneIndex++)
            {
                RuntimeLane lane = new();
                _lanes[laneIndex] = lane;
                IReadOnlyList<UnitDefinition> definitions = _currentLevel.Lanes[laneIndex].Units;
                for (int order = 0; order < definitions.Count; order++)
                {
                    UnitDefinition definition = definitions[order];
                    Vector3 position = QueuePosition(laneIndex, order);
                    UnitView view = new(
                        _unitsRoot.transform,
                        definition.Color,
                        definition.Charges,
                        position,
                        _theme,
                        _feel);
                    RuntimeUnit unit = new()
                    {
                        Definition = definition,
                        View = view,
                        LaneIndex = laneIndex,
                        OrderIndex = order,
                        Charges = definition.Charges,
                        State = UnitRuntimeState.Queued
                    };
                    RuntimeUnit captured = unit;
                    view.ClickTarget.Clicked = () => SelectUnit(captured);
                    lane.Units.Add(unit);
                }

                RefreshLane(laneIndex, false);
            }
        }

        private void RefreshLane(int laneIndex, bool animate)
        {
            RuntimeLane lane = _lanes[laneIndex];
            for (int index = lane.FrontIndex; index < lane.Units.Count; index++)
            {
                RuntimeUnit unit = lane.Units[index];
                int relative = index - lane.FrontIndex;
                Vector3 target = QueuePosition(laneIndex, relative);
                unit.View.SetQueueState(relative, relative == 0 && State == GameFlowState.Playing);
                if (animate) StartCoroutine(unit.View.SlideTo(target, _feel.QueueSlideDuration));
                else unit.View.Transform.position = target;
            }
        }

        private Vector3 QueuePosition(int laneIndex, int relativeOrder)
        {
            float x = _layout.QueueX(laneIndex, _lanes.Length);
            return new Vector3(
                x,
                _layout.QueueFrontY - relativeOrder * _layout.QueueSpacing,
                0.08f + relativeOrder * 0.09f);
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) return i;
            }
            return -1;
        }

        private void CreateSlotPart(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = new(name);
            part.transform.SetParent(_slotsRoot.transform, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = ChamferedCubeMesh.Get();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void ClearLevelObjects()
        {
            _projectiles?.Destroy();
            _impactFx?.Destroy();
            _boardView?.Destroy();
            if (_unitsRoot != null) Destroy(_unitsRoot);
            if (_slotsRoot != null) Destroy(_slotsRoot);
            _projectiles = null;
            _impactFx = null;
            _boardView = null;
            _board = null;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            for (int i = 0; i < _lanes.Length; i++) _lanes[i] = null;
        }
    }
}
