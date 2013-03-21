using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library {
    public class BaseItemComparer : IComparer<BaseItem> {
        private SortOrder order;
        private string propertyName;
        private StringComparison compareCulture = StringComparison.CurrentCultureIgnoreCase;

        public BaseItemComparer(SortOrder order) {
            this.order = order;
        }

        public BaseItemComparer(SortOrder order, StringComparison compare) {
            this.order = order;
            this.compareCulture = compare;
        }

        public BaseItemComparer(string property) {
            this.order = SortOrder.Custom;
            this.propertyName = property;
        }

        public BaseItemComparer(string property, StringComparison compare) {
            this.order = SortOrder.Custom;
            this.propertyName = property;
            this.compareCulture = compare;
        }

        #region IComparer<BaseItem> Members

        public int Compare(BaseItem x, BaseItem y) {
            int compare = 0;

            switch (order) {

                case SortOrder.Date:
                    compare = -x.DateCreated.CompareTo(y.DateCreated);
                    break;

                case SortOrder.Year:

                    var xProductionYear = x is IShow ? (x as IShow).ProductionYear : null;
                    var yProductionYear = y is IShow ? (y as IShow).ProductionYear : null;

                    xProductionYear = xProductionYear ?? 0;
                    yProductionYear = yProductionYear ?? 0;

                    int orderFactor = Config.Instance.YearSortAsc ? -1 : 1;
                    compare = orderFactor * yProductionYear.Value.CompareTo(xProductionYear.Value); //this will reverse the order if that option is set
                    break;

                case SortOrder.Rating:

                    var xImdbRating = x is IShow ? (x as IShow).ImdbRating : null;
                    var yImdbRating = y is IShow ? (y as IShow).ImdbRating : null;

                    xImdbRating = xImdbRating ?? 0;
                    yImdbRating = yImdbRating ?? 0;

                    compare = yImdbRating.Value.CompareTo(xImdbRating.Value);
                    break;

                case SortOrder.Runtime:
                    var xRuntime = x is IShow ? (x as IShow).RunningTime : null;
                    var yRuntime = y is IShow ? (y as IShow).RunningTime : null;

                    xRuntime = xRuntime ?? int.MaxValue;
                    yRuntime = yRuntime ?? int.MaxValue;

                    compare = xRuntime.Value.CompareTo(yRuntime.Value);
                    break;

                case SortOrder.Unwatched:

                    compare = ExtractUnwatchedCount(y).CompareTo(ExtractUnwatchedCount(x));
                    break;

                case SortOrder.Custom:

                    Logging.Logger.ReportVerbose("Sorting on custom field " + propertyName);
                    var yProp = y.GetType().GetProperty(propertyName);
                    var xProp = x.GetType().GetProperty(propertyName);
                    if (yProp == null || xProp == null) break;
                    var yVal = yProp.GetValue(y, null);
                    var xVal = xProp.GetValue(x,null);
                    if (yVal == null && xVal == null) break;
                    if (yVal == null) return 1;
                    if (xVal == null) return -1;
                    Logging.Logger.ReportVerbose("Value x: " + xVal + " Value y: " + yVal);
                    compare = String.Compare(xVal.ToString(), yVal.ToString(),compareCulture);
                    break;

                default:
                    compare = 0;
                    break;
            }

            if (compare == 0) {

                var name1 = x.SortName ?? x.Name ?? "";
                var name2 = y.SortName ?? y.Name ?? "";

                if (Config.Instance.EnableAlphanumericSorting)
                    compare = AlphaNumericCompare(name1, name2,compareCulture);
                else
                    compare = String.Compare(name1,name2,compareCulture);
            }

            return compare;
        }

        private int ExtractUnwatchedCount(BaseItem item) {
            int count = 0;

            var video = item as Video;
            var folder = item as Folder;

            if (folder != null) {
                if (Kernel.Instance.ConfigData.SortUnwatchedByName) {
                    count = folder.UnwatchedCount > 0 ? 1 : 0;
                } else {
                    count = folder.UnwatchedCount;
                }
            } else if (video != null) {
                count = video.PlaybackStatus.WasPlayed ? 0 : 1;
            }

            return count;
        }

        #endregion

        private static bool NullCompare(object o1, object o2, out int compare) {
            compare = 0;
            if (o1 == null || o2 == null) {
                if (o1 == null && o2 == null) {
                    compare = 0;
                } else if (o1 == null) {
                    compare = -1;
                } else if (o2 == null) {
                    compare = 1;
                }
                return true;
            }
            return false;
        }

        public static int AlphaNumericCompare(string s1, string s2, StringComparison compareCulture) {
            // http://dotnetperls.com/Content/Alphanumeric-Sorting.aspx

            int len1 = s1.Length;
            int len2 = s2.Length;
            int marker1 = 0;
            int marker2 = 0;

            // Walk through two the strings with two markers.
            while (marker1 < len1 && marker2 < len2) {
                char ch1 = s1[marker1];
                char ch2 = s2[marker2];

                // Some buffers we can build up characters in for each chunk.
                char[] space1 = new char[len1];
                int loc1 = 0;
                char[] space2 = new char[len2];
                int loc2 = 0;

                // Walk through all following characters that are digits or
                // characters in BOTH strings starting at the appropriate marker.
                // Collect char arrays.
                do {
                    space1[loc1++] = ch1;
                    marker1++;

                    if (marker1 < len1) {
                        ch1 = s1[marker1];
                    } else {
                        break;
                    }
                } while (char.IsDigit(ch1) == char.IsDigit(space1[0]));

                do {
                    space2[loc2++] = ch2;
                    marker2++;

                    if (marker2 < len2) {
                        ch2 = s2[marker2];
                    } else {
                        break;
                    }
                } while (char.IsDigit(ch2) == char.IsDigit(space2[0]));

                // If we have collected numbers, compare them numerically.
                // Otherwise, if we have strings, compare them alphabetically.
                string str1 = new string(space1);
                string str2 = new string(space2);
                
                int result;

                //biggest int - 2147483647
                if (char.IsDigit(space1[0]) && char.IsDigit(space2[0]) /*&& str1.Length < 10 && str2.Length < 10*/) //this assumed the entire string was a number...
                {
                    try
                    {
                        int thisNumericChunk = int.Parse(str1.Substring(0,str1.Length > 9 ? 10 : str1.Length));
                        int thatNumericChunk = int.Parse(str2.Substring(0,str2.Length > 9 ? 10 : str2.Length));
                        //Logging.Logger.ReportVerbose("Comparing Numbers... " + str1 + "(" + thisNumericChunk + ")/" + str2 + "(" + thatNumericChunk + ")");
                        result = thisNumericChunk.CompareTo(thatNumericChunk);
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.ReportException("Error comparing numeric strings: "+str1+"/"+str2,e);
                        result = String.Compare(str1, str2, compareCulture);
                    }
                    
                } else {
                    //Logging.Logger.ReportVerbose("Comparing Strings... " + str1 + "/" + str2);
                    result = String.Compare(str1,str2,compareCulture);
                }

                if (result != 0) {
                    return result;
                }
            }
            return len1 - len2;
        }
    }
}
