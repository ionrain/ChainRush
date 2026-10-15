#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using Core.Events;
using TMPro;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    public enum PerformanceBenchFieldType
    { Population, Visible, Batch, Interval, Lifetime, Speed, Density, Replacement, Warmup, Duration, Repetitions, Seed, InitialPool }
    [Serializable] public sealed class PerformanceBenchUIField
    {
        public PerformanceBenchFieldType type;
        public TMP_InputField input;
    }
    [Serializable] public sealed class PerformanceBenchUIButton
    {
        public PerformanceBenchCommandType command;
        public UnityEngine.UI.Button button;
    }

    public sealed class PerformanceBenchUIController : MonoBehaviour, IEventListener<PerformanceBenchStateEvent>
    {
        public TMP_Dropdown scenario;
        public TMP_Dropdown pool;
        public TMP_Dropdown movementBudget;
        public UnityEngine.UI.Toggle trace;
        public TMP_Text status;
        public TMP_Text seriesEstimate;
        public List<PerformanceBenchUIField> fields = new();
        public List<PerformanceBenchUIButton> commands = new();
        public List<PerformanceBenchSeriesEntry> series = new();
        public List<UnityEngine.UI.Toggle> seriesSelection = new();
        public UnityEngine.UI.Button addConfiguration;
        public RectTransform seriesContent;
        public TMP_FontAsset font;

        void Awake()
        {
            foreach (var command in commands)
            {
                var captured = command.command;
                command.button.onClick.AddListener(() => Send(captured));
            }
            for (int i = 0; i < seriesSelection.Count; i++) WireSelection(i);
            addConfiguration.onClick.AddListener(AddConfiguration);
            RefreshEstimate();
        }
        void OnEnable() { if (Application.isPlaying) EventBus.Register<PerformanceBenchStateEvent>(this); }
        void OnDisable() { if (Application.isPlaying) EventBus.Unregister<PerformanceBenchStateEvent>(this); }
        void WireSelection(int index)
        {
            seriesSelection[index].onValueChanged.AddListener(selected =>
            { series[index].selected = selected; RefreshEstimate(); });
        }
        void Send(PerformanceBenchCommandType command)
        {
            try
            {
                if (command == PerformanceBenchCommandType.RunSeries)
                    EventBus.Trigger(new PerformanceBenchCommandEvent(command, series: PerformanceBenchSeries.Freeze(series)));
                else if (command == PerformanceBenchCommandType.Start)
                    EventBus.Trigger(new PerformanceBenchCommandEvent(command, ReadSettings()));
                else EventBus.Trigger(new PerformanceBenchCommandEvent(command));
            }
            catch (Exception exception) { status.text = exception.Message; }
        }
        public PerformanceBenchSettings ReadSettings()
        {
            var value = new PerformanceBenchSettings
            {
                scenario = (PerformanceBenchScenarioType)scenario.value, pool = (PerformanceBenchPoolType)pool.value,
                movementBudget = (PerformanceBenchMovementBudgetType)movementBudget.value, trace = trace.isOn,
            };
            foreach (var field in fields)
            {
                if (!double.TryParse(field.input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    || double.IsNaN(number) || double.IsInfinity(number))
                    throw new ArgumentException("Некорректное число: " + field.type);
                int Integer()
                {
                    if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
                        throw new ArgumentException("Нужно целое число: " + field.type);
                    return (int)number;
                }
                switch (field.type)
                {
                    case PerformanceBenchFieldType.Population: value.population = Integer(); break;
                    case PerformanceBenchFieldType.Visible: value.visiblePopulation = Integer(); break;
                    case PerformanceBenchFieldType.Batch: value.batchSize = Integer(); break;
                    case PerformanceBenchFieldType.Interval: value.interval = (float)number; break;
                    case PerformanceBenchFieldType.Lifetime: value.lifetime = (float)number; break;
                    case PerformanceBenchFieldType.Speed: value.speed = (float)number; break;
                    case PerformanceBenchFieldType.Density: value.density = (float)number; break;
                    case PerformanceBenchFieldType.Replacement: value.replacementPercent = (float)number; break;
                    case PerformanceBenchFieldType.Warmup: value.warmup = (float)number; break;
                    case PerformanceBenchFieldType.Duration: value.duration = (float)number; break;
                    case PerformanceBenchFieldType.Repetitions: value.repetitions = Integer(); break;
                    case PerformanceBenchFieldType.Seed: value.seed = Integer(); break;
                    case PerformanceBenchFieldType.InitialPool: value.initialPoolCapacity = Integer(); break;
                }
            }
            if (!value.TryValidate(out string failure)) throw new ArgumentException(failure);
            return value;
        }
        void AddConfiguration()
        {
            try
            {
                var settings = ReadSettings();
                var entry = new PerformanceBenchSeriesEntry { settings = settings, selected = false };
                var selection = CreateSeriesRow(seriesContent, Describe(settings), font);
                series.Add(entry); seriesSelection.Add(selection); WireSelection(series.Count - 1); RefreshEstimate();
            }
            catch (Exception exception) { status.text = exception.Message; }
        }
        public static string Describe(PerformanceBenchSettings value) =>
            $"{value.scenario} / {value.population} / {value.movementBudget} / {value.pool} / {(value.trace ? "trace" : "normal")} / {value.repetitions}x";
        void RefreshEstimate()
        {
            double duration = 0; int runs = 0;
            foreach (var entry in series) if (entry.selected)
            { runs += entry.settings.repetitions; duration += entry.settings.repetitions * (entry.settings.warmup + entry.settings.duration); }
            seriesEstimate.text = $"Selected runs: {runs}; measurement + warmup: {duration / 60:F1} min\nPreparation and cleanup add time. Select rows explicitly.";
        }
        public void OnEvent(PerformanceBenchStateEvent e)
        {
            bool active = e.Phase >= PerformanceBenchPhaseType.Preparing && e.Phase <= PerformanceBenchPhaseType.Cleanup;
            scenario.interactable = pool.interactable = movementBudget.interactable = trace.interactable = !active;
            addConfiguration.interactable = !active;
            foreach (var field in fields) field.input.interactable = !active;
            foreach (var selection in seriesSelection) selection.interactable = !active;
            foreach (var command in commands)
                command.button.interactable = command.command == PerformanceBenchCommandType.Stop || command.command == PerformanceBenchCommandType.Clear
                    ? active : command.command == PerformanceBenchCommandType.CreateBatch || command.command == PerformanceBenchCommandType.DeleteBatch
                    ? e.Phase == PerformanceBenchPhaseType.Measuring && e.Pending == 0 : !active;
            status.text = $"Run {e.Run}: {e.Phase}   {e.Elapsed:F1}s\nEntity {e.Population}/{e.RequestedPopulation}; ready views {e.Bound}/{e.RequestedPopulation}; visible {e.Visible}\nPending {e.Pending}; frame {e.FrameMs:F2} ms; completed {e.CompletedRuns}\nMoved/waiting this frame: {e.Moved}/{e.Waiting}; budget {e.MovementBudget}\nRoutes OK/failed: {e.RoutesBuilt}/{e.RoutesFailed}; occupancy violations {e.OccupancyViolations}\nStop: {e.StopReason}\n{e.Message}";
        }

        public static UnityEngine.UI.Toggle CreateSeriesRow(RectTransform parent, string text, TMP_FontAsset font)
        {
            var root = new GameObject("Configuration", typeof(RectTransform), typeof(UnityEngine.UI.Toggle));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(420, 34);
            var layout = root.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = 34;
            var background = new GameObject("Box", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(rect, false);
            var boxRect = (RectTransform)background.transform; boxRect.anchorMin = boxRect.anchorMax = new Vector2(0, .5f);
            boxRect.pivot = new Vector2(0, .5f); boxRect.sizeDelta = new Vector2(22, 22);
            var box = background.GetComponent<UnityEngine.UI.Image>(); box.color = new Color(.15f, .22f, .28f);
            var check = new GameObject("Check", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            check.transform.SetParent(boxRect, false);
            var checkRect = (RectTransform)check.transform; checkRect.anchorMin = Vector2.zero; checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(5, 5); checkRect.offsetMax = new Vector2(-5, -5);
            var graphic = check.GetComponent<UnityEngine.UI.Image>(); graphic.color = new Color(.2f, .85f, .6f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(rect, false);
            var labelRect = (RectTransform)label.transform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(30, 0); labelRect.offsetMax = Vector2.zero;
            var tmp = label.GetComponent<TextMeshProUGUI>(); tmp.font = font; tmp.fontSize = 13; tmp.text = text; tmp.raycastTarget = false;
            var toggle = root.GetComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = box; toggle.graphic = graphic;
            toggle.isOn = false; return toggle;
        }
    }
}
#endif
