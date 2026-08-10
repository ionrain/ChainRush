using System;
using System.Collections.Generic;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Entities;
using Core.Events;
using Core.SimulationControl;
using Core.Skills;
using Core.UI.Flow;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Core.Entities.EntityId;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Board
{
    [DisallowMultipleComponent]
    public sealed class ChainRushBoardUIController :
        UIPresentationController,
        IEventListener<CapabilityHostRegisteredEvent>,
        IEventListener<CapabilityHostUnregisteredEvent>
    {
        [SerializeField] RectTransform gridRoot;
        [SerializeField] GridLayoutGroup gridLayout;
        [SerializeField] ChainRushBoardCellView cellPrefab;
        [SerializeField] CapabilityHostBaseData boardHostDefinition;
        [SerializeField] FrameworkSkillData mergeSkill;

        readonly List<ChainRushBoardCellView> _cells = new List<ChainRushBoardCellView>();
        readonly List<ChainRushBoardCellView> _selectedCells = new List<ChainRushBoardCellView>(4);
        readonly List<EntityId> _selectedEntities = new List<EntityId>(4);

        ActivityUIContext _context;
        EntityId _boardHostEntityId;
        bool _isSelecting;
        bool _selectionLocked;
        bool _awaitingBoardRefresh;

        void OnEnable()
        {
            EventBus.Register<CapabilityHostRegisteredEvent>(this);
            EventBus.Register<CapabilityHostUnregisteredEvent>(this);
        }

        void OnDisable()
        {
            EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
            EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
            ClearSelection();
            _isSelecting = false;
        }

        void OnDestroy()
        {
            ReleaseGrid();
        }

        public override bool TryApplyUIContext(IUIContext uiContext)
        {
            if (!(uiContext is ActivityUIContext activityUIContext)
                || !activityUIContext.ActivityId.IsValid
                || activityUIContext.ProjectionTarget == null
                || activityUIContext.ProjectionSettings == null
                || gridRoot == null
                || gridLayout == null
                || cellPrefab == null)
            {
                return false;
            }

            if (_context == activityUIContext && _cells.Count == activityUIContext.Cells.Count)
                return true;

            ReleaseGrid();
            _context = activityUIContext;
            ConfigureGrid(activityUIContext);
            _selectionLocked = true;
            _awaitingBoardRefresh = true;

            for (int i = 0; i < activityUIContext.Cells.Count; i++)
            {
                ActivityUICell cellData = activityUIContext.Cells[i];
                ChainRushBoardCellView cell = Instantiate(cellPrefab, gridRoot, false);
                cell.Bind(this, cellData);
                if (!activityUIContext.ProjectionTarget.RegisterCell(cellData.WorldPosition, cell.RectTransform))
                {
                    Destroy(cell.gameObject);
                    ReleaseGrid();
                    return false;
                }

                _cells.Add(cell);
            }

            TryUnlockAfterRefresh();
            return true;
        }

        public override void OnUILifecycleStateChanged(UIHandle uiHandle, UILifecycleState state)
        {
            if (state != UILifecycleState.Hiding
                && state != UILifecycleState.Unloading
                && state != UILifecycleState.Unloaded)
            {
                return;
            }

            ClearSelection();
            _isSelecting = false;
        }

        public void OnEvent(CapabilityHostRegisteredEvent e)
        {
            if (_context == null
                || !_context.ActivityId.IsValid
                || e.Snapshot.ActivityId != _context.ActivityId
                || e.Snapshot.PlacementType != CapabilityHostPlacementType.NonSpatial
                || !MatchesBoardHost(e.Snapshot.Definition))
            {
                return;
            }

            if (!_boardHostEntityId.IsValid || e.Snapshot.EntityId.Value < _boardHostEntityId.Value)
                _boardHostEntityId = e.Snapshot.EntityId;
        }

        public void OnEvent(CapabilityHostUnregisteredEvent e)
        {
            if (!_boardHostEntityId.IsValid || e.EntityId != _boardHostEntityId)
                return;

            _boardHostEntityId = EntityId.Invalid;
            _selectionLocked = false;
            _awaitingBoardRefresh = false;
            _isSelecting = false;
            ClearSelection();
        }

        internal void BeginSelection(ChainRushBoardCellView cell)
        {
            if (_selectionLocked || !CanSelect(cell))
                return;

            ClearSelection();
            _isSelecting = true;
            AddSelection(cell);
        }

        internal void ExtendSelection(ChainRushBoardCellView cell)
        {
            if (!_isSelecting
                || _selectionLocked
                || !CanSelect(cell)
                || _selectedCells.Count == 0
                || _selectedCells.Count >= mergeSkill.TargetCount.Max
                || _selectedEntities.Contains(cell.EntityId)
                || !AreAdjacent(_selectedCells[_selectedCells.Count - 1], cell))
            {
                return;
            }

            AddSelection(cell);
        }

        internal void EndSelection()
        {
            if (!_isSelecting)
                return;

            _isSelecting = false;
            if (!TrySubmitSelection())
                ClearSelection();
        }

        internal void OnCellEntityRemoved(ChainRushBoardCellView cell, EntityId entityId)
        {
            if (!_selectionLocked || !_selectedEntities.Contains(entityId))
                return;

            _isSelecting = false;
            ClearSelection();
        }

        internal void OnCellEntityAdded()
        {
            if (!_selectionLocked || !_awaitingBoardRefresh)
                return;

            TryUnlockAfterRefresh();
        }

        void TryUnlockAfterRefresh()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] == null || !_cells[i].EntityId.IsValid)
                    return;
            }

            _awaitingBoardRefresh = false;
            _selectionLocked = false;
        }

        bool TrySubmitSelection()
        {
            if (_context == null
                || !_context.ActivityId.IsValid
                || !_boardHostEntityId.IsValid
                || mergeSkill == null
                || !mergeSkill.TargetCount.Contains(_selectedEntities.Count))
            {
                return false;
            }

            _selectionLocked = true;
            _awaitingBoardRefresh = true;
            EventBus.Trigger(SimulationControlIntentEvent.ActivateSkillEntities(
                _context.ActivityId,
                _boardHostEntityId,
                mergeSkill,
                _selectedEntities));
            return true;
        }

        bool CanSelect(ChainRushBoardCellView cell)
        {
            return cell != null
                && cell.EntityId.IsValid
                && mergeSkill != null
                && mergeSkill.TargetCount.IsValid
                && mergeSkill.TargetCount.Max > 0;
        }

        void AddSelection(ChainRushBoardCellView cell)
        {
            _selectedCells.Add(cell);
            _selectedEntities.Add(cell.EntityId);
            cell.SetSelected(true);
        }

        void ClearSelection()
        {
            for (int i = 0; i < _selectedCells.Count; i++)
            {
                if (_selectedCells[i] != null)
                    _selectedCells[i].SetSelected(false);
            }

            _selectedCells.Clear();
            _selectedEntities.Clear();
        }

        void ConfigureGrid(ActivityUIContext context)
        {
            int columnCount = ResolveColumnCount(context.Cells);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Math.Max(1, columnCount);
            gridLayout.cellSize = context.ProjectionSettings.CellSize;
            gridLayout.spacing = context.ProjectionSettings.Spacing;
        }

        void ReleaseGrid()
        {
            ClearSelection();
            _isSelecting = false;
            _selectionLocked = false;
            _awaitingBoardRefresh = false;
            _boardHostEntityId = EntityId.Invalid;

            for (int i = 0; i < _cells.Count; i++)
            {
                ChainRushBoardCellView cell = _cells[i];
                if (cell == null)
                    continue;

                if (_context != null && _context.ProjectionTarget != null)
                {
                    _context.ProjectionTarget.UnregisterCell(
                        cell.WorldPosition,
                        cell.RectTransform);
                }

                Destroy(cell.gameObject);
            }

            _cells.Clear();
            _context = null;
        }

        bool MatchesBoardHost(CapabilityHostBaseData definition)
        {
            return boardHostDefinition != null
                && definition != null
                && string.Equals(
                    definition.Id,
                    boardHostDefinition.Id,
                    StringComparison.Ordinal);
        }

        static bool AreAdjacent(ChainRushBoardCellView left, ChainRushBoardCellView right)
        {
            Vector2Int delta = left.GridCoordinates - right.GridCoordinates;
            return delta != Vector2Int.zero
                && Math.Abs(delta.x) <= 1
                && Math.Abs(delta.y) <= 1;
        }

        static int ResolveColumnCount(IReadOnlyList<ActivityUICell> cells)
        {
            if (cells == null || cells.Count == 0)
                return 1;

            int min = cells[0].GridCoordinates.x;
            int max = min;
            for (int i = 1; i < cells.Count; i++)
            {
                int column = cells[i].GridCoordinates.x;
                min = Math.Min(min, column);
                max = Math.Max(max, column);
            }

            return checked(max - min + 1);
        }
    }
}
