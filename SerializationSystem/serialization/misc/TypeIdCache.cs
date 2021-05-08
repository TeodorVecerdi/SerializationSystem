using System;
using System.Collections.Generic;

namespace SerializationSystem.Internal {
    internal class TypeIdCache {
        private readonly Dictionary<int, TypeId> cache = new Dictionary<int, TypeId>();

        internal TypeId GetCached(Type type) {
            if (!Contains(type, out var hash, out var typeId)) cache[hash] = typeId;
            return cache[hash];
        }

        internal TypeId GetCached(string name) {
            if (!Contains(name, out var hash, out var typeId)) cache[hash] = typeId;
            return cache[hash];
        }

        private bool Contains(Type type, out int hashCode, out TypeId typeId) {
            return Contains(typeId = new TypeId(type), out hashCode);
        }

        private bool Contains(string name, out int hashCode, out TypeId typeId) {
            return Contains(typeId = new TypeId(name), out hashCode);
        }

        private bool Contains(TypeId typeId, out int hashCode) {
            hashCode = TypeId.Comparer.GetHashCode(typeId);
            return cache.ContainsKey(hashCode);
        }
    }
}