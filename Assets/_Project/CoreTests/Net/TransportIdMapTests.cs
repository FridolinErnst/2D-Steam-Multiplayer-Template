using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Net.Tests
{
    public sealed class TransportIdMapTests
    {
        [Test]
        public void GetOrAdd_New_Handles_Get_Increasing_Ids_From_One()
        {
            var map = new TransportIdMap<uint>();
            Assert.AreEqual(1UL, map.GetOrAdd(100));
            Assert.AreEqual(2UL, map.GetOrAdd(200));
        }

        [Test]
        public void GetOrAdd_Same_Handle_Returns_Same_Id()
        {
            var map = new TransportIdMap<uint>();
            ulong id = map.GetOrAdd(7);
            Assert.AreEqual(id, map.GetOrAdd(7));
            Assert.AreEqual(1, map.Count);
        }

        [Test]
        public void Lookups_Work_Both_Ways()
        {
            var map = new TransportIdMap<uint>();
            ulong id = map.GetOrAdd(42);
            Assert.IsTrue(map.TryGetHandle(id, out uint handle));
            Assert.AreEqual(42u, handle);
            Assert.IsTrue(map.TryGetId(42, out ulong back));
            Assert.AreEqual(id, back);
        }

        [Test]
        public void Remove_Forgets_Both_Directions_And_Does_Not_Reuse_Id()
        {
            var map = new TransportIdMap<uint>();
            ulong first = map.GetOrAdd(1);
            Assert.IsTrue(map.Remove(1));
            Assert.IsFalse(map.TryGetHandle(first, out _));
            Assert.IsFalse(map.Remove(1));
            Assert.AreNotEqual(first, map.GetOrAdd(1));
        }

        [Test]
        public void Clear_Restarts_Ids()
        {
            var map = new TransportIdMap<uint>();
            map.GetOrAdd(1);
            map.GetOrAdd(2);
            map.Clear();
            Assert.AreEqual(0, map.Count);
            Assert.AreEqual(TransportIdMap<uint>.FirstId, map.GetOrAdd(3));
        }

        [Test]
        public void CopyHandles_Lists_All_Mapped_Handles()
        {
            var map = new TransportIdMap<uint>();
            map.GetOrAdd(5);
            map.GetOrAdd(6);
            var handles = new List<uint> { 99 };
            map.CopyHandles(handles);
            CollectionAssert.AreEquivalent(new[] { 5u, 6u }, handles);
        }
    }
}
