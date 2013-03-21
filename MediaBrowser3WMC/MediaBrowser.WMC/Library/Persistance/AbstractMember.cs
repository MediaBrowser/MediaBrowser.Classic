using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace MediaBrowser.Library.Persistance {
    public abstract class AbstractMember {

        MemberInfo memberInfo; 

        public AbstractMember(MemberInfo memberInfo) {
            this.memberInfo = memberInfo;
        }

        public abstract object Read(object instance);
        public abstract void Write(object instance, object value);
        public abstract Type Type { get; }
        public abstract string Name { get; }

        public object[] GetAttributes() {
            return memberInfo.GetCustomAttributes(false); 
        } 


    }

    class PropertyMember : AbstractMember {

        PropertyInfo propertyInfo;

        public PropertyMember(PropertyInfo propertyInfo) : base(propertyInfo) {
            this.propertyInfo = propertyInfo;
        }

        public override object Read(object instance) {
            return propertyInfo.GetGetMethod().Invoke(instance, System.Type.EmptyTypes);
        }

        public override void Write(object instance, object value) {
            propertyInfo.GetSetMethod().Invoke(instance, new object[] { value });
        }

        public override Type Type {
            get { return propertyInfo.PropertyType; }
        }

        public override string Name {
            get { return propertyInfo.Name; }
        }
    }

    class FieldMember : AbstractMember {
        FieldInfo fieldInfo;

        public FieldMember(FieldInfo fieldInfo) : base(fieldInfo) {
            this.fieldInfo = fieldInfo;
        }

        public override object Read(object instance) {
            return fieldInfo.GetValue(instance);
        }

        public override void Write(object instance, object value) {
            fieldInfo.SetValue(instance, value);
        }

        public override Type Type {
            get { return fieldInfo.FieldType; }
        }

        public override string Name {
            get { return fieldInfo.Name; }
        }
    }
}
