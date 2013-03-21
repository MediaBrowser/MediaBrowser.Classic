using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Collections;

namespace MediaBrowser.Library.Persistance {
    class PersistableField : Persistable {

        FieldInfo fieldInfo;

        public PersistableField(FieldInfo fieldInfo) {
            this.fieldInfo = fieldInfo;
            Init();
        }

        public override void EmitRead(ILGenerator il) {
            il.Emit(OpCodes.Call, GetBinaryRead(ResolvedType));
            il.Emit(OpCodes.Stfld, fieldInfo);
        }

        
        public override void EmitWrite(ILGenerator il) {
            il.Emit(OpCodes.Ldfld, fieldInfo);
            il.Emit(OpCodes.Call, GetBinaryWrite(ResolvedType));
        }

        protected override Type Type {
            get { return fieldInfo.FieldType; }
        }


        public override object GetValue(object o) {
            return fieldInfo.GetValue(o);
        }

        public override void SetValue(object o, object val) {
            fieldInfo.SetValue(o, val);
        }

        public override MemberInfo MemberInfo {
            get { return fieldInfo; }
        }
    }
}
