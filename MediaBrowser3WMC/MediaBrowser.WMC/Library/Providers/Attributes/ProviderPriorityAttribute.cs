using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Providers.Attributes {

    [global::System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ProviderPriorityAttribute : Attribute {

        /// <summary>
        /// Specify the order of the provider in the chain, when omitted providers are added to the end of the chain
        /// </summary>
        /// <param name="priority">priority of the provider in the chain, 0 is the highest</param>
        public ProviderPriorityAttribute(int priority) {
            Priority = priority;
        }

        /// <summary>
        /// The priority of this provider in the chain, lowest number wins
        /// </summary>
        public int Priority { get; private set; }
    }


}
