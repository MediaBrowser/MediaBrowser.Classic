using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Proxies;
using System.Drawing;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting;
using System.ComponentModel;

namespace MediaBrowser.Library.Extensions {

    public interface IInterceptorNotifiable {
        void OnPropertyChanged(string propertyName);
    }

    /// <summary>
    /// A simple RealProxy based property interceptor
    /// Will call OnPropertyChanged whenever and property on the child object is changed
    /// </summary>
    public class Interceptor<T> where T : MarshalByRefObject, IInterceptorNotifiable, new() {

        class InterceptorProxy : RealProxy {
            T proxy;
            T target;

            public InterceptorProxy(T target)
                : base(typeof(T)) {
                this.target = target;
            }

            public override object GetTransparentProxy() {
                proxy = (T)base.GetTransparentProxy();
                return proxy;
            }


            public override IMessage Invoke(IMessage msg) {

                IMethodCallMessage call = msg as IMethodCallMessage;
                if (call != null) {

                    var result = InvokeMethod(call);
                    if (call.MethodName.StartsWith("set_")) {
                        string propName = call.MethodName.Substring(4);
                        target.OnPropertyChanged(propName);
                    }
                    return result;
                } else {
                    throw new NotSupportedException();
                }
            }

            IMethodReturnMessage InvokeMethod(IMethodCallMessage callMsg) {
                return RemotingServices.ExecuteMessage(target, callMsg);
            }

        }


        public static T Create() {
            var interceptor = new InterceptorProxy(new T());
            return (T)interceptor.GetTransparentProxy();
        }


        private Interceptor() {
        }


    }
}
