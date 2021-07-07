using SerializationSystem;
using SerializationSystem.Internal;
using SerializationSystem.Logging;

namespace Testing {
    internal static class Program {
        public static void Main() {
            LogOptions.LogSerialization = false;
            LogOptions.LogSerializationRead = false;
            LogOptions.LogSerializationWrite = false;
            LogOptions.LogSerializationReplacements = false;
            Serializer.ExceptionHandler = new TestHandler();
            
            InternalTesting.Run();
            

            /*var test1 = new Parent(new Impl("Michael", 14));
            var test2 = new Parent(new Impl2("John", "Cool St. 69", "+69 420 69 420", new Impl2.Vector2{X=14.5f, Y=-4.5f}));
            
            Log.Message("BEFORE:");
            Log.Message(test1);
            Log.Message(test2);
            
            var bytes1 = Serializer.Serialize(test1);
            var bytes2 = Serializer.Serialize(test2);
            var test1d = Serializer.Deserialize(bytes1);
            var test2d = Serializer.Deserialize(bytes2);
            
            Log.Message("AFTER:");
            Log.Message(test1d);
            Log.Message(test2d);*/
        }
    }
}