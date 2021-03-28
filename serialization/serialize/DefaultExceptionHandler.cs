using System;

namespace SerializationSystem.Internal {
    internal sealed class DefaultExceptionHandler : SerializationExceptionHandler {
        public override object HandleDeserializationException(Exception exception) {
            throw exception;
        }

        public override byte[] HandleSerializationException(Exception exception) {
            throw exception;
        }
    }
}