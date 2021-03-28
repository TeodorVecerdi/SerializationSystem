using System;

namespace SerializationSystem {
    [AttributeUsage(AttributeTargets.Constructor)]
    public class PreferredCtorAttribute : Attribute {
        public object[] Arguments { get; }

        /// <summary>
        /// Specifies which constructor should be used when creating an object instance while deserializing.
        /// Uses whatever default parameters there are, and default values for all other parameters.
        /// </summary>
        public PreferredCtorAttribute() {
            Arguments = new object[0];
        }

        /// <summary>
        /// Specifies which constructor should be used when creating an object instance while deserializing.
        /// Uses <paramref name="arguments"/> as arguments for the constructor. If a value from <paramref name="arguments"/> doesn't match,
        /// then a default parameter or default value will be used.
        /// </summary>
        public PreferredCtorAttribute(params object[] arguments) {
            Arguments = arguments;
        }
    }
}