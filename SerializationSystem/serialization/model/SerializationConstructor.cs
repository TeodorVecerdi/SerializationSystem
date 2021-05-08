using System;
using System.Reflection;

namespace SerializationSystem.Internal {
    internal class SerializationConstructor {
        private readonly ConstructorInfo constructor;
        private readonly object[] parameters;

        internal SerializationConstructor(ConstructorInfo constructor, object[] parameters) {
            this.constructor = constructor;
            this.parameters = parameters;
        }

        internal object Create() {
            return constructor.Invoke(parameters);
        }
    }
}