using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;

namespace MediaBrowser.Library.Persistance {
    public class PersisatableProperty : Persistable {

        PropertyInfo propertyInfo;

        public PersisatableProperty(PropertyInfo propertyInfo) {
            this.propertyInfo = propertyInfo;
            Init();
        }

        public override void EmitRead(ILGenerator il) {
            il.Emit(OpCodes.Call, GetBinaryRead(ResolvedType));
            il.Emit(OpCodes.Call, propertyInfo.GetSetMethod(true));
        }

        public override void EmitWrite(ILGenerator il) {
            il.Emit(OpCodes.Call, propertyInfo.GetGetMethod(true));
            il.Emit(OpCodes.Call, GetBinaryWrite(ResolvedType));
        }

        protected override Type Type {
            get { return propertyInfo.PropertyType; }
        }


        public override object GetValue(object o) {
            return propertyInfo.GetValue(o, null);
        }

        public override void SetValue(object o, object val) {
            propertyInfo.SetValue(o, val, null);
        }

        public override MemberInfo MemberInfo {
            get { return propertyInfo; }
        }
    }
}
