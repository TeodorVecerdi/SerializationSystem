namespace SerializationSystem {
    public enum SerializeMode {
        /// <summary>
        /// Serializes all fields marked with the [Serialized] attribute (Default)
        /// </summary>
        ExplicitFields,
        /// <summary>
        /// Serializes all fields marked with the [Serialized] attribute and any other
        /// public field, except fields marked with the [NonSerialized] attribute
        /// </summary>
        AllPublicFields,
        /// <summary>
        /// Serializes all fields, except fields marked with the [NonSerialized] attribute
        /// </summary>
        AllFields,
        /// <summary>
        /// <inheritdoc cref="ExplicitFields"/>
        /// </summary>
        Default = ExplicitFields
    }
}