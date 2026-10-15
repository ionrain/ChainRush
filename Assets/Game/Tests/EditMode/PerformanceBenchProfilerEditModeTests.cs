#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Core.EditorTools;
using Core.Events;
using NUnit.Framework;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace ChainRush.Tests.EditMode
{
    public sealed partial class PerformanceBenchProfilerEditModeTests
    {
        static Type BenchType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetType("ChainRush.Diagnostics." + name) is Type type) return type;
            throw new InvalidOperationException("Bench type is unavailable: " + name);
        }

        static void Publish(object value)
        {
            typeof(EventBus).GetMethod("Trigger").MakeGenericMethod(value.GetType()).Invoke(null, new[] { value });
        }

        [Test]
        public void NormalAdapter_DisablesRecordingAndRestoresAllSettingsOnDispose()
        {
            bool enabled = Profiler.enabled, binary = Profiler.enableBinaryLog;
            bool deep = ProfilerDriver.deepProfiling, editor = ProfilerDriver.profileEditor;
            bool stacks = Profiler.enableAllocationCallstacks;
            string directory = Path.Combine(Path.GetTempPath(), "bench-profiler-" + Guid.NewGuid().ToString("N"));
            var owner = new object();
            var adapter = (IDisposable)Activator.CreateInstance(BenchType("PerformanceBenchProfilerAdapter"),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { owner }, null);
            try
            {
                var phase = Enum.Parse(BenchType("PerformanceBenchPhaseType"), "Preparing");
                var capture = Enum.Parse(BenchType("PerformanceBenchCaptureType"), "Normal");
                var budget = new ProfilerDumpStorageBudget(directory);
                var message = Activator.CreateInstance(BenchType("PerformanceBenchRunPhaseEvent"),
                    owner, 1, phase, capture, directory, "{}", Path.GetFullPath(Path.Combine(Application.dataPath, "..")), budget);
                Publish(message);
                Assert.IsFalse(Profiler.enabled); Assert.IsFalse(Profiler.enableBinaryLog);
                Assert.IsFalse(ProfilerDriver.deepProfiling); Assert.IsFalse(Profiler.enableAllocationCallstacks);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.raw", SearchOption.AllDirectories).Length);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.data", SearchOption.AllDirectories).Length);
            }
            finally
            {
                adapter.Dispose(); Directory.Delete(directory, true);
                Assert.AreEqual(enabled, Profiler.enabled); Assert.AreEqual(binary, Profiler.enableBinaryLog);
                Assert.AreEqual(deep, ProfilerDriver.deepProfiling); Assert.AreEqual(editor, ProfilerDriver.profileEditor);
                Assert.AreEqual(stacks, Profiler.enableAllocationCallstacks);
            }
        }
        [Test]
        public void CPUAdapter_PreparesContextBeforeRecording_AndAssemblyRetirementRestoresSettings()
        {
            bool enabled = Profiler.enabled, binary = Profiler.enableBinaryLog;
            bool deep = ProfilerDriver.deepProfiling, editor = ProfilerDriver.profileEditor, stacks = Profiler.enableAllocationCallstacks;
            string directory = Path.Combine(Path.GetTempPath(), "bench-window-" + Guid.NewGuid().ToString("N"));
            var owner = new object(); var type = BenchType("PerformanceBenchProfilerAdapter");
            var adapter = (IDisposable)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { owner }, null);
            try
            {
                var capture = Enum.Parse(BenchType("PerformanceBenchCaptureType"), "CPUWindow");
                var budget = new ProfilerDumpStorageBudget(directory);
                foreach (string name in new[] { "Preparing", "Measuring" })
                    Publish(Activator.CreateInstance(BenchType("PerformanceBenchRunPhaseEvent"), owner, 1,
                        Enum.Parse(BenchType("PerformanceBenchPhaseType"), name), capture, directory, "{}",
                        Path.GetFullPath(Path.Combine(Application.dataPath, "..")), budget));
                var preparation = (ProfilerCapturePreparation)type.GetField("_preparation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter);
                Assert.IsNotNull(preparation); Assert.IsFalse(preparation.profilerEnabled);
                Assert.AreEqual("recorded", preparation.status, preparation.reason);
                Assert.IsTrue(Profiler.enabled); Assert.IsFalse(Profiler.enableBinaryLog);
                Assert.IsTrue(File.Exists(Path.Combine(directory, "capture-preparation.json")));
                type.GetMethod("OnAssembliesRetiring", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
                Assert.AreEqual(enabled, Profiler.enabled); Assert.AreEqual(binary, Profiler.enableBinaryLog);
                Assert.AreEqual(deep, ProfilerDriver.deepProfiling); Assert.AreEqual(editor, ProfilerDriver.profileEditor);
                Assert.AreEqual(stacks, Profiler.enableAllocationCallstacks);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.raw", SearchOption.AllDirectories).Length);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.data", SearchOption.AllDirectories).Length);
            }
            finally { adapter.Dispose(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

    }
}
#endif
