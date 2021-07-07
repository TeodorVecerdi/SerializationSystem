using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SerializationSystem.Internal;
using SerializationSystem.Logging;

namespace SerializationSystem {
    public static class Serializer {
        private static readonly ConcurrentDictionary<SerializationModelKey, SerializationModel> serializationModel = new(SerializationModelKey.EqualityComparer);
        private static readonly SerializationExceptionHandler defaultExceptionHandler = new DefaultExceptionHandler();
        private static SerializationExceptionHandler exceptionHandler = defaultExceptionHandler;

        private static bool isSerializationResultReplaced;
        private static object deserializationReplacement;
        private static byte[] serializationReplacement;

        public static byte[] Serialize(object obj, SerializeMode serializeMode = SerializeMode.Default, ModelType modelType = ModelType.Default) => Serialize(obj, obj.GetType(), serializeMode, modelType);

        public static byte[] Serialize<T>(T obj, SerializeMode serializeMode = SerializeMode.Default, ModelType modelType = ModelType.Default) {
            var type = typeof(T);
            if (!SerializeUtils.CanSerializeType(type, out _)) type = obj.GetType();
            return Serialize(obj, type, serializeMode, modelType);
        }

        public static byte[] Serialize(object obj, Type type, SerializeMode serializeMode = SerializeMode.Default, ModelType modelType = ModelType.Default) {
            isSerializationResultReplaced = false;
            var result = Serialize(obj, type, new Packet(), serializeMode, modelType);
            return isSerializationResultReplaced ? serializationReplacement : result;
        }

        public static object Deserialize(byte[] data) {
            isSerializationResultReplaced = false;
            var result = Deserialize(new Packet(data));
            return isSerializationResultReplaced ? deserializationReplacement : result;
        }

        public static SerializationExceptionHandler ExceptionHandler {
            set => exceptionHandler = value ?? defaultExceptionHandler;
        }

        internal static byte[] Serialize(object obj, Type type, Packet packet, SerializeMode serializeMode, ModelType modelType) {
            if (isSerializationResultReplaced) return new byte[0];
            try {
                if (SerializeUtils.IsTriviallySerializable(type)) {
                    if (type.IsInterface) {
                        var newType = obj.GetType();
                        Log.Error($"[TRIVIAL] Replacing interface type {type} with {newType}");
                        type = newType;
                    }
                    
                    if (LogOptions.LOG_SERIALIZATION) Log.Info($"[TRIVIAL] Serializing type {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
                    var bytes = SerializeTrivialImpl(obj, type, packet, serializeMode, modelType).GetBytes();
                    if (LogOptions.LOG_SERIALIZATION) Log.Info($"Serialized type {SerializeUtils.FriendlyName(type)} [{bytes.Length} bytes]", null, "SERIALIZE");
                    return bytes;
                }

                if (!SerializeUtils.CanSerializeType(type, out var reason)) {
                    var exception = new System.Runtime.Serialization.SerializationException(
                        $"Could not serialize object of type {SerializeUtils.FriendlyName(type)} [Reason: {reason}]");
                    return exceptionHandler.HandleSerializationException(exception);
                }

                if (modelType == ModelType.Normal && !HasSerializationModel(type, serializeMode)) BuildSerializationModel(type, serializeMode);
                BeforeSerializeCallback(obj, type);
                if (LogOptions.LOG_SERIALIZATION) Log.Info($"Serializing type {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
                var bytes2 = SerializeImpl(obj, type, packet, serializeMode, modelType).GetBytes();
                if (LogOptions.LOG_SERIALIZATION) Log.Info($"Serialized type {SerializeUtils.FriendlyName(type)} [{bytes2.Length} bytes]", null, "SERIALIZE");
                return bytes2;
            } catch (Exception exception) {
                return exceptionHandler.HandleSerializationException(exception);
            }
        }

        private static Packet SerializeImpl(object obj, Type type, Packet packet, SerializeMode serializeMode, ModelType modelType) {
            // TODO: Implement SerializeType
            if (type.IsInterface) {
                var objType = obj.GetType();
                if (LogOptions.LOG_SERIALIZATION) Log.Warn($"Interface type found {type.FullName}. Serializing using object type {objType.FullName}", null, "SERIALIZE");
                return SerializeImpl(obj, objType, packet, serializeMode, modelType);
            }

            if (modelType == ModelType.Normal) {
                return SerializeImplNormal(obj, type, packet, serializeMode);
            }

            return null;
        }

        private static Packet SerializeImplNormal(object obj, Type type, Packet packet, SerializeMode serializeMode) {
            // TODO: Implement SerializeType
            var typeId = type.ID();
            packet.WriteTypeId(typeId, ModelType.Default);
            packet.Write(serializeMode, SerializeMode.Default, ModelType.Default);
            if (LogOptions.LOG_SERIALIZATION) Log.Info($"Serialized SerializeMode[{serializeMode}]", null, "SERIALIZE");

            var model = serializationModel[new SerializationModelKey(typeId, serializeMode)];
            foreach (var field in model.Fields) {
                var fieldType = field.FieldType;
                var value = field.GetValue(obj);
                if (fieldType.IsInterface) {
                    var newType = value.GetType();
                    if (LogOptions.LOG_SERIALIZATION) Log.Warn($"Interface type found {fieldType.FullName}. Serializing using object type {newType.FullName}", null, "SERIALIZE");
                    fieldType = newType;
                    packet.WriteTypeId(fieldType.ID(), ModelType.Default);
                }
                packet.Write(fieldType, value, serializeMode, ModelType.Default);
            }

            return packet;
        }

        private static Packet SerializeTrivialImpl(object obj, Type type, Packet packet, SerializeMode serializeMode, ModelType modelType) {
            // TODO: Implement SerializeType
            var typeId = type.ID();
            packet.WriteTypeId(typeId, modelType);
            packet.Write(serializeMode, SerializeMode.Default, modelType);
            packet.Write(type, obj, serializeMode, modelType);
            if (LogOptions.LOG_SERIALIZATION) Log.Info($"Serialized SerializeMode[{serializeMode}]", null, "SERIALIZE");
            return packet;
        }

        internal static object Deserialize(Packet packet) {
            if (isSerializationResultReplaced) return null;
            try {
                var typeId = packet.ReadTypeId();
                var serializeMode = packet.Read<SerializeMode>(SerializeMode.Default);
                if (LogOptions.LOG_SERIALIZATION) Log.Info($"Read SerializeMode[{serializeMode}]", null, "SERIALIZE");
                var type = typeId.Type;
                if (SerializeUtils.IsTriviallySerializable(type)) {
                    if (type.IsInterface) {
                        Log.Error($"Found interface type when wasn't expecting: {type}");
                    }
                    
                    if (LogOptions.LOG_SERIALIZATION) Log.Info($"[TRIVIAL] Deserializing type {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
                    var objTrivial = DeserializeTrivialImpl(packet, type, serializeMode);
                    AfterDeserializeCallback(objTrivial, type);
                    return objTrivial;
                }

                if (!SerializeUtils.CanSerializeType(typeId.Type, out var reason)) {
                    var e = new System.Runtime.Serialization.SerializationException(
                        $"Could not deserialize object of type {SerializeUtils.FriendlyName(typeId.Type)} [Reason: {reason}]");
                    return exceptionHandler.HandleDeserializationException(e);
                }

                if (!HasSerializationModel(type, serializeMode)) BuildSerializationModel(type, serializeMode);
                if (LogOptions.LOG_SERIALIZATION) Log.Info($"Deserializing type {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
                var obj = DeserializeImpl(packet, type, serializeMode);
                AfterDeserializeCallback(obj, type);
                return obj;
            } catch (Exception exception) {
                return exceptionHandler.HandleDeserializationException(exception);
            }
        }

        private static object DeserializeImpl(Packet packet, Type type, SerializeMode serializeMode) {
            var model = serializationModel[new SerializationModelKey(type.ID(), serializeMode)];
            var instance = model.Constructor.Create();
            foreach (var field in model.Fields) {
                var fieldType = field.FieldType;
                if (fieldType.IsInterface) {
                    var newType = packet.ReadTypeId().Type;
                    if (LogOptions.LOG_SERIALIZATION) Log.Warn($"Interface type found {fieldType.FullName}. Deserializing using object type {newType.FullName}", null, "SERIALIZE");
                    fieldType = newType;
                }
                var value = packet.Read(fieldType, serializeMode);
                field.SetValue(instance, value);
            }

            return instance;
        }

        private static object DeserializeTrivialImpl(Packet packet, Type type, SerializeMode serializeMode) {
            return packet.Read(type, serializeMode);
        }

        internal static void ReplaceSerializationResult(byte[] replacement) {
            isSerializationResultReplaced = true;
            serializationReplacement = replacement;
            if (LogOptions.LOG_SERIALIZATION_REPLACEMENTS) Log.Message($"Replaced serialization result with {replacement.Length} bytes", null, messageTitle: "SERIALIZE");
        }

        internal static void ReplaceDeserializationResult(object replacement) {
            isSerializationResultReplaced = true;
            deserializationReplacement = replacement;
            if (LogOptions.LOG_SERIALIZATION_REPLACEMENTS) Log.Message($"Replaced deserialization result with {replacement}", null, messageTitle: "SERIALIZE");
        }

        private static void BeforeSerializeCallback(object obj, Type type) {
            if (typeof(ISerializationCallback).IsAssignableFrom(type)) {
                var method = type.GetMethod("OnBeforeSerialize");
                System.Diagnostics.Debug.Assert(method != null);
                method.Invoke(obj, new object[0]);
                if (LogOptions.LOG_SERIALIZATION) Log.Info($"OnBeforeSerialize callback called for {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
                return;
            }

            Type unityInterfaceType;
            // Check if UnityEngine serialization callback exists is present
            try {
                unityInterfaceType = TypeIdUtils.FindTypeByName("UnityEngine.ISerializationCallbackReceiver", true);
            } catch {
                // do nothing if not present
                return;
            }

            if (!unityInterfaceType.IsAssignableFrom(type)) return;
            var unityMethod = type.GetMethod("OnBeforeSerialize");
            System.Diagnostics.Debug.Assert(unityMethod != null);
            unityMethod.Invoke(obj, new object[0]);
            if (LogOptions.LOG_SERIALIZATION) Log.Info($"OnBeforeSerialize callback called for {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
        }

        private static void AfterDeserializeCallback(object obj, Type type) {
            if (typeof(ISerializationCallback).IsAssignableFrom(type)) {
                var method = type.GetMethod("OnAfterDeserialize");
                System.Diagnostics.Debug.Assert(method != null);
                method.Invoke(obj, new object[0]);
                if (LogOptions.LOG_SERIALIZATION) Log.Info($"OnAfterDeserialize callback called for {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
                return;
            }

            // Check if UnityEngine serialization callback exists is present
            Type unityInterfaceType;
            try {
                unityInterfaceType = TypeIdUtils.FindTypeByName("UnityEngine.ISerializationCallbackReceiver", true);
            } catch {
                // do nothing if not present
                return;
            }

            if (!unityInterfaceType.IsAssignableFrom(type)) return;
            var unityMethod = type.GetMethod("OnAfterDeserialize");
            System.Diagnostics.Debug.Assert(unityMethod != null);
            unityMethod.Invoke(obj, new object[0]);
            if (LogOptions.LOG_SERIALIZATION) Log.Info($"OnAfterDeserialize callback called for {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE");
        }

        internal static void BuildSerializationModel(Type type, SerializeMode serializeMode) {
            var model = new SerializationModel(type, serializeMode);
            serializationModel[new SerializationModelKey(type.ID(), serializeMode)] = model;
        }

        internal static bool HasSerializationModel(Type type, SerializeMode mode) => serializationModel.ContainsKey(new SerializationModelKey(type.ID(), mode));

        private class SerializationModelKey {
            private readonly TypeId typeId;
            private readonly SerializeMode mode;

            internal SerializationModelKey(TypeId typeId, SerializeMode mode) {
                this.typeId = typeId;
                this.mode = mode;
            }

            internal static Comparer EqualityComparer { get; } = new Comparer();

            internal sealed class Comparer : IEqualityComparer<SerializationModelKey> {
                public bool Equals(SerializationModelKey x, SerializationModelKey y) {
                    if (ReferenceEquals(x, y)) return true;
                    if (ReferenceEquals(x, null)) return false;
                    if (ReferenceEquals(y, null)) return false;
                    if (x.GetType() != y.GetType()) return false;
                    return Equals(x.typeId, y.typeId) && x.mode == y.mode;
                }

                public int GetHashCode(SerializationModelKey obj) {
                    unchecked {
                        return ((obj.typeId != null ? obj.typeId.GetHashCode() : 0) * 397) ^ (int) obj.mode;
                    }
                }
            }
        }
    }
}