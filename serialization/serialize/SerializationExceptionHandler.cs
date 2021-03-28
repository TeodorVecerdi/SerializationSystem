using System;

namespace SerializationSystem {
    public abstract class SerializationExceptionHandler {
        public abstract object HandleDeserializationException(Exception exception);
        public abstract byte[] HandleSerializationException(Exception exception);

        protected static void ReplaceSerializationResult(byte[] replacement) => Serializer.ReplaceSerializationResult(replacement);
        protected static void ReplaceDeserializationResult(object replacement) => Serializer.ReplaceDeserializationResult(replacement);
    }
}