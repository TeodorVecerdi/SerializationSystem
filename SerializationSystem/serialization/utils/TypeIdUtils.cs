using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Serialization.Internal;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    internal static class TypeIdUtils {
        private static readonly TypeIdCache cache = new TypeIdCache();
        internal static TypeId ID(this Type type) => Get(type);
        internal static TypeId AsType(this string name) => Get(name);

        internal static TypeId Get(Type type) => cache.GetCached(type);
        internal static TypeId Get(string name) => cache.GetCached(name);

        private const string kTypeNotFoundFormat = "Could not find type {0} in any loaded or referenced assembly.";
        private const string kTypeCacheNotCreated = "Could not create type cache.";
        private const string kNonExistentTypeSetNotCreated = "Could not create non existent type set.";

        private static ConcurrentDictionary<string, Type> typeCache;
        private static ConcurrentSet<string> nonExistentTypes;
        private static ConcurrentSet<Assembly> loadedAssemblies;
        internal static Type FindTypeByName(string name) => FindTypeByName(name, false); 
        internal static Type FindTypeByName(string name, bool suppressErrors) {
            typeCache ??= new ConcurrentDictionary<string, Type>();
            nonExistentTypes ??= new ConcurrentSet<string>();
            
            if (typeCache == null) {
                Throw(kTypeCacheNotCreated, suppressErrors);
            }
            
            if (nonExistentTypes == null) {
                Throw(kNonExistentTypeSetNotCreated, suppressErrors);
            }

            if (typeCache.ContainsKey(name)) return typeCache[name];
            if (nonExistentTypes.Contains(name)) {
                Throw(string.Format(kTypeNotFoundFormat, name), suppressErrors);
            }
            
            var type = Type.GetType(name);
            if (type != null) {
                typeCache[name] = type;
                return type;
            }

            try {
                if (loadedAssemblies == null) {
                    // Preload all assemblies
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    loadedAssemblies ??= new ConcurrentSet<Assembly>(assemblies.Union(assemblies.SelectMany(assembly => assembly.GetReferencedAssemblies()).Distinct().Select(Assembly.Load)));
                }

                //To speed things up, we check first in the already loaded assemblies.
                foreach (var assembly in loadedAssemblies) {
                    type = assembly.GetType(name);
                    if (type == null) continue;
                    
                    typeCache[name] = type;
                    return type;
                }
            } catch (Exception e) {
                if(!suppressErrors) Log.Except(e, new TypeId((string)null), includeStackTrace: true);
                throw;
            }

            nonExistentTypes.Add(name);
            Throw(string.Format(kTypeNotFoundFormat, name), suppressErrors);
            return null;
        }

        private static void Throw(string message, bool suppressErrors) {
            var exception = new Exception(message);
            if (!suppressErrors) Log.Except(exception, new TypeId((string) null), includeStackTrace: true);
            throw exception;
        }
    }
}