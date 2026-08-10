using Core.Entities;
using Core.Projection;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Board
{
    [DisallowMultipleComponent]
    public sealed class ChainRushBoardItemView : MonoBehaviour, IProjectionBindingConsumer
    {
        ChainRushBoardCellView _cell;
        EntityId _entityId;

        public void OnProjectionBound(ProjectionBindingContext context)
        {
            if (!context.Handle.EntityId.IsValid)
                return;

            ChainRushBoardCellView cell = GetComponentInParent<ChainRushBoardCellView>();
            if (cell == null || !cell.BindItem(this, context.Handle.EntityId))
                return;

            _cell = cell;
            _entityId = context.Handle.EntityId;
        }

        public void OnProjectionUnbound(ProjectionBindingContext context)
        {
            if (_cell == null || !_entityId.IsValid || context.Handle.EntityId != _entityId)
                return;

            _cell.UnbindItem(this, _entityId);
            _cell = null;
            _entityId = EntityId.Invalid;
        }
    }
}
