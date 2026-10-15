#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ChainRush.Diagnostics;
using Core;
using Core.GameRuntime;
using Core.Players;
using Core.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;

namespace ChainRush.Editor
{
    public static partial class PerformanceBenchAuthoring
    {
        static void CreateScene(GameRuntimeProfileData profile, GameStartupPlanData startup,
            GameDeterminismConfigData determinism, PlayerData owner)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PerformanceBenchRuntime");
            var host = root.AddComponent<GameRuntimeHost>();
            Set(host, "profile", profile); Set(host, "startupPlan", startup); Set(host, "determinism", determinism);
            Set(host, "owner", owner); Set(host, "startPoolSession", true); Set(host, "autoInitialize", true);
            Set(host, "unlockFrameRate", false); root.AddComponent<PerformanceBenchPulseController>();
            var cameraRoot = new GameObject("BenchCamera"); cameraRoot.transform.SetParent(root.transform);
            var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 21;
            camera.transform.position = new Vector3(0, 0, -50); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .04f, .055f); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            cameraRoot.tag = "MainCamera";
            cameraRoot.AddComponent<PerformanceBenchCameraController>();
            var eventRoot = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventRoot.transform.SetParent(root.transform);
            var canvasRoot = new GameObject("BenchUI", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasRoot.transform.SetParent(root.transform);
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var controller = canvasRoot.AddComponent<PerformanceBenchUIController>();
            controller.font = CreateFont();
            var left = Panel(canvasRoot.transform, "Controls", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(440, 1048));
            var right = Panel(canvasRoot.transform, "Series", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(440, 1048));
            Label(left, "CHAINRUSH / PERFORMANCE BENCH", 26, new Vector2(14, -14), new Vector2(412, 36), controller.font);
            float y = 62;
            controller.scenario = Dropdown(left, "Scenario", new List<string> { "Lifecycle", "AI + movement", "AI + navigation", "Render control", "Churn + movement" }, ref y, controller.font);
            controller.scenario.value = (int)PerformanceBenchScenarioType.AI;
            controller.pool = Dropdown(left, "Pool", new List<string> { "Warm", "Expansion" }, ref y, controller.font);
            controller.movementBudget = Dropdown(left, "Movement budget", new List<string> { "64 per step", "Whole population" }, ref y, controller.font);
            controller.movementBudget.value = (int)PerformanceBenchMovementBudgetType.Population;
            var defaults = new PerformanceBenchSettings();
            AddField(controller, left, PerformanceBenchFieldType.Population, "Objects", defaults.population, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Visible, "Visible objects", defaults.visiblePopulation, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Batch, "Batch size", defaults.batchSize, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Interval, "Interval (seconds)", defaults.interval, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Lifetime, "Lifetime (seconds)", defaults.lifetime, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Speed, "Speed (units/second)", defaults.speed, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Density, "Density", defaults.density, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Replacement, "Replacement (%)", defaults.replacementPercent, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Warmup, "Warmup (seconds)", defaults.warmup, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Duration, "Measure (seconds)", defaults.duration, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Repetitions, "Series repetitions", defaults.repetitions, ref y);
            AddField(controller, left, PerformanceBenchFieldType.Seed, "Seed", defaults.seed, ref y);
            AddField(controller, left, PerformanceBenchFieldType.InitialPool, "Initial pool capacity", defaults.initialPoolCapacity, ref y);
            controller.trace = PerformanceBenchUIController.CreateSeriesRow(left, "Detailed trace (separate results)", controller.font);
            Place((RectTransform)controller.trace.transform, new Vector2(14, -y), new Vector2(412, 32)); y += 42;
            int commandIndex = 0;
            foreach (PerformanceBenchCommandType command in Enum.GetValues(typeof(PerformanceBenchCommandType)))
            {
                string text = command switch { PerformanceBenchCommandType.CreateBatch => "Create batch", PerformanceBenchCommandType.DeleteBatch => "Delete batch",
                    PerformanceBenchCommandType.RunSeries => "Run selected series", _ => command.ToString() };
                var button = Button(left, text, new Vector2(14 + commandIndex % 2 * 210, -y - commandIndex / 2 * 42), new Vector2(202, 36), controller.font);
                controller.commands.Add(new PerformanceBenchUIButton { command = command, button = button }); commandIndex++;
            }
            y += 180;
            controller.status = Label(left, "Idle. Start a workload or select a series.\nAll actors remain simulated outside the camera.\nUse . for decimals. Deep Profile must be OFF.", 16,
                new Vector2(14, -y), new Vector2(412, Math.Max(80, 1030 - y)), controller.font);
            Label(right, "REPEATABLE SERIES", 26, new Vector2(14, -14), new Vector2(412, 36), controller.font);
            controller.seriesEstimate = Label(right, "Select configurations explicitly.", 16, new Vector2(14, -60), new Vector2(412, 70), controller.font);
            controller.addConfiguration = Button(right, "Add current settings to series", new Vector2(14, -140), new Vector2(412, 36), controller.font);
            controller.seriesContent = ScrollContent(right, controller.font);
            foreach (PerformanceBenchScenarioType scenario in Enum.GetValues(typeof(PerformanceBenchScenarioType)))
            foreach (int population in new[] { 1000, 5000, 10000 })
            {
                var settings = new PerformanceBenchSettings { scenario = scenario, population = population };
                AddSeries(controller, settings);
                if (settings.Moves)
                { var current = settings.Copy(); current.movementBudget = PerformanceBenchMovementBudgetType.Current64; AddSeries(controller, current); }
                if (scenario == PerformanceBenchScenarioType.Lifecycle)
                    foreach (int batch in new[] { 1, 10, 100, 1000 })
                    foreach (PerformanceBenchPoolType pool in Enum.GetValues(typeof(PerformanceBenchPoolType)))
                        AddSeries(controller, new PerformanceBenchSettings { scenario = scenario, population = population, batchSize = batch, pool = pool });
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Failed to save PerformanceBench scene.");
            Created.Add(ScenePath);
        }
        static void AddSeries(PerformanceBenchUIController controller, PerformanceBenchSettings settings)
        {
            controller.series.Add(new PerformanceBenchSeriesEntry { settings = settings });
            controller.seriesSelection.Add(PerformanceBenchUIController.CreateSeriesRow(controller.seriesContent, PerformanceBenchUIController.Describe(settings), controller.font));
        }
        static TMP_FontAsset CreateFont()
        {
            var source = Require<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 36, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            font.name = "BenchUIFont"; AssetDatabase.CreateAsset(font, Content + "/BenchUIFont.asset"); Created.Add(Content + "/BenchUIFont.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;/%()+-_!?=\nАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя");
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = root.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.05f, .09f, .12f, .96f); image.raycastTarget = false; return rect;
        }
        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; }
        static TMP_Text Label(Transform parent, string text, float fontSize, Vector2 position, Vector2 size, TMP_FontAsset font)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, position, size);
            var label = root.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = fontSize; label.text = text;
            label.color = new Color(.88f, .94f, .97f); label.raycastTarget = false; return label;
        }
        static TMP_Dropdown Dropdown(Transform parent, string text, List<string> options, ref float y, TMP_FontAsset font)
        {
            Label(parent, text, 17, new Vector2(14, -y), new Vector2(170, 30), font);
            var root = TMP_DefaultControls.CreateDropdown(default); root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, new Vector2(190, -y), new Vector2(236, 32));
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) { label.font = font; label.fontSize = 16; }
            var dropdown = root.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(options); y += 42; return dropdown;
        }
        static void AddField(PerformanceBenchUIController controller, Transform parent, PerformanceBenchFieldType type, string text, double value, ref float y)
        {
            Label(parent, text, 17, new Vector2(14, -y), new Vector2(210, 30), controller.font);
            var root = TMP_DefaultControls.CreateInputField(default); root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, new Vector2(230, -y), new Vector2(196, 30));
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) { label.font = controller.font; label.fontSize = 17; }
            var input = root.GetComponent<TMP_InputField>(); input.text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            controller.fields.Add(new PerformanceBenchUIField { type = type, input = input }); y += 35;
        }
        static UnityEngine.UI.Button Button(Transform parent, string text, Vector2 position, Vector2 size, TMP_FontAsset font)
        {
            var root = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            root.transform.SetParent(parent, false); Place((RectTransform)root.transform, position, size);
            var image = root.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.12f, .25f, .32f);
            var button = root.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Label(root.transform, text, 16, Vector2.zero, size, font); label.alignment = TextAlignmentOptions.Center; return button;
        }
        static RectTransform ScrollContent(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("ScrollView", typeof(RectTransform), typeof(UnityEngine.UI.ScrollRect)); root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, new Vector2(14, -190), new Vector2(412, 835));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask)); viewport.transform.SetParent(root.transform, false);
            var viewportRect = (RectTransform)viewport.transform; viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero; viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var rect = (RectTransform)content.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = 4; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.GetComponent<UnityEngine.UI.ScrollRect>(); scroll.content = rect; scroll.viewport = viewportRect; scroll.horizontal = false; scroll.vertical = true;
            return rect;
        }
    }
}
#endif
