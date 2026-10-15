using System;
using System.Reflection;
using Core.Activities;
using Core.Projection;
using Core.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Tests.EditMode
{
    public sealed class BoardItemLifecycleEditModeTests
    {
        [Test]
        public void ReusedBoardItem_LateOldUnbindDoesNotClearNewCell()
        {
            var cellType = Type.GetType("ChainRush.Board.BoardCellView, Assembly-CSharp", true);
            var itemType = Type.GetType("ChainRush.Board.BoardItemView, Assembly-CSharp", true);
            var first = new GameObject("First cell", typeof(RectTransform));
            var second = new GameObject("Second cell", typeof(RectTransform));
            var view = new GameObject("Reused board item");
            try
            {
                var firstCell = first.AddComponent(cellType); var secondCell = second.AddComponent(cellType);
                view.transform.SetParent(first.transform, false);
                var item = (IProjectionBindingConsumer)view.AddComponent(itemType);
                var activity = new ActivityId(701);
                var oldContext = new ProjectionBindingContext(new ProjectionInstanceHandle(activity,
                    new EntityId(702), new MaterializationKey(activity, 1)), default, Vector3.zero);
                var newContext = new ProjectionBindingContext(new ProjectionInstanceHandle(activity,
                    new EntityId(703), new MaterializationKey(activity, 2)), default, Vector3.zero);
                item.OnProjectionBound(oldContext); item.OnProjectionUnbound(oldContext);
                Assert.AreEqual(EntityId.Invalid, cellType.GetProperty("EntityId").GetValue(firstCell));
                view.transform.SetParent(second.transform, false); item.OnProjectionBound(newContext);
                item.OnProjectionUnbound(oldContext);
                Assert.AreEqual(newContext.Handle.EntityId, cellType.GetProperty("EntityId").GetValue(secondCell));
                item.OnProjectionUnbound(newContext);
                Assert.AreEqual(EntityId.Invalid, cellType.GetProperty("EntityId").GetValue(secondCell));
            }
            finally { Object.DestroyImmediate(view); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
    }
}
