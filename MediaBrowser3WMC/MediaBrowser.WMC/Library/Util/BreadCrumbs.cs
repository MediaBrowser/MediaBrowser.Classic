using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Util {
    public class BreadCrumbs : IEnumerable<string> {

        struct BreadCrumb {
            public string Name { get; set; }
            public bool Linked { get; set; }
        }

        Stack<BreadCrumb> crumbs = new Stack<BreadCrumb>();

        int maxDisplayCrumbs;

        public BreadCrumbs(int maxDisplayCrumbs) {
            this.maxDisplayCrumbs = maxDisplayCrumbs;
        }

        public void Push(string name) {
            Push(name, false);
        }

        public void Push(string name, bool linked) {
            crumbs.Push(new BreadCrumb() { Name = name, Linked = linked });
        }

        public void Pop() {
            crumbs.Pop();
            while (crumbs.Count > 0 && crumbs.Peek().Linked == true) {
                crumbs.Pop();
            }
        }

        public int Count {
            get {
                return crumbs.Count;
            }
        }

        //ebr - I'm sure sam can come up with a better way to do this, but we need to strip out PINEntry and direct entry crumbs...
        private List<string> displayCrumbs
        {
            get
            {
                string[] nameArray = crumbs.Select(i => i.Name).Reverse().ToArray();
                List<string> displayNames = new List<string>();
                foreach (string i in nameArray)
                {
                    if (i != "PINENTRY" && i != "DIRECTENTRY")
                        displayNames.Add(i);
                }
                return displayNames;
            }
        }

        public override string ToString()
        {
            int max = maxDisplayCrumbs;
            List<string> displCrumbs = displayCrumbs;
            if (displCrumbs.Count < max)
                max = displCrumbs.Count;
            if (max == 0)
                return "";
            return string.Join(" | ",displayCrumbs.ToArray() , displCrumbs.Count - max, max);
        }


        public IEnumerator<string> GetEnumerator() {
            foreach (var item in crumbs) {
                yield return item.Name;
            }
        }


        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

    } 
}
