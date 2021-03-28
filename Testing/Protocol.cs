using System;
using SerializationSystem;
using SerializationSystem.Logging;

namespace Testing {
    public class SerializationError {
        [Serialized] public string ErrorType;
        [Serialized] public string Message;

        public SerializationError(string errorType, string message) {
            ErrorType = errorType;
            Message = message;
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() {
            return $"SerializationError\nErrorType: {ErrorType}\nMessage: {Message}";
        }
    }
    
    public class TestHandler : SerializationExceptionHandler {
        public override object HandleDeserializationException(Exception exception) {
            Log.Except(exception, includeStackTrace: true, includeTimestamp: false, includeFileInfo: false);
            ReplaceDeserializationResult(new SerializationError(exception.GetType().FullName, exception.Message));
            return null;
        }

        public override byte[] HandleSerializationException(Exception exception) {
            Log.Except(exception, includeStackTrace: true, includeTimestamp: false, includeFileInfo: false);
            ReplaceSerializationResult(Serializer.Serialize(new SerializationError(exception.GetType().FullName, exception.Message)));
            return new byte[0];
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
}