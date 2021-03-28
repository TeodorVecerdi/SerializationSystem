using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    internal static class SerializeUtils {
        private static readonly ConcurrentDictionary<Type, InstantiateCtor> ctorCache = new ConcurrentDictionary<Type, InstantiateCtor>();

        internal static bool HasSerializedAttr(FieldInfo field) => field.GetCustomAttribute<SerializedAttribute>() != null;
        internal static bool HasNonSerializedAttr(FieldInfo field) => field.GetCustomAttribute<NonSerializedAttribute>() != null;

        internal static bool IsTriviallySerializable(Type type) =>
            BuiltinTypes.Contains(type) || type.IsEnum || type == typeof(Type) || CanSerializeList(type) || CanSerializeDictionary(type);

        internal static bool CanSerializeType(Type type, out string reason) {
            if (IsTriviallySerializable(type)) {
                reason = "";
                return true;
            }

            if (type.IsAbstract) {
                reason = "Type is abstract";
                return false;
            }

            try {
                var inst = Instantiate(type);
                Utils.KeepUnusedVariable(ref inst);
            } catch (Exception e) {
                while (e.InnerException != null) e = e.InnerException;
                reason = $"Exception occured while instantiating: {e.Message}";
                return false;
            }

            reason = "";
            return true;
        }

        internal static InstantiateCtor Ctor(this Type type) {
            if (!ctorCache.ContainsKey(type)) {
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (constructors.Length == 0) return null;
                //Find suitable constructor
                ConstructorInfo defaultCtor = null;
                foreach (var ctor in constructors) {
                    // Preferred ctor
                    var attr = ctor.GetCustomAttribute<PreferredCtorAttribute>();
                    if (attr != null) {
                        ctorCache[type] = new InstantiateCtor(ctor, attr);
                        break;
                    }

                    // Default ctor
                    if (ctor.GetParameters().Length == 0) defaultCtor = ctor;
                }

                if (!ctorCache.ContainsKey(type)) {
                    var finalCtor = defaultCtor ?? constructors[0];
                    ctorCache[type] = new InstantiateCtor(finalCtor, null);
                }
            }

            return ctorCache[type];
        }

        internal static object Instantiate(Type type, SerializationConstructor ctor) {
            return BuiltinTypes.Contains(type) ? type.Default() : ctor.Create();
        }

        internal static object Instantiate(Type type) {
            if (BuiltinTypes.Contains(type))
                return type.Default();
            var ctor = type.Ctor();
            var parameters = CtorParameters(ctor);
            return new SerializationConstructor(ctor.Constructor, parameters).Create();
        }

        internal static object Instance(this Type type) => Instantiate(type);
        internal static object Instance(this Type type, SerializationConstructor ctor) => Instantiate(type, ctor);

        internal static T Instantiate<T>() {
            return (T) Instantiate(typeof(T));
        }

        internal static T Instantiate<T>(SerializationConstructor ctor) {
            return (T) Instantiate(typeof(T), ctor);
        }

        internal static object[] CtorParameters(InstantiateCtor ctor) {
            if (ctor == null) return new object[0];
            var parameters = ctor.Constructor.GetParameters();
            if (parameters.Length == 0) return new object[0];

            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) {
                if (ctor.Attribute != null && i < ctor.Attribute.Arguments.Length && CanCast(ctor.Attribute.Arguments[i].GetType(), parameters[i].ParameterType)) {
                    var val = ctor.Attribute.Arguments[i];
                    if (ctor.Attribute.Arguments[i].GetType() != parameters[i].ParameterType)
                        val = Convert.ChangeType(val, parameters[i].ParameterType);
                    args[i] = val;
                    continue;
                }

                if (parameters[i].HasDefaultValue) args[i] = parameters[i].DefaultValue;
                else args[i] = parameters[i].ParameterType.Default();
            }

            return args;
        }

        internal static object Default(this Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        internal static bool CanCast(Type from, Type to) {
            if (from == null || to == null) return false;
            if (to.IsAssignableFrom(from)) return true;
            try {
                Convert.ChangeType(Activator.CreateInstance(from), to);
                return true;
            } catch {
                return false;
            }
        }

        internal static string FriendlyName(Type type) {
            try {
                if (BuiltinTypes.Contains(type)) return BuiltinFriendlyName(type);
                if (type.IsGenericType) return GenericFriendlyName(type);
                if (type.IsArray) return ArrayFriendlyName(type);
                return type.Name;
            } catch {
                Log.Error($"Could not get friendly name for type {type.FullName}");
                return type.FullName;
            }
        }

        internal static string GenericFriendlyName(Type type) {
            var baseTypeName = type.Name.Split('`')[0];
            var argumentTypes = type.GetGenericArguments().Select(FriendlyName);
            return $"{baseTypeName}<{string.Join(",", argumentTypes)}>";
        }

        internal static string ArrayFriendlyName(Type type) {
            var baseType = type.GetElementType();
            var dimension = new string(',', type.GetArrayRank() - 1);
            return $"{FriendlyName(baseType)}[{dimension}]";
        }

        internal static string BuiltinFriendlyName(Type type) {
            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(string)) return "string";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            return type.Name;
        }

        internal static bool IsArray(Type type) => type.IsArray && type.GetArrayRank() == 1;
        internal static bool IsList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        internal static bool IsListInterface(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>);
        internal static bool IsDictionary(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
        internal static bool IsDictionaryInterface(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>);

        internal static bool CanSerializeList(Type type) {
            var isInterfaceAssignable = type.IsGenericType && type.GenericTypeArguments.Length == 1 
                                        && typeof(IList<>).MakeGenericType(type.GenericTypeArguments[0]).IsAssignableFrom(type)
                                        && typeof(IList).IsAssignableFrom(type);
            return IsArray(type) || IsList(type) || IsListInterface(type) || isInterfaceAssignable;
        }

        internal static bool CanSerializeDictionary(Type type) {
            var isInterfaceAssignable = type.IsGenericType && type.GenericTypeArguments.Length == 2 
                                        && typeof(IDictionary<,>).MakeGenericType(type.GenericTypeArguments[0], type.GenericTypeArguments[1]).IsAssignableFrom(type)
                                        && typeof(IDictionary).IsAssignableFrom(type);
            return IsDictionary(type) || IsDictionaryInterface(type) || isInterfaceAssignable;
        }

        internal static Type GetListElementType(Type type) => type.IsGenericType ? type.GenericTypeArguments[0] : type.GetElementType();
        internal static Type GetDictionaryKeyType(Type type) => type.GenericTypeArguments[0];
        internal static Type GetDictionaryValueType(Type type) => type.GenericTypeArguments[1];

        internal static readonly HashSet<Type> BuiltinTypes = new HashSet<Type> {
            typeof(bool), typeof(byte), typeof(sbyte), typeof(char),
            typeof(decimal), typeof(double), typeof(float), typeof(int),
            typeof(uint), typeof(long), typeof(ulong), typeof(short),
            typeof(ushort), typeof(string)
        };

        internal class InstantiateCtor {
            public readonly ConstructorInfo Constructor;
            public readonly PreferredCtorAttribute Attribute;

            public InstantiateCtor(ConstructorInfo constructor, PreferredCtorAttribute attribute) {
                Constructor = constructor;
                Attribute = attribute;
            }
        }
    }
}