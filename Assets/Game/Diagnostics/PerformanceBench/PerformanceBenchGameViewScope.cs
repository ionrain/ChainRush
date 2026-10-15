#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    // minimum seam: a run owns a temporary Editor size selection, not a scene/camera mutation.
    internal sealed class PerformanceBenchGameViewScope : IDisposable
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly EditorWindow _previousFocus, _view;
        readonly PropertyInfo _selected;
        readonly object _group, _createdSize;
        readonly int _previousIndex, _createdIndex = -1;
        readonly bool _opened;
        bool _disposed, _selectionChanged;

        internal PerformanceBenchGameViewScope()
        {
            try
            {
            var assembly = typeof(EditorWindow).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            _previousFocus = EditorWindow.focusedWindow;
            var existing = Resources.FindObjectsOfTypeAll(viewType);
            _opened = existing.Length == 0;
            _view = _opened ? EditorWindow.GetWindow(viewType) : (EditorWindow)existing[0];
            _selected = viewType.GetProperty("selectedSizeIndex", Instance)
                ?? throw new MissingMemberException("Unity GameView.selectedSizeIndex is unavailable.");
            _previousIndex = (int)_selected.GetValue(_view);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            _group = sizesType.GetProperty("currentGroup", Instance).GetValue(sizes);
            int builtins = (int)Call("GetBuiltinCount"), custom = (int)Call("GetCustomCount");
            int selected = -1;
            for (int i = 0; i < builtins + custom; i++)
            {
                object size = Call("GetGameViewSize", i); var type = size.GetType();
                if ((int)type.GetProperty("width", Instance).GetValue(size) == 923
                    && (int)type.GetProperty("height", Instance).GetValue(size) == 1870
                    && type.GetProperty("sizeType", Instance).GetValue(size).ToString() == "FixedResolution")
                { selected = i; break; }
            }
            if (selected < 0)
            {
                var type = assembly.GetType("UnityEditor.GameViewSize", true);
                var sizeType = assembly.GetType("UnityEditor.GameViewSizeType", true);
                _createdSize = Activator.CreateInstance(type, Instance, null,
                    new[] { Enum.Parse(sizeType, "FixedResolution"), (object)923, 1870, "PerformanceBench 923x1870" }, null);
                Call("AddCustomSize", _createdSize); _createdIndex = custom; selected = builtins + custom;
            }
            _selectionChanged = true; Select(selected); _view.Focus(); _view.Repaint();
            }
            catch { Dispose(); throw; }
        }
        void Select(int index) => _view.GetType().GetMethod("SizeSelectionCallback", Instance)
            .Invoke(_view, new object[] { index, null });

        object Call(string name, params object[] args) => _group.GetType().GetMethod(name, Instance).Invoke(_group, args);

        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try
            {
                if (_view != null && _selectionChanged) Select(_previousIndex);
                if (_createdIndex >= 0)
                {
                    int builtins = (int)Call("GetBuiltinCount"), custom = (int)Call("GetCustomCount");
                    if (_createdIndex < custom && ReferenceEquals(Call("GetGameViewSize", builtins + _createdIndex), _createdSize))
                        Call("RemoveCustomSize", builtins + _createdIndex);
                }
            }
            finally
            {
                if (_opened && _view != null) _view.Close();
                if (_previousFocus != null) _previousFocus.Focus();
            }
        }
    }
}
#endif
