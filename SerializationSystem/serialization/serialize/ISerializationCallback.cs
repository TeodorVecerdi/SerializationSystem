namespace SerializationSystem {
    public interface ISerializationCallback {
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }
}