using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    /**
	 * The Packet class provides a simple wrapper around an array of bytes (in the form of a MemoryStream), 
	 * that allows us to write/read values to/from the Packet easily. 
	 * Additionally it abstract/decouples how (de)serialization is done from the rest of the application.
	 * 
	 * All the application knows about are packets, no matter how you implemented the (de)serialization itself.
	 */
    internal class Packet {
        private readonly BinaryWriter writer; //only used in write mode, to write bytes into a byte array
        private readonly BinaryReader reader; //only used in read mode, to read bytes from a byte array

        /**
		 * Create a Packet for writing.
		 */
        internal Packet() {
            //BinaryWriter wraps a Stream, in this case a MemoryStream, which in turn wraps an array of bytes
            writer = new BinaryWriter(new MemoryStream());
        }

        /**
		 * Create a Packet from an existing byte array so we can read from it
		 */
        internal Packet(byte[] pSource) {
            //BinaryReader wraps a Stream, in this case a MemoryStream, which in turn wraps an array of bytes
            reader = new BinaryReader(new MemoryStream(pSource));
        }

        internal void Write(Type type, object obj, SerializeMode serializeMode) {
            var size = GetSize(type, obj);
            if (LogOptions.LOG_SERIALIZATION_WRITE)
                Log.Info($"Writing type {SerializeUtils.FriendlyName(type)} [{(size == -1 || size == 0 ? "??" : size.ToString())} bytes]", null, "SERIALIZE-WRITE");
            
            if (SerializeUtils.BuiltinTypes.Contains(type)) WriteBuiltin(type, obj);
            else if (type.IsEnum) WriteEnum(type, obj);
            else if (type == typeof(Type)) WriteTypeId(((Type) obj).ID());
            else WriteNullable(type, obj, serializeMode);
        }

        private void WriteBuiltin(Type type, object obj) {
            if (type == typeof(bool)) writer.Write((bool) obj);
            else if (type == typeof(byte)) writer.Write((byte) obj);
            else if (type == typeof(sbyte)) writer.Write((sbyte) obj);
            else if (type == typeof(char)) writer.Write((char) obj);
            else if (type == typeof(decimal)) writer.Write((decimal) obj);
            else if (type == typeof(double)) writer.Write((double) obj);
            else if (type == typeof(float)) writer.Write((float) obj);
            else if (type == typeof(int)) writer.Write((int) obj);
            else if (type == typeof(uint)) writer.Write((uint) obj);
            else if (type == typeof(long)) writer.Write((long) obj);
            else if (type == typeof(ulong)) writer.Write((ulong) obj);
            else if (type == typeof(short)) writer.Write((short) obj);
            else if (type == typeof(ushort)) writer.Write((ushort) obj);
            else if (type == typeof(string)) WriteNullable(type, obj, SerializeMode.Default);
        }

        private void WriteNullable(Type type, object obj, SerializeMode serializeMode) {
            var canBeNull = !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
            var isNull = canBeNull && obj == null;

            if (isNull) {
                writer.Write(false);
                return;
            }

            if (canBeNull) writer.Write(true);
            if (type == typeof(string)) writer.Write((string) obj);
            else if (SerializeUtils.CanSerializeList(type)) WriteList(type, obj, serializeMode);
            else if (SerializeUtils.CanSerializeDictionary(type)) WriteDictionary(type, obj, serializeMode);
            else Serializer.Serialize(obj, type, this, serializeMode);
        }

        internal void Write<T>(T obj, SerializeMode serializeMode) {
            var type = typeof(object);
            if (obj != null) type = obj.GetType();
            Write(type, obj, serializeMode);
        }

        internal object Read(Type type, SerializeMode serializeMode) {
            if (LogOptions.LOG_SERIALIZATION_READ) Log.Info($"Reading type {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE-READ");
            
            if (SerializeUtils.BuiltinTypes.Contains(type)) return ReadBuiltin(type);
            if (type.IsEnum) return ReadEnum(type);
            if (type == typeof(Type)) return ReadTypeId().Type;
            return ReadNullable(type, serializeMode);
        }

        private object ReadBuiltin(Type type) {
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(char)) return reader.ReadChar();
            if (type == typeof(decimal)) return reader.ReadDecimal();
            if (type == typeof(double)) return reader.ReadDouble(); 
            if (type == typeof(float)) return reader.ReadSingle(); 
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32(); 
            if (type == typeof(long)) return reader.ReadInt64(); 
            if (type == typeof(ulong)) return reader.ReadUInt64(); 
            if (type == typeof(short)) return reader.ReadInt16(); 
            if (type == typeof(ushort)) return reader.ReadUInt16();
            // string
            return ReadNullable(type, SerializeMode.Default); 
        }

        private object ReadNullable(Type type, SerializeMode serializeMode) {
            var canBeNull = !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

            if (canBeNull) {
                var hasValue = Read<bool>(SerializeMode.Default);
                if (!hasValue) return null;
            }

            if (type == typeof(string)) return reader.ReadString();
            if (SerializeUtils.CanSerializeList(type)) return ReadList(type, serializeMode);
            if (SerializeUtils.CanSerializeDictionary(type)) return ReadDictionary(type, serializeMode);
            return Serializer.Deserialize(this);
        }

        internal T Read<T>(SerializeMode serializeMode) {
            return (T) Read(typeof(T), serializeMode);
        }

        private void WriteList(Type type, object obj, SerializeMode serializeMode) {
            var list = (IList) obj;
            var elemType = SerializeUtils.GetListElementType(type);
            Write(list.Count, SerializeMode.Default);
            foreach (var elem in list) {
                Write(elemType, elem, serializeMode);
            }
        }

        private object ReadList(Type type, SerializeMode serializeMode) {
            var count = Read<int>(serializeMode);
            var elemType = SerializeUtils.GetListElementType(type);
            IList list;
            var isList = false;
            if (SerializeUtils.IsList(type)) {
                list = (IList) Activator.CreateInstance(type, count);
                isList = true;
            } else list = Array.CreateInstance(elemType, count);

            for (var i = 0; i < count; i++) {
                if (isList) list.Add(Read(elemType, serializeMode));
                else list[i] = Read(elemType, serializeMode);
            }

            return list;
        }

        private void WriteDictionary(Type type, object obj, SerializeMode serializeMode) {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            var dict = (IDictionary) obj;
            var keyCollection = dict.Keys;
            var keyList = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(keyType));
            Debug.Assert(keyList != null, nameof(keyList) + " != null");
            foreach (var key in keyCollection) {
                keyList.Add(key);
            }

            var valueCollection = dict.Values;
            var valueList = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(valueType));
            Debug.Assert(valueList != null, nameof(valueList) + " != null");
            foreach (var value in valueCollection) {
                valueList.Add(value);
            }

            WriteList(typeof(List<>).MakeGenericType(keyType), keyList, serializeMode);
            WriteList(typeof(List<>).MakeGenericType(valueType), valueList, serializeMode);
        }

        private object ReadDictionary(Type type, SerializeMode serializeMode) {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            var keyList = (IList) ReadList(typeof(List<>).MakeGenericType(keyType), serializeMode);
            var valueList = (IList) ReadList(typeof(List<>).MakeGenericType(valueType), serializeMode);
            Debug.Assert(keyList != null, nameof(keyList) + " != null");
            Debug.Assert(valueList != null, nameof(valueList) + " != null");
            Debug.Assert(keyList.Count == valueList.Count, $"{nameof(keyList)}.Count == {nameof(valueList)}.Count");

            var dict = (IDictionary) Activator.CreateInstance(type);
            Debug.Assert(dict != null, nameof(dict) + " != null");
            for (var i = 0; i < keyList.Count; i++) {
                Debug.Assert(keyList[i] != null, $"{nameof(keyList)}[i] != null");
                dict.Add(keyList[i], valueList[i]);
            }

            return dict;
        }

        private void WriteEnum(Type type, object obj) {
            var underlyingType = type.GetEnumUnderlyingType();
            var underlyingValue = Convert.ChangeType(obj, underlyingType);
            Write(underlyingType, underlyingValue, SerializeMode.Default);
        }

        private object ReadEnum(Type type) {
            var underlyingType = type.GetEnumUnderlyingType();
            var underlyingValue = Read(underlyingType, SerializeMode.Default);
            return Enum.ToObject(type, underlyingValue);
        }

        internal void WriteTypeId(TypeId typeId) {
            var type = typeId.Type;
            if (!type.IsGenericType) {
                if (LogOptions.LOG_SERIALIZATION_WRITE)
                    Log.Info($"Writing TypeId of {SerializeUtils.FriendlyName(typeId.Type)} [{sizeof(char) * typeId.ID.Length} bytes]", null, "SERIALIZE-WRITE");
                Write(typeId.ID, SerializeMode.Default);
                return;
            }

            var typeDef = TypeIdUtils.Get(type.GetGenericTypeDefinition());
            if (LogOptions.LOG_SERIALIZATION_WRITE)
                Log.Info($"Writing TypeId of {SerializeUtils.FriendlyName(typeDef.Type)} [{sizeof(char) * typeDef.ID.Length} bytes]", null, "SERIALIZE-WRITE");
            Write(typeDef.ID, SerializeMode.Default);
            var genericArguments = type.GetGenericArguments();
            foreach (var arg in genericArguments) {
                WriteTypeId(TypeIdUtils.Get(arg));
            }
        }

        internal TypeId ReadTypeId() {
            var type = TypeIdUtils.FindTypeByName(Read<string>(SerializeMode.Default));
            if (LogOptions.LOG_SERIALIZATION_READ) Log.Info($"Reading TypeId of {SerializeUtils.FriendlyName(type)}", null, "SERIALIZE-READ");
            if (!type.IsGenericTypeDefinition) return TypeIdUtils.Get(type);

            var genericArguments = new List<TypeId>();
            for (var i = 0; i < type.GetGenericArguments().Length; i++) {
                genericArguments.Add(ReadTypeId());
            }

            return TypeIdUtils.Get(type.MakeGenericType(genericArguments.Select(id => id.Type).ToArray()));
        }

        private int GetSize(Type type, object obj) {
            if (SerializeUtils.BuiltinTypes.Contains(type)) return type.SizeOf();
            if (SerializeUtils.IsArray(type)) {
                var elemType = SerializeUtils.GetListElementType(type);
                var elemSize = GetSize(elemType, elemType.Instance());
                if (elemSize == -1) return -1;
                return elemSize * ((Array) obj).GetLength(0);
            }

            if (SerializeUtils.IsList(type)) {
                var elemType = SerializeUtils.GetListElementType(type);
                var elemSize = GetSize(elemType, elemType.Instance());
                if (elemSize == -1) return -1;
                return elemSize * ((IList) obj).Count;
            }

            if (SerializeUtils.IsDictionary(type)) {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                var keySize = GetSize(keyType, keyType.Instance());
                var valueSize = GetSize(valueType, valueType.Instance());
                if (keySize == -1 || valueSize == -1) return -1;
                return (keySize + valueSize) * ((ICollection) obj).Count;
            }

            if (type.IsEnum) return type.GetEnumUnderlyingType().SizeOf();

            return -1;
        }

        /**
		 * Return the bytes that have been written into this Packet.
		 * Only works in Write mode.
		 */
        internal byte[] GetBytes() {
            //If we opened the Packet in writing mode, we'll probably need to send it at some point.
            //MemoryStream can either return the whole buffer or simply the part of the buffer that has been filled,
            //which is what we do here using ToArray()
            return ((MemoryStream) writer.BaseStream).ToArray();
        }

        internal byte[] GetBytesReadMode() {
            return ((MemoryStream) reader.BaseStream).ToArray();
        }
    }
}