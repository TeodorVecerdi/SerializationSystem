namespace SerializationSystem.Logging {
    public static class LogOptions {
        public static bool LogSerialization { private get; set; } = true;
        public static bool LogSerializationReplacements { private get; set; } = true;
        public static bool LogSerializationWrite { private get; set; } = true;
        public static bool LogSerializationRead { private get; set; } = true;
        
        internal static bool LOG_SERIALIZATION => LogSerialization;
        internal static bool LOG_SERIALIZATION_REPLACEMENTS => LogSerializationWrite && LOG_SERIALIZATION;
        internal static bool LOG_SERIALIZATION_WRITE => LogSerializationWrite && LOG_SERIALIZATION;
        internal static bool LOG_SERIALIZATION_READ => LogSerializationRead && LOG_SERIALIZATION;
    }
}