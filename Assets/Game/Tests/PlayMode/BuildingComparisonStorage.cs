using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core.Storage;
using Core.Storage.Adapters;

namespace ChainRush.Tests.PlayMode
{
    // minimum seam: the same fresh authored profile for both variants; no reads or writes of user saves.
    internal sealed class BuildingComparisonStorage : IStorageRuntime, IDisposable
    {
        static readonly FieldInfo RuntimeField = typeof(StorageService).GetField("_runtime",
            BindingFlags.NonPublic | BindingFlags.Static);
        readonly IStorageRuntime _previous = (IStorageRuntime)RuntimeField.GetValue(null);
        readonly DefaultSettingsValueCodec _codec = new();
        readonly Dictionary<(SettingsContext, SettingsKey, Type), FrozenValue> _current = new();
        internal int BaselineKeys => 0;
        internal void BeginVariant() => _current.Clear();
        public void Dispose() => StorageService.InstallRuntime(_previous);

        public ValueTask<SettingsReadResult<T>> GetAsync<T>(SettingsKey key, SettingsContext context,
            SettingsReadOptions options, CancellationToken cancellationToken)
        {
            _current.TryGetValue((context, key, typeof(T)), out var record);
            return new ValueTask<SettingsReadResult<T>>(new SettingsReadResult<T>(record.Found,
                record.Found ? _codec.Decode<T>(record.Payload) : default, record.Source, record.Schema));
        }
        public ValueTask SetAsync<T>(SettingsKey key, T value, SettingsContext context,
            SettingsWriteOptions options, CancellationToken cancellationToken)
        {
            _current[(context, key, typeof(T))] = new FrozenValue(true, _codec.Encode(value),
                SettingsSourceType.LocalFile, SettingsSchema.CurrentVersion);
            return default;
        }
        public ValueTask<bool> RemoveAsync(SettingsKey key, SettingsContext context, CancellationToken cancellationToken)
        {
            bool found = false;
            var keys = new HashSet<(SettingsContext, SettingsKey, Type)>(_current.Keys);
            foreach (var identity in keys)
                if (identity.Item1.Equals(context) && identity.Item2.Equals(key))
                { found = true; _current[identity] = default; }
            return new ValueTask<bool>(found);
        }
        public ValueTask ResetContextAsync(SettingsContext context, CancellationToken cancellationToken)
        {
            var keys = new HashSet<(SettingsContext, SettingsKey, Type)>(_current.Keys);
            foreach (var identity in keys)
                if (identity.Item1.Equals(context)) _current[identity] = default;
            return default;
        }
        public ValueTask SyncAsync(SettingsContext context, CancellationToken cancellationToken) => default;
        readonly struct FrozenValue
        {
            internal readonly bool Found;
            internal readonly string Payload;
            internal readonly SettingsSourceType Source;
            internal readonly int Schema;
            internal FrozenValue(bool found, string payload, SettingsSourceType source, int schema)
            { Found = found; Payload = payload; Source = source; Schema = schema; }
        }
    }
}
