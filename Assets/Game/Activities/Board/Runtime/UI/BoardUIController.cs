using System;
using System.Collections.Generic;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Entities;
using Core.Events;
using Core.Taxonomy;
using Core.UI.Flow;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Board
{
    [DisallowMultipleComponent]
    public sealed class BoardUIController :
        UIPresentationController,
        IEventListener<CapabilityHostRegisteredEvent>,
        IEventListener<CapabilityHostUnregisteredEvent>,
        IEventListener<SelectionProgressEvent>,
        IEventListener<SelectionResultEvent>
    {
        [SerializeField] RectTransform gridRoot;
        [SerializeField] GridLayoutGroup gridLayout;
        [SerializeField] BoardCellView cellPrefab;
        [SerializeField] CapabilityHostBaseData boardHostDefinition;
        [SerializeField] TaxonomyTermData selectionRequestType;

        readonly List<BoardCellView> _cells = new List<BoardCellView>();
        readonly List<BoardCellView> _selectedCells = new List<BoardCellView>(4);
        readonly List<EntityId> _selectedEntities = new List<EntityId>(4);

        ActivityUIContext _context;
        EntityId _boardHostEntityId;
        bool _isSelecting;
        bool _selectionLocked;
        bool _awaitingBoardRefresh;
        bool _boardRefreshObserved;
        SelectionIntentEvent _pendingBeginIntent;

        void OnEnable()
        {
            EventBus.Register<CapabilityHostRegisteredEvent>(this);
            EventBus.Register<CapabilityHostUnregisteredEvent>(this);
            EventBus.Register<SelectionProgressEvent>(this);
            EventBus.Register<SelectionResultEvent>(this);
        }

        void OnDisable()
        {
            CancelPendingSelection();
            EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
            EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
            EventBus.Unregister<SelectionProgressEvent>(this);
            EventBus.Unregister<SelectionResultEvent>(this);
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
            _boardRefreshObserved = true;

            for (int i = 0; i < activityUIContext.Cells.Count; i++)
            {
                ActivityUICell cellData = activityUIContext.Cells[i];
                BoardCellView cell = Instantiate(cellPrefab, gridRoot, false);
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
            CancelPendingSelection();
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
            _boardRefreshObserved = false;
            _isSelecting = false;
            CancelPendingSelection();
            ClearSelection();
        }

        public void OnEvent(SelectionProgressEvent e)
        {
            if (!MatchesPendingRequest(
                    e.RequestId,
                    e.ActivityId,
                    e.RequestType,
                    e.ReceiverEntityId))
            {
                return;
            }

            ApplySelectionProgress(e.AcceptedTargetEntityIds);
            if (e.InputClosed)
                _isSelecting = false;
        }

        public void OnEvent(SelectionResultEvent e)
        {
            if (!MatchesPendingRequest(
                    e.RequestId,
                    e.ActivityId,
                    e.RequestType,
                    e.ReceiverEntityId))
            {
                return;
            }

            ApplySelectionProgress(e.SelectedEntityIds);
            _pendingBeginIntent = default;
            _isSelecting = false;

            if (e.Type == SelectionResultType.Committed)
            {
                _selectionLocked = true;
                _awaitingBoardRefresh = true;
                _boardRefreshObserved = false;
                return;
            }

            _selectionLocked = false;
            _awaitingBoardRefresh = false;
            _boardRefreshObserved = false;
            ClearSelection();
        }

        bool MatchesPendingRequest(
            SelectionRequestId requestId,
            ActivityId activityId,
            TaxonomyTermData requestType,
            EntityId receiverEntityId)
        {
            return _pendingBeginIntent.RequestId.IsValid
                && requestId == _pendingBeginIntent.RequestId
                && _context != null
                && activityId == _context.ActivityId
                && receiverEntityId == _boardHostEntityId
                && selectionRequestType != null
                && requestType != null
                && selectionRequestType.Matches(requestType);
        }

        internal void BeginSelection(BoardCellView cell)
        {
            if (_selectionLocked || !CanSubmitTarget(cell))
                return;

            ClearSelection();
            _pendingBeginIntent = SelectionIntentEvent.Begin(
                _context.ActivityId,
                selectionRequestType,
                EntityId.Invalid,
                _boardHostEntityId);
            _isSelecting = true;
            EventBus.Trigger(_pendingBeginIntent);
            EventBus.Trigger(SelectionIntentEvent.Target(_pendingBeginIntent, cell.EntityId));
        }

        internal void ExtendSelection(BoardCellView cell)
        {
            if (!_isSelecting
                || _selectionLocked
                || !_pendingBeginIntent.RequestId.IsValid
                || !CanSubmitTarget(cell))
            {
                return;
            }

            EventBus.Trigger(SelectionIntentEvent.Target(_pendingBeginIntent, cell.EntityId));
        }

        internal void EndSelection()
        {
            if (!_isSelecting)
                return;

            _isSelecting = false;
            if (_pendingBeginIntent.RequestId.IsValid)
                EventBus.Trigger(SelectionIntentEvent.Complete(_pendingBeginIntent));
        }

        internal void OnCellEntityRemoved(BoardCellView cell, EntityId entityId)
        {
            if (!_selectionLocked || !_selectedEntities.Contains(entityId))
                return;

            _boardRefreshObserved = true;
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
            if (!_boardRefreshObserved)
                return;

            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] == null || !_cells[i].EntityId.IsValid)
                    return;
            }

            _awaitingBoardRefresh = false;
            _selectionLocked = false;
            _boardRefreshObserved = false;
            _pendingBeginIntent = default;
        }

        void CancelPendingSelection()
        {
            if (_pendingBeginIntent.RequestId.IsValid)
                EventBus.Trigger(SelectionIntentEvent.Cancel(_pendingBeginIntent));
            _pendingBeginIntent = default;
        }

        bool CanSubmitTarget(BoardCellView cell)
        {
            return cell != null
                && cell.EntityId.IsValid
                && _context != null
                && _context.ActivityId.IsValid
                && _boardHostEntityId.IsValid
                && selectionRequestType != null;
        }

        void ApplySelectionProgress(List<EntityId> selectedEntityIds)
        {
            ClearSelection();
            for (int entityIndex = 0;
                selectedEntityIds != null && entityIndex < selectedEntityIds.Count;
                entityIndex++)
            {
                EntityId entityId = selectedEntityIds[entityIndex];
                for (int cellIndex = 0; cellIndex < _cells.Count; cellIndex++)
                {
                    BoardCellView cell = _cells[cellIndex];
                    if (cell == null || cell.EntityId != entityId)
                        continue;

                    _selectedCells.Add(cell);
                    _selectedEntities.Add(entityId);
                    cell.SetSelected(true);
                    break;
                }
            }
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
            _boardRefreshObserved = false;
            _boardHostEntityId = EntityId.Invalid;
            CancelPendingSelection();

            for (int i = 0; i < _cells.Count; i++)
            {
                BoardCellView cell = _cells[i];
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
