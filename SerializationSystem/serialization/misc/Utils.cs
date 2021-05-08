namespace SerializationSystem.Internal {
    internal static class Utils {
        internal static void KeepUnusedVariable<T>(ref T t) => t = ref Nop(ref t);
        private static ref T Nop<T>(ref T t) => ref t;
    }
}