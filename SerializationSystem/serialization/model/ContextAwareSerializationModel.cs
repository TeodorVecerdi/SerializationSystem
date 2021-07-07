using System;
using System.Linq;
using System.Reflection;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    internal class ContextAwareSerializationModel {
        internal record FieldInfoWrapper(FieldInfo FieldInfo, Type ActualType);

        internal readonly SerializationConstructor Constructor;
        internal readonly FieldInfoWrapper[] Fields;

        internal ContextAwareSerializationModel(Type type, object obj, SerializeMode serializeMode) {
            if (type.IsInterface) {
                Log.Warn($"Trying to build serialization model for interface type {type.FullName}", messageTitle: "SERIALIZE-WARN");
                return;
            }
            var ctor = type.Ctor();
            var parameters = SerializeUtils.CtorParameters(ctor);
            Constructor = new SerializationConstructor(ctor.Constructor, parameters);

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fields = serializeMode switch {
                SerializeMode.ExplicitFields => fields.Where(SerializeUtils.HasSerializedAttr).ToArray(),
                SerializeMode.AllPublicFields => fields.Where(field => SerializeUtils.HasSerializedAttr(field) || field.IsPublic && !SerializeUtils.HasNonSerializedAttr(field))
                                                       .ToArray(),
                SerializeMode.AllFields => fields.Where(field => !SerializeUtils.HasNonSerializedAttr(field)).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(serializeMode), serializeMode, null)
            };

            Fields = new FieldInfoWrapper[fields.Length];
            for (var i = 0; i < fields.Length; i++) {
                var fieldInfo = fields[i];
                if (!fieldInfo.FieldType.IsInterface) {
                    Fields[i] = new FieldInfoWrapper(fieldInfo, fieldInfo.FieldType);
                    continue;
                }

                // Get interface value & concrete type
                var fieldValue = fieldInfo.GetValue(obj);
                if (fieldValue == null) {
                    Fields[i] = new FieldInfoWrapper(fieldInfo, fieldInfo.FieldType);
                    continue;
                }

                Fields[i] = new FieldInfoWrapper(fieldInfo, fieldValue.GetType());
            }
        }
    }
}