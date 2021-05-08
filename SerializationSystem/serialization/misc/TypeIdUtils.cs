using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    internal static class TypeIdUtils {
        private static readonly TypeIdCache cache = new TypeIdCache();
        internal static TypeId ID(this Type type) => Get(type);
        internal static TypeId AsType(this string name) => Get(name);

        internal static TypeId Get(Type type) => cache.GetCached(type);
        internal static TypeId Get(string name) => cache.GetCached(name);

        private static ConcurrentDictionary<string, Type> typeCache;
        internal static Type FindTypeByName(string name) => FindTypeByName(name, false); 
        internal static Type FindTypeByName(string name, bool suppressErrors) {
            if(typeCache == null) typeCache = new ConcurrentDictionary<string, Type>();
            if (typeCache == null) {
                var e = new Exception("Could not create type cache.");
                if(!suppressErrors) Log.Except(e, new TypeId((string) null), includeStackTrace: true);
                throw e;
            }

            if (typeCache.ContainsKey(name)) return typeCache[name];
            
            var type = Type.GetType(name);
            if (type != null) {
                typeCache[name] = type;
                return type;
            }

            try {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                //To speed things up, we check first in the already loaded assemblies.
                foreach (var assembly in assemblies) {
                    type = assembly.GetType(name);
                    if (type == null) continue;
                    
                    typeCache[name] = type;
                    return type;
                }

                var loadedAssemblies = assemblies.ToList();
                foreach (var loadedAssembly in assemblies) {
                    foreach (var referencedAssemblyName in loadedAssembly.GetReferencedAssemblies()) {
                        if (loadedAssemblies.Any(x => x.GetName() == referencedAssemblyName)) continue;
                        
                        try {
                            var referencedAssembly = Assembly.Load(referencedAssemblyName);
                            type = referencedAssembly.GetType(name);
                            if (type != null) {
                                typeCache[name] = type;
                                return type;
                            }
                            
                            loadedAssemblies.Add(referencedAssembly);
                        } catch {
                            //We will ignore this, because the Type might still be in one of the other Assemblies.
                        }
                    }
                }
            } catch (Exception e) {
                if(!suppressErrors) Log.Except(e, new TypeId((string)null), includeStackTrace: true);
                throw;
            }

            if (type != null) {
                typeCache[name] = type;
                return type;
            }
            
            var exception = new Exception($"Could not find type {name} in any loaded or referenced assembly.");
            if(!suppressErrors) Log.Except(exception, new TypeId((string) null), includeStackTrace: true);
            throw exception;
        }
    }
}