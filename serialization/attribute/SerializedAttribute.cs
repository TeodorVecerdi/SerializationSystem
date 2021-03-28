using System;

namespace SerializationSystem {
    /// <summary>Marks a field as serialized.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SerializedAttribute : Attribute {
        
    }
}