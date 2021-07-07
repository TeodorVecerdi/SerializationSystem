using System;
using System.Collections.Concurrent;
using Serialization.Internal;
using SerializationSystem.Logging;

namespace SerializationSystem.Internal {
    public static class InternalTesting {
        public static void Run() {
            //!! REFERENCE ------------------------------------
            var hello = new Hello(
                new Transform(new Vector3(123.0f), new Vector3Rand(51, 23.4, 10230), 123.0m),
                "Hello World",
                new Impl("John", 69),
                new Impl2("Jason", "Hello", "World", new Impl2.Vector2{X = 51.0f, Y = 123.5123f})
            );
            //!! ----------------------------------------------
            
            // Get recursive types into a set
            var types = new ConcurrentSet<Type>();
            var typeToIndex = new ConcurrentDictionary<Type, int>();
            AddTypes(typeof(Hello), hello, types, typeToIndex);
            
            foreach (var type in typeToIndex) {
                Console.WriteLine($"{type.Value} => {type.Key}");
            }
        }

        private static void AddTypes(Type type, object obj, ConcurrentSet<Type> types, ConcurrentDictionary<Type, int> typeToIndex) {
            var rootModel = new ContextAwareSerializationModel(type, obj, SerializeMode.AllFields);
            foreach (var (fieldInfo, actualType) in rootModel.Fields) {
                var fieldValue = fieldInfo.GetValue(obj);
                if(fieldValue == null) continue;
                
                if (types.Add(actualType)) {
                    typeToIndex[actualType] = types.Count - 1;
                }
                if (!SerializeUtils.IsTriviallySerializable(actualType)) {
                    AddTypes(actualType, fieldValue, types, typeToIndex);
                }
            }
        }
    }
    
    public interface IInterface {
        string Name { get; set; }
    }

    public interface IInterface2 : IInterface {
        string Address { get; set; }
        string PhoneNumber { get; set; }
    }

    public class Impl : IInterface {
        [field: Serialized]
        public string Name { get; set; }
        [Serialized] public int Age;
        
        public Impl() { }
        public Impl(string name, int age) {
            Name = name;
            Age = age;
        }
        
        public override string ToString() {
            return $"{Name} is {Age} years old";
        }
    }

    public class Impl2 : IInterface2 {
        public class Vector2 {
            [Serialized] public float X;
            [Serialized] public float Y;
        }
        
        [field: Serialized] public string Name { get; set; }
        [field: Serialized] public string Address { get; set; }
        [field: Serialized] public string PhoneNumber { get; set; }
        [Serialized] public Vector2 Position;

        public Impl2() { }
        public Impl2(string name, string address, string phoneNumber, Vector2 position) {
            Name = name;
            Address = address;
            PhoneNumber = phoneNumber;
            Position = position;
        }

        public override string ToString() {
            return $"{Name} is at position [{Position.X}, {Position.Y}]. They live at {Address} and can be contacted at {PhoneNumber}";
        }

    }

    public class Parent {
        [Serialized]
        public IInterface Interface;

        public Parent() { }
        public Parent(IInterface @interface) {
            Interface = @interface;
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() {
            return $"Interface: {Interface.GetType().FullName} => {Interface}";
        }
    }

    public class Vector3 {
        public Vector3() : this(0.0f) {
        }

        public Vector3(float value) : this(value, value, value) {
        }
        
        public Vector3(float x, float y, float z) {
            X = x;
            Y = y;
            Z = z;
        }

        [field: Serialized] public float X { get; set; }
        [field: Serialized] public float Y { get; set; }
        [field: Serialized] public float Z { get; set; }
    }

    public class Vector3Rand {
        public Vector3Rand() : this(0) {
        }

        public Vector3Rand(float value) : this((int)value, (double)value, (long)value) {
        }
        
        public Vector3Rand(int x, double y, long z) {
            X = x;
            Y = y;
            Z = z;
        }

        [field: Serialized] public int X { get; set; }
        [field: Serialized] public double Y { get; set; }
        [field: Serialized] public long Z { get; set; }
    }

    public class Transform {
        public Transform(Vector3 position, Vector3Rand scale, decimal rotation) {
            Position = position;
            Scale = scale;
            Rotation = rotation;
        }

        [field: Serialized] public Vector3 Position { get; set; }
        [field: Serialized] public Vector3Rand Scale { get; set; }
        [field: Serialized] public decimal Rotation { get; set; }
    }

    public class Hello {
        [field: Serialized] public Transform Transform { get; set; }
        [field: Serialized] public string Name { get; set; }
        [field: Serialized] public IInterface Something { get; set; }
        [field: Serialized] public IInterface SomethingElse { get; set; }

        public Hello(Transform transform, string name, IInterface something, IInterface somethingElse) {
            Transform = transform;
            Name = name;
            Something = something;
            SomethingElse = somethingElse;
        }
    }
}