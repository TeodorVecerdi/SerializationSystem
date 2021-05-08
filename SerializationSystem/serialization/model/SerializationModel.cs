using System;
using System.Linq;
using System.Reflection;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    internal class SerializationModel {
        internal readonly SerializationConstructor Constructor;
        internal readonly FieldInfo[] Fields;

        internal SerializationModel(Type type, SerializeMode serializeMode) {
            if (type.IsInterface) {
                Log.Warn($"Trying to build serialization model for interface type {type.FullName}", messageTitle: "SERIALIZE-WARN");
                return;
            }
            var ctor = type.Ctor();
            var parameters = SerializeUtils.CtorParameters(ctor);
            Constructor = new SerializationConstructor(ctor.Constructor, parameters);

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            switch (serializeMode) {
                case SerializeMode.ExplicitFields:
                    Fields = fields.Where(SerializeUtils.HasSerializedAttr).ToArray();
                    break;
                case SerializeMode.AllPublicFields:
                    Fields = fields.Where(field => SerializeUtils.HasSerializedAttr(field) || field.IsPublic && !SerializeUtils.HasNonSerializedAttr(field)).ToArray();
                    break;
                case SerializeMode.AllFields:
                    Fields = fields.Where(field => !SerializeUtils.HasNonSerializedAttr(field)).ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(serializeMode), serializeMode, null);
            }

            foreach (var field in Fields) {
                if(field.FieldType.IsInterface) continue;   
                if (!SerializeUtils.IsTriviallySerializable(field.FieldType) && !Serializer.HasSerializationModel(field.FieldType, serializeMode)) {
                    Serializer.BuildSerializationModel(field.FieldType, serializeMode);
                }
            }
        }
    }
}