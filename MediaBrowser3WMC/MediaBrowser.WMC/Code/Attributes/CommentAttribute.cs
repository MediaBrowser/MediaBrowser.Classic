using System;


namespace MediaBrowser.Attributes {

    [global::System.AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class CommentAttribute : Attribute {

        // This is a positional argument
        public CommentAttribute(string comment) {
            Comment = comment;
        }

        public string Comment { get; private set; }
    }
}

