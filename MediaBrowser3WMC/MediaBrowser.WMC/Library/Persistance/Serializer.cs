using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection.Emit;
using System.Reflection;
using System.Diagnostics;
using System.Linq.Expressions;
using MediaBrowser.Library.Entities.Attributes;

namespace MediaBrowser.Library.Persistance {


    public class Serializer {
        static Dictionary<string, Type> typeMap = new Dictionary<string, Type>();
        static Dictionary<Type, Serializer> serializers = new Dictionary<Type, Serializer>();

        static Dictionary<Type, bool> skipValidatingSize = new Dictionary<Type, bool>();

        private static bool SkipValidatingSize(Type t) {
            lock (skipValidatingSize) {
                bool rval;
                if (skipValidatingSize.TryGetValue(t, out rval)) {
                    return rval; 
                }

                var skipValidation = t.GetCustomAttributes(typeof(SkipSerializationValidationAttribute), true);
                skipValidatingSize[t] = skipValidation != null && skipValidation.Length == 1;
                return skipValidatingSize[t];
            }
        }

        private static Serializer GetSerializer(Type type)
        {
            Serializer serializer;
            lock (serializers) {
                if (!serializers.TryGetValue(type, out serializer)) {
                    Type baked = typeof(GenericSerializer<>).MakeGenericType(type);

                    object persistables = baked.GetProperty("Persistables", BindingFlags.NonPublic | BindingFlags.Static).GetGetMethod(true).Invoke(null, null);

                    serializer = new Serializer((IEnumerable<Persistable>)persistables, type);

                    serializers[type] = serializer;
                }
            }
            return serializer;
        }

        /// <summary>
        /// Serialize any object to a stream, will write a type manifest as well 
        /// </summary>
        public static void Serialize<T>(Stream stream, T obj) where T : class, new() {
            Serialize(new BinaryWriter(stream), obj);
        }

        /// <summary>
        /// Serialize any object to a stream, will write a type manifest as well 
        /// </summary>
        public static void Serialize<T>(BinaryWriter bw, T obj) where T : class, new() {
            if (obj == null) {
                throw new ArgumentNullException("object being serialized can not be null"); 
            }

            Type type = obj.GetType();

            bw.Write(type.FullName);
            long startPos = bw.BaseStream.Position;

            MethodInfo method;
            // Build in versioning data here... 
            if (Persistable.TryGetBinaryWrite(type, out method)) {
                if (method.IsStatic) {
                    method.Invoke(null, new object[] {bw, obj });
                } else {
                    method.Invoke(bw, new object[] { obj });
                }
            } else {
                if (typeof(T) == type) {
                    GenericSerializer<T>.Serialize(obj, bw);
                } else {
                    // slower
                    Serializer.GetSerializer(type).SerializeInternal(obj, bw);
                }
            }

            // write the length so we can validate. 
            if (!SkipValidatingSize(type)) {
                bw.Write(bw.BaseStream.Position - startPos);
            }

        }

        public static T Deserialize<T>(Stream stream) where T : class, new() {
            return Deserialize<T>(new BinaryReader(stream));
        }

        /// <summary>
        /// Deserialize an object that was serialized using the Serialize method,
        ///  has robustness to version changes 
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static T Deserialize<T>(BinaryReader reader) where T : class, new() { 
            Type type = GetCachedType(reader.ReadString());
            if (type == null) return null; //probably an old type or from plugin that is no longer installed

            long startPos = reader.BaseStream.Position;

            T deserialized;

            MethodInfo method;
            // versioning goes here 
            if (Persistable.TryGetBinaryRead(type, out method)) {
                
                if (method.IsStatic) {
                    deserialized = (T)method.Invoke(null, new object[] { reader } );
                } else {
                    deserialized = (T)method.Invoke(reader, null);
                }
            } else {
                if (typeof(T) == type) {
                    deserialized = GenericSerializer<T>.Deserialize(reader);
                } else {
                    deserialized = (T)Serializer.GetSerializer(type).DeserializeInternal(reader);
                }
            }

            if (!SkipValidatingSize(type)) {
                if ((reader.BaseStream.Position - startPos) != reader.ReadInt64()) {
                    // its corrupt 
                    throw new SerializationException("Corrupt item in cache");
                }
            } 

            return deserialized;
        } 


        /// <summary>
        /// Create an instance of the passed object
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static T Instantiate<T>(string aType) where T : class, new() { 
            Type type = GetCachedType(aType);
            if (type == null) return null; //old type or from plugin that is not installed

            T instance;

            MethodInfo method;
            if (Persistable.TryGetBinaryRead(type, out method)) {
                
                if (method.IsStatic) {
                    instance = (T)method.Invoke(null, new object[] { aType } );
                } else {
                    instance = (T)method.Invoke(aType, null);
                }
            } else {
                if (typeof(T) == type) {
                    instance = GenericSerializer<T>.Instantiate(aType);
                } else {
                    instance = (T)Serializer.GetSerializer(type).InstantiateInternal(aType);
                }
            }

            return instance;
        } 

        internal static Type GetCachedType(string typeName) {
            Type type;
            // tip for the reader: Dictonary gets will go in to a tail spin
            //   if you do not lock
            lock (typeMap) {
                if (!typeMap.TryGetValue(typeName, out type)) {
                    type = AppDomain
                        .CurrentDomain
                        .GetAssemblies()
                        .Select(a => a.GetType(typeName, false))
                        .Where(t => t != null)
                        .FirstOrDefault();
                    if (type != null) typeMap[typeName] = type;
                }
            }
            return type;
        }

        public static T Clone<T>(T obj) where T : class, new() {
            T rval;

            // tricky, the T passed in may not be actual type of the object being cloned 
            Serializer serializer = GetSerializer(obj.GetType());

            using (var stream = new MemoryStream()) {
                var writer = new BinaryWriter(stream);
                serializer.SerializeInternal(obj, writer);
                stream.Position = 0;
                var reader = new BinaryReader(stream);
                rval = (T)serializer.DeserializeInternal(reader);
            }
            return rval;
        }


        Persistable[] persistables;
        Type type;
        Func<object> constructor;

        private Serializer(IEnumerable<Persistable> persistables, Type type) {
            this.type = type;
            this.persistables = persistables.ToArray();

            DynamicMethod dm = new DynamicMethod("FastConstruct", type,
                Type.EmptyTypes, typeof(Serializer).Module, true);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Newobj, type.GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Ret);
            constructor = (Func<object>)dm.CreateDelegate(typeof(Func<object>));
        }


        private void SerializeInternal(object data, BinaryWriter bw) {
            for (int i = 0; i < persistables.Length; i++) {
                persistables[i].Serialize(bw, data);
            }
        }
 

        private object DeserializeInternal(BinaryReader br) {
            int ndx = 0;
            object obj = null;
            try {
                obj = constructor.DynamicInvoke();

                for (int i = 0; i < persistables.Length; i++) {
                    ndx = i;
                    persistables[i].Deserialize(br, obj);
                }

                return obj;
            } catch (Exception exception) {
                throw new SerializationException("Failed to deserialize object, corrupt stream. ("+exception != null && obj != null ? exception.Message + " Type: "+obj.GetType().Name +" Attribute: "+persistables[ndx].MemberInfo.Name : "", exception);
            }
        }

        private object InstantiateInternal(string aType) {
            try {
                object obj = constructor.DynamicInvoke();

                return obj;
            } catch (Exception exception) {
                throw new SerializationException("Failed to instantiate object: "+aType, exception);
            }
        }

        public void MergeObjects(object source, object target, bool force) {
            foreach (var persistable in persistables) {
                if (persistable.GetValue(target) == null || force) {
                    persistable.SetValue(target, persistable.GetValue(source));
                } else
                    if (persistable.GetAttributes<DontClearOnForcedRefreshAttribute>() != null &&
                        persistable.GetValue(target) != null &&
                        persistable is object)
                    {
                        // go another level on the merge if this item was not cleared
                        var innerSource = persistable.GetValue(source);
                        var innerTarget = persistable.GetValue(target);
                        GetSerializer(innerSource.GetType()).MergeObjects(innerSource, innerTarget, force);
                    }

            }
        }

        /// <summary>
        /// Merge all non-null persistable fields in source into target
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void Merge(object source, object target) {
            Merge(source, target, false);
        }

        /// <summary>
        /// Merge persistable fields in source into target
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="force">force non null fields to be overwritten</param>
        public static void Merge(object source, object target, bool force) {
            GetSerializer(source.GetType()).MergeObjects(source, target, force);
        }

        public static IEnumerable<Persistable> GetPersistables(object obj) {
            return GetSerializer(obj.GetType()).persistables;
        }
  
    }

    
}
