using System.Collections.Generic;

namespace Game.Core.Net
{
    /// <summary>
    /// Two-way map between a transport's native connection handles and the <c>ulong</c> client ids NGO expects.
    /// Ids are handed out from a counter starting at <see cref="FirstId"/> and never reused within one session.
    /// </summary>
    public sealed class TransportIdMap<THandle>
    {
        /// <summary>First id handed out. 0 is reserved for the server itself.</summary>
        public const ulong FirstId = 1;

        private readonly Dictionary<THandle, ulong> _idByHandle = new Dictionary<THandle, ulong>();
        private readonly Dictionary<ulong, THandle> _handleById = new Dictionary<ulong, THandle>();
        private ulong _nextId = FirstId;

        /// <summary>Number of mapped connections.</summary>
        public int Count => _idByHandle.Count;

        /// <summary>Returns the id of <paramref name="handle"/>, mapping it to a new id if it has none yet.</summary>
        public ulong GetOrAdd(THandle handle)
        {
            if (_idByHandle.TryGetValue(handle, out ulong id)) return id;
            id = _nextId++;
            _idByHandle.Add(handle, id);
            _handleById.Add(id, handle);
            return id;
        }

        /// <summary>Looks up the id of a mapped handle.</summary>
        public bool TryGetId(THandle handle, out ulong id) => _idByHandle.TryGetValue(handle, out id);

        /// <summary>Looks up the handle of a mapped id.</summary>
        public bool TryGetHandle(ulong id, out THandle handle) => _handleById.TryGetValue(id, out handle);

        /// <summary>Removes a handle and its id; returns false if it was not mapped.</summary>
        public bool Remove(THandle handle)
        {
            if (!_idByHandle.TryGetValue(handle, out ulong id)) return false;
            _idByHandle.Remove(handle);
            _handleById.Remove(id);
            return true;
        }

        /// <summary>Copies every mapped handle into <paramref name="into"/> (cleared first).</summary>
        public void CopyHandles(List<THandle> into)
        {
            into.Clear();
            into.AddRange(_idByHandle.Keys);
        }

        /// <summary>Forgets every mapping and restarts ids at <see cref="FirstId"/>.</summary>
        public void Clear()
        {
            _idByHandle.Clear();
            _handleById.Clear();
            _nextId = FirstId;
        }
    }
}
