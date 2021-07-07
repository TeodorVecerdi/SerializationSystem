namespace SerializationSystem {
    public enum ModelType {
        /// <summary>
        /// Can accurately cache the SerializationModel behind object types,
        /// but packet sizes are bigger because the object type is written before each object value.
        /// </summary>
        Normal,
        
        /// <summary>
        /// Has a slightly lower packet size at the cost of runtime performance
        /// since the SerializationModel depends on the specific object being serialized and cannot be cached
        /// </summary>
        ContextAware,
        Default = Normal
    }
}