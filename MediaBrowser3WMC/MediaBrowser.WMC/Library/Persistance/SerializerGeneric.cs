using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Reflection.Emit;

namespace MediaBrowser.Library.Persistance {
    public static class GenericSerializer<T> where T : class, new() {


        static Persistable[] persistables = Persistables.ToArray();
 
        static IEnumerable<Persistable> Persistables {
            get {
                return Properties
                    .Select(p => (Persistable)new PersisatableProperty(p))
                    .Concat(
                        Fields.Select(f => (Persistable)new PersistableField(f))
                    );
            }
        }
      

        static IEnumerable<PropertyInfo> Properties {
            get {
                var type = typeof(T);
                var properties = GetPropertiesForType(type);

                type = type.BaseType;
                while (type != typeof(object) && type != null) {
                    properties = properties.Concat(GetPropertiesForType(type));
                    type = type.BaseType;
                }

                // distinctify the properties - to help out with weird inheritance stuff
                var dict = new Dictionary<string, PropertyInfo>();
                foreach (var property in properties) {
                    if (!dict.ContainsKey(property.Name)) {
                        dict[property.Name] = property;
                    }
                }

                return dict.Values;
            }
        }

        static IEnumerable<PropertyInfo> GetPropertiesForType(Type t) {
            return t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttributes(typeof(PersistAttribute), true).Length > 0)
                .Where(p => p.GetGetMethod(true) != null && p.GetSetMethod(true) != null);
        }


        static IEnumerable<FieldInfo> Fields {
            get {
                var type = typeof(T);
                var fields = GetFieldsForType(type);

                type = type.BaseType;
                while (type != typeof(object) && type != null) {
                    fields = fields.Concat(GetFieldsForType(type));
                    type = type.BaseType;
                }

                return fields;
            }
        }

        static IEnumerable<FieldInfo> GetFieldsForType(Type t) {
            return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttributes(typeof(PersistAttribute), true).Length > 0);
        }


        public static void Serialize(T data, Stream stream) {
            BinaryWriter bw = new BinaryWriter(stream);
            Serialize(data, bw);
        }

        public static void Serialize(T data, BinaryWriter bw) {
            for (int i = 0; i < persistables.Length; i++) {
                persistables[i].Serialize(bw, data);
            }
        }

        public static T Deserialize(Stream stream) {
            BinaryReader br = new BinaryReader(stream);
            return Deserialize(br);
        }

        public static T Deserialize(BinaryReader br) {
                T obj = new T();
            try {

                for (int i = 0; i < persistables.Length; i++) {
                    persistables[i].Deserialize(br, obj);
                }
                return obj;
            } catch (EndOfStreamException) {
                return obj; //partial object probably okay...
            } catch (Exception exception) {
                throw new SerializationException("Failed to deserialize object, corrupt stream.", exception);
            }
        }

        public static T Instantiate(string type) {
            try {
                T obj = new T();

                return obj;
            } catch (Exception exception) {
                throw new SerializationException("Failed to instantiate object: "+type, exception);
            }
        }
    
    }
}
