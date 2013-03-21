using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Collections;

namespace MediaBrowser.Library.Persistance {
    public abstract class Persistable {

        static Dictionary<Type, MethodInfo> readFieldMethods;
        static Dictionary<Type, MethodInfo> writeFieldMethods;

        static Persistable() {
            readFieldMethods = new Dictionary<Type, MethodInfo>();
            writeFieldMethods = new Dictionary<Type, MethodInfo>();

            foreach (var methodInfo in typeof(BinaryWriter).GetMethods()) {
                if (methodInfo.Name == "Write") {
                    var parameters = methodInfo.GetParameters();
                    if (parameters != null && parameters.Length == 1) {
                        writeFieldMethods[parameters[0].ParameterType] = methodInfo;
                    }
                }
            }


            foreach (var methodInfo in typeof(BinaryReader).GetMethods()) {
                if (methodInfo.Name.StartsWith("Read") && methodInfo.Name != "Read")
                {
                    var parameters = methodInfo.GetParameters();
                    if (parameters == null || parameters.Length == 0) {
                        readFieldMethods[methodInfo.ReturnType] = methodInfo;
                    }
                }
            }


            foreach (var methodInfo in typeof(Persistable).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)) {
                if (methodInfo.Name.StartsWith("Write")) {
                    var parameters = methodInfo.GetParameters();
                    if (parameters != null && parameters.Length == 2) {
                        writeFieldMethods[parameters[1].ParameterType] = methodInfo;
                    }
                }
            }


            foreach (var methodInfo in typeof(Persistable).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)) {
                if (methodInfo.Name.StartsWith("Read") && methodInfo.Name != "Read")
                {
                    var parameters = methodInfo.GetParameters();
                    if (parameters == null || parameters.Length == 1) {
                        readFieldMethods[methodInfo.ReturnType] = methodInfo;
                    }
                }
            }


         }

        public abstract void EmitRead(ILGenerator il);
        public abstract void EmitWrite(ILGenerator il);

        Action<BinaryWriter, object> writer;
        Action<BinaryReader, object> reader;

        protected void Init() {

            if (!readFieldMethods.ContainsKey(ResolvedType) ||
                !writeFieldMethods.ContainsKey(ResolvedType)) {

                // performance can be improved here if we use the specific serializer then we can avoid a cast
                writer = (BinaryWriter binaryWriter, object obj) => {
                    object val = GetValue(obj);
                    binaryWriter.Write(val != null);
                    if (val != null) {
                        Serializer.Serialize<object>(binaryWriter, GetValue(obj));
                    }
                };
                reader = (BinaryReader binaryReader, object obj) => {
                    bool exists = binaryReader.ReadBoolean();
                    if (exists) {
                        SetValue(obj, Serializer.Deserialize<object>(binaryReader));
                    }
                };

            } else {
                writer = GenerateWriter();
                reader = GenerateReader();
            }
        }
   

        Action<BinaryWriter, object> GenerateWriter() {
            DynamicMethod dm = new DynamicMethod("WriteToBinaryWriter",
                null,
                new Type[] { typeof(BinaryWriter), typeof(object) },
                typeof(Persistable).Module,
                true);

            ILGenerator il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            EmitWrite(il);
            il.Emit(OpCodes.Ret);

            return (Action<BinaryWriter, object>)dm.CreateDelegate(typeof(Action<BinaryWriter, object>));
        }

        Action<BinaryReader, object> GenerateReader() {
            DynamicMethod dm = new DynamicMethod("ReadFromBinaryReader",
               typeof(void),
               new Type[] { typeof(BinaryReader), typeof(object) },
               typeof(Persistable).Module,
               true);

            ILGenerator il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_0);
            EmitRead(il);
            il.Emit(OpCodes.Ret);

            return (Action<BinaryReader,object>)dm.CreateDelegate(typeof(Action<BinaryReader,object>));
        } 



        public Action<BinaryWriter, object> Serialize { 
            get {
                return writer;
            } 
        }
        public Action<BinaryReader, object> Deserialize {
            get {
                return reader;
            }
        }

        public abstract object GetValue(object o);
        public abstract void SetValue(object o, object val);
        public abstract MemberInfo MemberInfo { get;  }

        public  T[] GetAttributes<T>() where T : Attribute { 
            // we could cache this ... 
            T[] found;
            var attribs = MemberInfo.GetCustomAttributes(typeof(T), false);
            if (attribs != null && attribs.Length > 0) {
                found = attribs.Select(a => (T)a).ToArray();
            } else {
                found = null;
            }
            return found;
        }

        protected abstract Type Type { get;  }

        public Type ResolvedType { 
            get {
                Type type = Type; 
                if (type.BaseType == typeof(Enum)) {
                    type = typeof(int);
                }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
                    type = typeof(IList);
                }
                return type; 
            }  
        } 

        static IList ReadList(BinaryReader br) {
            IList list = null;
            if (!br.ReadBoolean()) {
                Type t = Serializer.GetCachedType(br.ReadString());  
                // slow ... 
                list = (IList)t.GetConstructor(new Type[] {}).Invoke(null);

                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    if (!br.ReadBoolean()) {
                        list.Add(Serializer.Deserialize<object>(br.BaseStream));
                    }
                }

            }
            return list;
        }

        static void WriteList(BinaryWriter bw, IList list) {
            bw.Write(list == null);
            if (list != null) {

                bw.Write(list.GetType().FullName);
                bw.Write(list.Count);

                foreach (var item in list) {
                    // stuff is boxed so this is safe 
                    bw.Write(item == null);
                    if (item != null) {
                        Serializer.Serialize(bw.BaseStream, item);
                    }
                }
            }
        }

        internal static MethodInfo GetBinaryWrite(Type type) {
            return writeFieldMethods[type];
        }

        internal static MethodInfo GetBinaryRead(Type type) {
            return readFieldMethods[type];
        }

        internal static bool TryGetBinaryWrite(Type type, out MethodInfo methodInfo) {
            return writeFieldMethods.TryGetValue(type, out methodInfo);
        }

        internal static bool TryGetBinaryRead(Type type, out MethodInfo methodInfo) {
            return readFieldMethods.TryGetValue(type, out methodInfo);
        }


        #region Specific Serialization implementations
        protected static MethodInfo GetStaticMethodInfo(string name) {
            return typeof(PersistableField).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        }


        static Guid ReadGuid(BinaryReader br) {
            return new Guid(br.ReadBytes(16));
        }

        static void WriteGuid(BinaryWriter bw, Guid guid) {
            bw.Write(guid.ToByteArray());
        }

        static DateTime ReadDateTime(BinaryReader br) {
            return new DateTime(br.ReadInt64());
        }

        static void WriteDateTime(BinaryWriter bw, DateTime date) {
            bw.Write(date.Ticks);
        }

        static string ReadString(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadString();
            else
                return null;
        }

        static void WriteString(BinaryWriter bw, string val) {
            bw.Write(val != null);
            if (val != null)
                bw.Write(val);
        }

        #endregion

        #region nullables - This area can be code-gened - I chose to implement nullables this way so I can avoid having complex logic in the il generator


        static Int16? ReadNullableInt16(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadInt16();
            else
                return null;
        }

        static void WriteNullableInt16(BinaryWriter bw, Int16? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Int16)val);
            }
        }

        static Int32? ReadNullableInt32(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadInt32();
            else
                return null;
        }

        static void WriteNullableInt32(BinaryWriter bw, Int32? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Int32)val);
            }
        }

        static Int64? ReadNullableInt64(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadInt64();
            else
                return null;
        }

        static void WriteNullableInt64(BinaryWriter bw, Int64? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Int64)val);
            }
        }

        static Single? ReadNullableSingle(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadSingle();
            else
                return null;
        }

        static void WriteNullableSingle(BinaryWriter bw, Single? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Single)val);
            }
        }

        static Double? ReadNullableDouble(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadDouble();
            else
                return null;
        }

        static void WriteNullableDouble(BinaryWriter bw, Double? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Double)val);
            }
        }

        static Boolean? ReadNullableBoolean(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadBoolean();
            else
                return null;
        }

        static void WriteNullableBoolean(BinaryWriter bw, Boolean? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Boolean)val);
            }
        }


        static Char? ReadNullableChar(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadChar();
            else
                return null;
        }

        static void WriteNullableChar(BinaryWriter bw, Char? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Char)val);
            }
        }

        static Byte? ReadNullableByte(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadByte();
            else
                return null;
        }

        static void WriteNullableByte(BinaryWriter bw, Byte? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Byte)val);
            }
        }


        static Decimal? ReadNullableDecimal(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return br.ReadDecimal();
            else
                return null;
        }

        static void WriteNullableDecimal(BinaryWriter bw, Decimal? val) {
            bw.Write(val != null);
            if (val != null) {
                bw.Write((Decimal)val);
            }
        }

        static DateTime? ReadNullableDateTime(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return ReadDateTime(br);
            else
                return null;
        }

        static void WriteNullableDateTime(BinaryWriter bw, DateTime? val) {
            bw.Write(val != null);
            if (val != null) {
                WriteDateTime(bw,(DateTime)val);
            }
        }


        static Guid? ReadNullableGuid(BinaryReader br) {
            bool present = br.ReadBoolean();
            if (present)
                return ReadGuid(br);
            else
                return null;
        }

        static void WriteNullableGuid(BinaryWriter bw, Guid? val) {
            bw.Write(val != null);
            if (val != null) {
                WriteGuid(bw, (Guid)val);
            }
        }
 


       #endregion 
    
        
    }
}
