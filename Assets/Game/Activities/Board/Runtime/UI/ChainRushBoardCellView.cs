using Core.Activities;
using Core.Entities;
using Core.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Board
{
    [DisallowMultipleComponent]
    public sealed class ChainRushBoardCellView :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] Image background;
        [SerializeField] Color normalColor = new Color(0.12f, 0.14f, 0.18f, 0.92f);
        [SerializeField] Color selectedColor = new Color(0.25f, 0.85f, 0.95f, 1f);
        [SerializeField] UnityEvent onSelected = new UnityEvent();
        [SerializeField] UnityEvent onDeselected = new UnityEvent();

        ChainRushBoardUIController _controller;
        ChainRushBoardItemView _itemView;
        EntityId _entityId;
        bool _selected;

        public RectTransform RectTransform => transform as RectTransform;
        public Vector2Int GridCoordinates { get; private set; }
        public WorldPosition WorldPosition { get; private set; }
        public EntityId EntityId => _entityId;

        internal void Bind(ChainRushBoardUIController controller, ActivityUICell cell)
        {
            _controller = controller;
            GridCoordinates = cell.GridCoordinates;
            WorldPosition = cell.WorldPosition;
            if (background != null)
                background.color = normalColor;
            name = string.Concat(
                "BoardCell_",
                cell.GridCoordinates.x.ToString(),
                "_",
                cell.GridCoordinates.y.ToString());
        }

        internal bool BindItem(ChainRushBoardItemView itemView, EntityId entityId)
        {
            if (itemView == null
                || !entityId.IsValid
                || (_entityId.IsValid && _entityId != entityId))
            {
                return false;
            }

            _itemView = itemView;
            _entityId = entityId;
            _controller?.OnCellEntityAdded();
            return true;
        }

        internal void UnbindItem(ChainRushBoardItemView itemView, EntityId entityId)
        {
            if (_itemView != itemView || !_entityId.IsValid || _entityId != entityId)
                return;

            _itemView = null;
            _entityId = EntityId.Invalid;
            SetSelected(false);
            _controller?.OnCellEntityRemoved(this, entityId);
        }

        internal void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            if (background != null)
                background.color = selected ? selectedColor : normalColor;
            if (selected)
                onSelected.Invoke();
            else
                onDeselected.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _controller?.BeginSelection(this);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _controller?.EndSelection();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging)
                _controller?.ExtendSelection(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _controller?.EndSelection();
        }
    }
}
