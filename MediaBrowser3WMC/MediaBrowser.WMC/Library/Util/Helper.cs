using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using MediaBrowser.Interop;
using MediaBrowser.Library;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.Model.Dto;
using Microsoft.MediaCenter.UI;
using Microsoft.Win32;

namespace MediaBrowser.LibraryManagement
{
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Xml;
    using MediaBrowser.Library.Logging;
    using MediaBrowser.Library.Threading;

    public static class Helper
    {
        public const string MY_VIDEOS = "MyVideos";
        static readonly string[] isoExtensions = { "iso", "img" };

        public static Dictionary<string, bool> perceivedTypeCache = new Dictionary<string, bool>();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern PowerSettings.EXECUTION_STATE SetThreadExecutionState(PowerSettings.EXECUTION_STATE esFlags);
        
        public static void PreventSleep()
        {
            SetThreadExecutionState(PowerSettings.EXECUTION_STATE.ES_CONTINUOUS
                                    | PowerSettings.EXECUTION_STATE.ES_DISPLAY_REQUIRED
                                    | PowerSettings.EXECUTION_STATE.ES_SYSTEM_REQUIRED
                                    | PowerSettings.EXECUTION_STATE.ES_AWAYMODE_REQUIRED);
        }
        
        public static void AllowSleep()
        {
            SetThreadExecutionState(PowerSettings.EXECUTION_STATE.ES_CONTINUOUS);
        }

        #region Keyboard Manipulation

        public static void SendKeyLeft()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.LEFT,
                                                                   wVk = VirtualKeyShort.LEFT
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.LEFT,
                                                                   wVk = VirtualKeyShort.LEFT,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        public static void SendKeyRight()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.RIGHT,
                                                                   wVk = VirtualKeyShort.RIGHT
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.RIGHT,
                                                                   wVk = VirtualKeyShort.RIGHT,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        public static void SendKeyUp()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.UP,
                                                                   wVk = VirtualKeyShort.UP
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.UP,
                                                                   wVk = VirtualKeyShort.UP,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        public static void SendKeyDown()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.DOWN,
                                                                   wVk = VirtualKeyShort.DOWN
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.DOWN,
                                                                   wVk = VirtualKeyShort.DOWN,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        public static void SendKeyPageUp()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.PRIOR,
                                                                   wVk = VirtualKeyShort.PRIOR
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.PRIOR,
                                                                   wVk = VirtualKeyShort.PRIOR,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        public static void SendKeyPageDown()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.NEXT,
                                                                   wVk = VirtualKeyShort.NEXT
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.NEXT,
                                                                   wVk = VirtualKeyShort.NEXT,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        public static void SendKeyEnter()
        {
            var pInputs = new[]
                              {
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.RETURN,
                                                                   wVk = VirtualKeyShort.RETURN
                                                               }
                                                  }
                                      },
                                  new INPUT()
                                      {
                                          type = (uint) INPUT_TYPE.INPUT_KEYBOARD,
                                          U = new InputUnion()
                                                  {
                                                      ki = new KEYBDINPUT()
                                                               {
                                                                   wScan = ScanCodeShort.RETURN,
                                                                   wVk = VirtualKeyShort.RETURN,
                                                                   dwFlags = KEYEVENTF.KEYUP
                                                               }
                                                  }
                                      }

                              };

            SendInput((uint)pInputs.Length, pInputs, INPUT.Size);
        }

        /// <summary>
        /// Synthesizes keystrokes, mouse motions, and button clicks.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern uint SendInput(
        uint nInputs,
        [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs,
        int cbSize);

        internal enum INPUT_TYPE : uint
        {
            INPUT_MOUSE = 0,
            INPUT_KEYBOARD = 1,
            INPUT_HARDWARE = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            internal uint type;
            internal InputUnion U;
            internal static int Size
            {
                get { return Marshal.SizeOf(typeof(INPUT)); }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            internal int dx;
            internal int dy;
            internal int mouseData;
            internal MOUSEEVENTF dwFlags;
            internal uint time;
            internal UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            internal VirtualKeyShort wVk;
            internal ScanCodeShort wScan;
            internal KEYEVENTF dwFlags;
            internal int time;
            internal UIntPtr dwExtraInfo;
        }

        [Flags]
        internal enum MOUSEEVENTF : uint
        {
            ABSOLUTE = 0x8000,
            HWHEEL = 0x01000,
            MOVE = 0x0001,
            MOVE_NOCOALESCE = 0x2000,
            LEFTDOWN = 0x0002,
            LEFTUP = 0x0004,
            RIGHTDOWN = 0x0008,
            RIGHTUP = 0x0010,
            MIDDLEDOWN = 0x0020,
            MIDDLEUP = 0x0040,
            VIRTUALDESK = 0x4000,
            WHEEL = 0x0800,
            XDOWN = 0x0080,
            XUP = 0x0100
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            internal MOUSEINPUT mi;
            [FieldOffset(0)]
            internal KEYBDINPUT ki;
            [FieldOffset(0)]
            internal HARDWAREINPUT hi;
        }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        internal int uMsg;
        internal short wParamL;
        internal short wParamH;
    }

            [Flags]
    internal enum KEYEVENTF : uint
    {
        EXTENDEDKEY = 0x0001,
        KEYUP = 0x0002,
        SCANCODE = 0x0008,
        UNICODE = 0x0004
    }

            internal enum ScanCodeShort : short
            {
                LBUTTON = 0,
                RBUTTON = 0,
                CANCEL = 70,
                MBUTTON = 0,
                XBUTTON1 = 0,
                XBUTTON2 = 0,
                BACK = 14,
                TAB = 15,
                CLEAR = 76,
                RETURN = 28,
                SHIFT = 42,
                CONTROL = 29,
                MENU = 56,
                PAUSE = 0,
                CAPITAL = 58,
                KANA = 0,
                HANGUL = 0,
                JUNJA = 0,
                FINAL = 0,
                HANJA = 0,
                KANJI = 0,
                ESCAPE = 1,
                CONVERT = 0,
                NONCONVERT = 0,
                ACCEPT = 0,
                MODECHANGE = 0,
                SPACE = 57,
                PRIOR = 73,
                NEXT = 81,
                END = 79,
                HOME = 71,
                LEFT = 75,
                UP = 72,
                RIGHT = 77,
                DOWN = 80,
                SELECT = 0,
                PRINT = 0,
                EXECUTE = 0,
                SNAPSHOT = 84,
                INSERT = 82,
                DELETE = 83,
                HELP = 99,
                KEY_0 = 11,
                KEY_1 = 2,
                KEY_2 = 3,
                KEY_3 = 4,
                KEY_4 = 5,
                KEY_5 = 6,
                KEY_6 = 7,
                KEY_7 = 8,
                KEY_8 = 9,
                KEY_9 = 10,
                KEY_A = 30,
                KEY_B = 48,
                KEY_C = 46,
                KEY_D = 32,
                KEY_E = 18,
                KEY_F = 33,
                KEY_G = 34,
                KEY_H = 35,
                KEY_I = 23,
                KEY_J = 36,
                KEY_K = 37,
                KEY_L = 38,
                KEY_M = 50,
                KEY_N = 49,
                KEY_O = 24,
                KEY_P = 25,
                KEY_Q = 16,
                KEY_R = 19,
                KEY_S = 31,
                KEY_T = 20,
                KEY_U = 22,
                KEY_V = 47,
                KEY_W = 17,
                KEY_X = 45,
                KEY_Y = 21,
                KEY_Z = 44,
                LWIN = 91,
                RWIN = 92,
                APPS = 93,
                SLEEP = 95,
                NUMPAD0 = 82,
                NUMPAD1 = 79,
                NUMPAD2 = 80,
                NUMPAD3 = 81,
                NUMPAD4 = 75,
                NUMPAD5 = 76,
                NUMPAD6 = 77,
                NUMPAD7 = 71,
                NUMPAD8 = 72,
                NUMPAD9 = 73,
                MULTIPLY = 55,
                ADD = 78,
                SEPARATOR = 0,
                SUBTRACT = 74,
                DECIMAL = 83,
                DIVIDE = 53,
                F1 = 59,
                F2 = 60,
                F3 = 61,
                F4 = 62,
                F5 = 63,
                F6 = 64,
                F7 = 65,
                F8 = 66,
                F9 = 67,
                F10 = 68,
                F11 = 87,
                F12 = 88,
                F13 = 100,
                F14 = 101,
                F15 = 102,
                F16 = 103,
                F17 = 104,
                F18 = 105,
                F19 = 106,
                F20 = 107,
                F21 = 108,
                F22 = 109,
                F23 = 110,
                F24 = 118,
                NUMLOCK = 69,
                SCROLL = 70,
                LSHIFT = 42,
                RSHIFT = 54,
                LCONTROL = 29,
                RCONTROL = 29,
                LMENU = 56,
                RMENU = 56,
                BROWSER_BACK = 106,
                BROWSER_FORWARD = 105,
                BROWSER_REFRESH = 103,
                BROWSER_STOP = 104,
                BROWSER_SEARCH = 101,
                BROWSER_FAVORITES = 102,
                BROWSER_HOME = 50,
                VOLUME_MUTE = 32,
                VOLUME_DOWN = 46,
                VOLUME_UP = 48,
                MEDIA_NEXT_TRACK = 25,
                MEDIA_PREV_TRACK = 16,
                MEDIA_STOP = 36,
                MEDIA_PLAY_PAUSE = 34,
                LAUNCH_MAIL = 108,
                LAUNCH_MEDIA_SELECT = 109,
                LAUNCH_APP1 = 107,
                LAUNCH_APP2 = 33,
                OEM_1 = 39,
                OEM_PLUS = 13,
                OEM_COMMA = 51,
                OEM_MINUS = 12,
                OEM_PERIOD = 52,
                OEM_2 = 53,
                OEM_3 = 41,
                OEM_4 = 26,
                OEM_5 = 43,
                OEM_6 = 27,
                OEM_7 = 40,
                OEM_8 = 0,
                OEM_102 = 86,
                PROCESSKEY = 0,
                PACKET = 0,
                ATTN = 0,
                CRSEL = 0,
                EXSEL = 0,
                EREOF = 93,
                PLAY = 0,
                ZOOM = 98,
                NONAME = 0,
                PA1 = 0,
                OEM_CLEAR = 0,
            }
        internal enum VirtualKeyShort : short
        {
            ///<summary>
            ///ENTER key
            ///</summary>
            RETURN = 0x0D,

            ///<summary>
            ///LEFT ARROW key
            ///</summary>
            LEFT = 0x25,

            ///<summary>
            ///UP ARROW key
            ///</summary>
            UP = 0x26,

            ///<summary>
            ///RIGHT ARROW key
            ///</summary>
            RIGHT = 0x27,

            ///<summary>
            ///DOWN ARROW key
            ///</summary>
            DOWN = 0x28,

            ///<summary>
            ///SELECT key
            ///</summary>
            SELECT = 0x29,
            ///<summary>
            ///PAGE UP key
            ///</summary>
            PRIOR = 0x21,
            ///<summary>
            ///PAGE DOWN key
            ///</summary>
            NEXT = 0x22,
        }

        #endregion


    [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void ActivateMediaCenter()
        {
            System.Diagnostics.Process[] p = System.Diagnostics.Process.GetProcessesByName("ehshell");
            if (p.Length > 0) //found
            {
                SetForegroundWindow(p[0].MainWindowHandle);
            }
            //else not Found -> Do nothing.
        }

        public static bool IsExtenderNativeVideo(string filename)
        {
            string extension = System.IO.Path.GetExtension(filename).ToLower();
            var extensions = Config.Instance.ExtenderNativeTypes.Split(',');
            foreach (var item in extensions)
            {
                if (item == extension)
                {
                    return true;
                }
            }
            return false;
        }

        // Check if this file is an Iso.  (This is not used to determine what files
        // are videos, etc.  It is more used to filter certain cases
        // that are handled differently for Isos).
        public static bool IsIso(string filename)
        {
            string extension = System.IO.Path.GetExtension(filename).ToLower();
            foreach (string e in isoExtensions)
                if (extension == "." + e)
                    return true;
            return false;
        }

        public static List<string> GetIsoFiles(string path)
        {
            List<string> files = new List<string>();
            foreach(string ext in isoExtensions)
                files.AddRange(Directory.GetFiles(path, "*." + ext));
            return files;
        }

        // I left the hardcoded list, cause the failure mode is better, at least it will show
        // videos if the codecs are not installed properly
        public static bool IsVideo(string filename)
        {
            string extension = System.IO.Path.GetExtension(filename).ToLower();

            switch (extension)
            {
                // special case so DVD files are never considered videos and ISOs not
                case ".vob":
                case ".bup":
                case ".ifo":
                case ".iso":
                    return false; 
                case ".rmvb":
                case ".mov":
                case ".avi":
                case ".mpg":
                case ".mpeg":
                case ".wmv":
                case ".mp4":
                case ".mkv":
                case ".divx":
                case ".dvr-ms":
                case ".wtv":
                case ".ogm":
                case ".ogv":
                case ".asf":
                case ".m4v":
                case ".flv":
                case ".f4v":
                case ".3gp":
                    return true;

                default:

                    bool isVideo;
                    lock (perceivedTypeCache)
                    {
                        if (perceivedTypeCache.TryGetValue(extension, out isVideo))
                        {
                            return isVideo;
                        }
                    }

                    string pt = null;
                    RegistryKey key = Registry.ClassesRoot;
                    key = key.OpenSubKey(extension);
                    if (key != null)
                    {
                        pt = key.GetValue("PerceivedType") as string;
                    }
                    if (pt == null) pt = "";
                    pt = pt.ToLower();

                    lock (perceivedTypeCache)
                    {
                        perceivedTypeCache[extension] = (pt == "video");
                    }

                    return perceivedTypeCache[extension];
            }
            
        }

        private static string myVideosPath = null;
        public static string MyVideosPath
        {
            get
            {
                if (myVideosPath == null)
                {
                    // Missing from System.Environment
                    int CSIDL_MYVIDEO = 0xe;

                    StringBuilder lpszPath = new StringBuilder(260);
                    MediaBrowser.Interop.ShellNativeMethods.SHGetFolderPath(IntPtr.Zero, CSIDL_MYVIDEO, IntPtr.Zero, 0, lpszPath);
                    myVideosPath = lpszPath.ToString();
                }
                return myVideosPath;

            }
        }

        public static bool IsVob(String filename)
        {
            string extension = System.IO.Path.GetExtension(filename).ToLower();
            return extension == ".vob";
        }

        public static bool IsIfo(string filename) {
            string extension = System.IO.Path.GetExtension(filename).ToLower();
            return extension == ".ifo";
        }

        public static bool IsShortcut(string filename)
        {
            return System.IO.Path.GetExtension(filename).ToLower() == ".lnk";
        }

        internal static bool IsVirtualFolder(string filename)
        {
            return System.IO.Path.GetExtension(filename).ToLower() == ".vf";
        }

        public static string ResolveShortcut(string filename)
        {
            return MediaBrowser.Interop.ShortcutNativeMethods.ResolveShortcut(filename);
        }

        public static bool ContainsFile(string path, string filter)
        {
            if (Directory.Exists(path))
                return Directory.GetFiles(path, filter).Length > 0;
            else
                return false;
        }

        public static bool IsFolder(FileSystemInfo fsi)
        {
            return ((fsi.Attributes & FileAttributes.Directory) == FileAttributes.Directory);
        }

        public static bool IsFolder(string path)
        {
            //faster than directory.exists?
            try
            {
                return (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="files">A pre obtained list of the files in the path folder if available, else null</param>
        /// <param name="folders">A pre obtained list of folders in the path folder if available, else null</param>
        /// <returns></returns>
        public static bool IsDvDFolder(string path,string[] files, string[] folders)
        {
            if (files == null)
                files = Directory.GetFiles(path);
            foreach (string f in files)
                if ((f.Length > 4) && (f.Substring(f.Length - 4).ToLower() == ".vob"))
                    return true;
            if (folders == null)
                folders = Directory.GetDirectories(path);
            foreach (string f in folders)
                if (f.ToUpper().EndsWith("VIDEO_TS"))
                    return true;
            return false;
        }

        public static bool IsBluRayFolder(string path, string[] folders)
        {
            if (folders == null)
                folders = Directory.GetDirectories(path);
            foreach (string f in folders)
                if (f.ToUpper().EndsWith("BDMV"))
                    return true;
            return false;
        }

        public static bool IsHDDVDFolder(string path,  string[] folders)
        {
            if (folders == null)
                folders = Directory.GetDirectories(path);
            foreach (string f in folders)
                if (f.ToUpper().EndsWith("HVDVD_TS"))
                    return true;
            return false; 
        }

        public static int IsoCount(string path, string[] files)
        {
            if (files == null)
            {
                if (Directory.Exists(path))
                {
                    return GetIsoFiles(path).Count;
                }
                else
                    return 0;
            }
            else
            {
                int i = 0;
                foreach (string f in files)
                    if (f.Length > 4)
                    {
                        string ext = f.Substring(f.Length - 4).ToLower();
                        foreach(string e in isoExtensions)
                            if (ext == "." + e)
                            {
                                i++;
                                break;
                            }
                    }
                return i;
            }  
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="files">A pre obtained list of the files in the path folder if available, else null</param>
        /// <param name="folders">A pre obtained list of folders in the path folder if available, else null</param>
        /// <returns></returns>
        public static bool ContainsNestedDvdOrIso(string path, string[] files, string[] folders)
        {
            if (files == null)
                files = Directory.GetFiles(path);
            if (IsoCount(path, files) > 0)
                return true;
            if (folders == null)
                folders = Directory.GetDirectories(path);
            if (IsDvDFolder(path, files, folders))
                return true;
            if (IsBluRayFolder(path,  folders))
                return true;
            if (IsHDDVDFolder(path, folders))
                return true;
            
            foreach (string f in folders)
            {
                if (ContainsNestedDvdOrIso(f, null,null))
                    return true;
            }
            return false;  
        }


        static Regex commentExpression = new Regex(@"(\[[^\]]*\])");
        public static string RemoveCommentsFromName(string name)
        {
            return name == null ? null : commentExpression.Replace(name, "");
        }

        internal static bool HasNoAutoPlaylistFile(string path, string[] files)
        {
            foreach (string file in files)
                if (file.ToLower().EndsWith("noautoplaylist"))
                    return true;
            return false;
        }

        public static bool IsAlphaNumeric(string str)
        {
            return (str.ToCharArray().All(c => Char.IsLetter(c) || Char.IsNumber(c)));
        }

        public static string RemoveInvalidFileChars(string filename) {

            if (filename == null) return "";
            var cleanName = new StringBuilder();
            foreach (var letter in filename) {
                if (!System.IO.Path.GetInvalidFileNameChars().Contains(letter)) {
                    cleanName.Append(letter);
                }
            }
            return cleanName.ToString();

        }

        public static int DaysAgo(DateTime date)
        {
            var daysAgo = DateTime.Now.DayOfYear - date.DayOfYear;
            if (daysAgo < 0)
            {
                //crossed years
                daysAgo = (365 - date.DayOfYear) + DateTime.Now.DayOfYear - 1;
            }
            return daysAgo;
        }

        public static string TicksToFriendlyTime(long ticks)
        {
            var pos = new TimeSpan(ticks);
            return pos.Hours > 0 ? string.Format("{0}:{1}:{2}", pos.Hours, pos.Minutes.ToString("00"), pos.Seconds.ToString("00"))
                : string.Format("{0}:{1}", pos.Minutes.ToString("00"), pos.Seconds.ToString("00"));
        }

        public static string FriendlyDateStr(DateTime date)
        {
            if (date != DateTime.MinValue)
            {
                var daysAgo = DaysAgo(date);
                if (daysAgo <= 8)
                {
                    return (daysAgo > 9000 ? " never" : daysAgo == 0 ? " today" : daysAgo == 1 ? " yesterday" : " " + daysAgo.ToString("#0") + " days ago");
                }
                else
                {
                    return " " + date.ToShortDateString();
                }
            }
            else
            {
                return " never";
            }

        }

        /// <summary>
        /// Fetch an XmlDocument
        /// </summary>
        /// <param name="url"></param>
        /// <returns>document on success, null on failure</returns>
        public static XmlDocument Fetch(string url) {
            try {

                int attempt = 0;
                while (attempt < 2) {
                    attempt++;
                    try {
                        var req = (HttpWebRequest)HttpWebRequest.Create(url);
                        req.Timeout = 15000;
                        req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                        req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                        using (WebResponse resp = req.GetResponse())
                            try
                            {
                                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                                {
                                    XmlDocument doc = new XmlDocument();
                                    // this makes it a bit easier to debug.
                                    string payload = reader.ReadToEnd();
                                    doc.LoadXml(payload);
                                    return doc;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.ReportException("Error getting xml from plugin source",ex);
                            }
                            finally
                            {
                                resp.Close();
                                GC.Collect();  //forcing a collection here appears to solve the issue with timeouts on secondary sources
                                               //which probably means there is another issue here, but I cannot find it - tried closing
                                               //everything, 'using' clauses and this is the only thing that seems to work. -ebr
                            }
                    } catch (WebException ex) {
                        Logger.ReportWarning("Error requesting: " + url + "\n" + ex.ToString());
                    } catch (IOException ex) {
                        Logger.ReportWarning("Error requesting: " + url + "\n" + ex.ToString());
                    }
                        
                }
            } catch (Exception ex) {
                Logger.ReportWarning("Failed to fetch url: " + url + "\n" + ex.ToString());
            }

            return null;
        }

        /// <summary>
        /// Fetch json from an url
        /// </summary>
        /// <param name="url"></param>
        /// <returns>json string on success, null on failure</returns>
        public static string FetchJson(string url) {
            try
            {
                //Logger.ReportVerbose("Requesting json from: " + url);
                int attempt = 0;
                while (attempt < 2)
                {
                    attempt++;
                    try
                    {
                        using (WebClient client = new WebClient())
                        {
                            client.Headers.Add("Accept", "application/json");
                            client.Headers.Add("AcceptEncoding", "gzip,deflate");
                            
                            client.Encoding = Encoding.UTF8;

                            string payload = client.DownloadString(url);
                            return payload;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ReportException("Error getting json response from "+url, ex);
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.ReportWarning("Failed to fetch url: " + url + "\n" + ex.ToString());
            }

            return null;
        }

        /// <summary>
        /// Ping an url
        /// </summary>
        /// <param name="path"></param>
        public static void Ping(string path)
        {
            MediaBrowser.Library.Threading.Async.Queue(Async.ThreadPoolName.Ping, () =>
            {
                try
                {
                    WebRequest request = WebRequest.Create(path);
                    var response = request.GetResponse();
                    response.Close();
                }
                catch
                {

                }
            });
        }

        public static Dictionary<string, object> ToJsonDict(string json)
        {
            return json != null ? new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json) : null;
        }

        public static string GetNameFromFile(string filename)
        {
            string temp;
            string fn;
            //first, if the specified name is a file system folder, it probably doesn't have an extention so use the whole name
            if (System.IO.Directory.Exists(filename))
                fn = System.IO.Path.GetFileName(filename);
            else
                fn = System.IO.Path.GetFileNameWithoutExtension(filename);

            //now - strip out anything inside brackets
            temp = GetStringInBetween("[", "]", fn, true, true)[0];
            while (temp.Length > 0)
            {
                fn = fn.Replace(temp, "");
                temp = GetStringInBetween("[", "]", fn, true, true)[0];
            }
            return fn;
        }

        public static string GetAttributeFromPath(string path, string attrib)
        {
            string ret = null;
            if (path.ToLower().Contains(attrib.ToLower()))
            {
                string search = "[" + attrib.ToLower() + "=";
                int start = path.ToLower().IndexOf(search) + search.Length;
                int end = path.IndexOf("]", start);
                ret = path.Substring(start, end - start).ToLower();
            }
            return ret;
        }

        public static bool DontFetchMeta(string path)
        {
            return path != null ? path.ToLower().Contains("[dontfetchmeta]") : false;
        }

        public static string[] GetStringInBetween(string strBegin,
            string strEnd, string strSource,
            bool includeBegin, bool includeEnd)
        {
            string[] result = { "", "" };
            int iIndexOfBegin = strSource.IndexOf(strBegin);
            if (iIndexOfBegin != -1)
            {
                // include the Begin string if desired 
                if (includeBegin)
                    iIndexOfBegin -= strBegin.Length;
                strSource = strSource.Substring(iIndexOfBegin
                    + strBegin.Length);
                int iEnd = strSource.IndexOf(strEnd);
                if (iEnd != -1)
                {
                    // include the End string if desired 
                    if (includeEnd)
                        iEnd += strEnd.Length;
                    result[0] = strSource.Substring(0, iEnd);
                    // advance beyond this segment 
                    if (iEnd + strEnd.Length < strSource.Length)
                        result[1] = strSource.Substring(iEnd
                            + strEnd.Length);
                }
            }
            else
                // stay where we are 
                result[1] = strSource;
            return result;
        }

        /// <summary>
        /// Compiled regular expression for performance.
        /// </summary>
        static readonly Regex HtmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

        /// <summary>
        /// Remove HTML from string with compiled Regex.
        /// </summary>
        public static string StripTags(string source)
        {
            return HtmlRegex.Replace(source, string.Empty);
        }

        private static HashSet<string> _serverMediaInfoImages;
        public static HashSet<string> ServerMediaInfoImages
        {
            get { return _serverMediaInfoImages ?? (_serverMediaInfoImages = GetServerMediaInfoImages()); }
        }

        private static HashSet<string> GetServerMediaInfoImages()
        {
            var hash = new HashSet<string>();
            var info = Kernel.ApiClient.GetMediaInfoImages();
            if (info != null)
            {
                //Logger.ReportVerbose("********** MI Images: {0}", info.Length);
                foreach (var image in info)
                {
                    //Logger.ReportVerbose("********* MI Image: {0} / {1}", image.Theme ?? "null", image.Name ?? "null");
                    hash.Add((image.Theme ?? "all").ToLower() + image.Name.ToLower());
                }
            }
            return hash;
        }

        public static Image GetMediaInfoImage(string name)
        {
            if (name.EndsWith("_")) return null; //blank codec or other type
            name = name.ToLower().Replace("-", "_");
            name = name.Replace('/', '-');
            var themeName = Config.Instance.ViewTheme.ToLower();
            var id = "MiImage" + themeName + name;

            //try to load from image cache first
            var path = CustomImageCache.Instance.GetImagePath(id, true);
            if (path != null) return new Image(path); //was already cached

            //not cached - get it from the server if it is there
            if (ServerMediaInfoImages.Contains(themeName+name) || ServerMediaInfoImages.Contains("all"+name))
            {
                path = Kernel.ApiClient.GetMediaInfoImageUrl(name, Config.Instance.ViewTheme, new ImageOptions());
                var serverImage = new RemoteImage { Path = path};

                try
                {
                    var image = serverImage.DownloadImage();
                    Logger.ReportVerbose("===CustomImage " + path + " being cached on first access.  Shouldn't have to do this again...");
                    //cache it and return resulting cached image
                    return new Image("file://" + CustomImageCache.Instance.CacheImage(serverImage.Path, image));
                }
                catch (WebException)
                {
                    return GetImageFromResources(name, id);
                }
                
            }
            else
            {
                try
                {
                    return GetImageFromResources(name, id);
                }
                catch (Exception e)
                {
                    Logger.ReportException("Unable to get resource",e);
                    return null;
                }
            }
        }

        public static Image GetImageFromResources(string name, string id)
        {
            //not there, get it from resources in default or the current theme if it exists
            var resourceRef = Application.CurrentInstance.CurrentTheme.PageArea.Substring(0, Application.CurrentInstance.CurrentTheme.PageArea.LastIndexOf("/") + 1);
            //Logger.ReportInfo("============== Current Theme: " + Application.CurrentInstance.CurrentTheme.Name);
            var assembly = Kernel.Instance.FindPluginAssembly(Application.CurrentInstance.CurrentTheme.Name);
            if (assembly != null)
            {
                //Logger.ReportInfo("============== Found Assembly. ");
                var rman = new ResourceManager(Application.CurrentInstance.CurrentTheme.Name+".Resources", assembly);
                if (rman.GetObject(name) == null)
                {
                    Logger.ReportVerbose("Could not find resource '{0}' in theme {1}", name, Application.CurrentInstance.CurrentTheme.Name);
                    resourceRef = "resx://MediaBrowser/MediaBrowser.Resources/";
                }
            }
            //cache it
            Logger.ReportVerbose("===CustomImage " + resourceRef + name + " being cached on first access.  Should only have to do this once per session...");
            CustomImageCache.Instance.CacheResource(id, resourceRef + name);
            return new Image(resourceRef + name);

        }

        public static string FirstCap(string aStr)
        {
            string first = aStr.Substring(0, 1);
            string theRest = aStr.Substring(1);
            return first.ToUpper() + theRest.ToLower();
        }

        private static readonly char[] Digits = "0123456789".ToArray();
        public static string FirstCharOrDefault(string str, bool capitalize = true)
        {
            if (string.IsNullOrEmpty(str)) return "<Unknown>";
            var val = Digits.Contains(str[0]) ? "0" : str.Substring(0, 1);
            return capitalize ? val.ToUpper() : val;
        }

        /// <summary>
        /// Returns MAC Address from first Network Card in Computer
        /// </summary>
        /// <returns>[string] MAC Address</returns>
        public static string GetMACAddress()
        {
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = mc.GetInstances();
            string MACAddress = String.Empty;
            foreach (ManagementObject mo in moc)
            {
                if (MACAddress == String.Empty)  // only return MAC Address from first card
                {
                    try
                    {
                        if ((bool)mo["IPEnabled"] == true) MACAddress = mo["MacAddress"].ToString();
                    }
                    catch
                    {
                        mo.Dispose();
                        return Guid.NewGuid().ToString("N");
                    }
                }
                mo.Dispose();
            }
            MACAddress = MACAddress.Replace(":", "");
            return !string.IsNullOrEmpty(MACAddress) ? MACAddress : Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Get the machine's physical memory in GB
        /// </summary>
        /// <returns></returns>
        public static double GetPhysicalMemory()
        {
            double totalCapacity = 0;
            ObjectQuery objectQuery = new ObjectQuery("select * from Win32_PhysicalMemory");
            ManagementObjectSearcher searcher = new
            ManagementObjectSearcher(objectQuery);
            ManagementObjectCollection vals = searcher.Get();

            foreach (ManagementObject val in vals)
            {
                totalCapacity += System.Convert.ToDouble(val.GetPropertyValue("Capacity"));
            }
            return totalCapacity / 1073741824;
        }
    
        /// <summary>
        /// Try to wake the specified machine
        /// </summary>
        /// <param name="mac">Should be in format xx-xx-xx-xx-xx-xx</param>
        public static void WakeMachine(string mac)
        {
            Logger.ReportVerbose("Attempting to wake server at address {0}", mac);
            try
            {
                var client = new UdpClient();
                client.Connect(IPAddress.Broadcast,  7); 
                var counter = 0;
                //buffer to be sent
                var bytes = new byte[1024];   // more than enough :-)
                //first 6 bytes should be 0xFF
                for (var y = 0; y < 6; y++)
                    bytes[counter++] = 0xFF;
                //now repeate MAC 16 times
                for (var y = 0; y < 16; y++)
                {
                    for (var i = 0; i < mac.Length; i += 3)
                    {
                        bytes[counter++] =
                            byte.Parse(mac.Substring(i, 2),
                            NumberStyles.HexNumber);
                    }
                }

                //now send wake up packet
                client.Send(bytes, 1024);
                Logger.ReportVerbose("Wake command sent");
            }
            catch (Exception e)
            {
                Logger.ReportException("Error attempting to wake last server",e);
            }
        }

        public static int SystemIdleTime
        {
            get
            {
                // Get the system uptime
                int systemUptime = Environment.TickCount;

                // The tick at which the last input was recorded
                int LastInputTicks = 0;

                // The number of ticks that passed since last input
                int IdleTicks = 0;

                // Set the struct
                ShellNativeMethods.LASTINPUTINFO LastInputInfo = new ShellNativeMethods.LASTINPUTINFO();

                LastInputInfo.cbSize = (uint)Marshal.SizeOf(LastInputInfo);

                LastInputInfo.dwTime = 0;



                // If we have a value from the function

                if (ShellNativeMethods.GetLastInputInfo(ref LastInputInfo))
                {
                    // Get the number of ticks at the point when the last activity was seen
                    LastInputTicks = (int)LastInputInfo.dwTime;
                    // Number of idle ticks = system uptime ticks - number of ticks at last input
                    IdleTicks = systemUptime - LastInputTicks;

                }
                return IdleTicks;
            }
        }

        /// <summary>
        /// check the availability of the location and wait for it to be valid up to the timeout (BLOCKING)
        /// </summary>
        /// <param name="location">the location to check (assumed to be a network location)</param>
        /// <param name="timeout">milliseconds to wait if not avail</param>
        public static bool WaitForLocation(string location, int timeout)
        {
            double elapsed = 0;
            string dir = Path.HasExtension(location) ? Path.GetDirectoryName(location) : location; //try and be sure it is a directory (will give parent if already directory)
            DateTime started = DateTime.Now;
            while (elapsed < timeout && !Directory.Exists(dir))
            {
                Logger.ReportInfo("Unable to access location: " + dir + ". Waiting "+timeout+"ms for it to be available..."+elapsed.ToString("0")+"ms so far...");
                System.Threading.Thread.Sleep(1000);
                var now = DateTime.Now;
                elapsed = (now - started).TotalMilliseconds;
            }
            if (elapsed >= timeout) Logger.ReportWarning("Timed out attempting to access " + dir);
            return elapsed < timeout;
        }

        /// <summary>
        /// Quick and dirty method to parse an INI file into a NameValueCollection
        /// </summary>
        public static NameValueCollection ParseIniFile(string path)
        {
            NameValueCollection values = new NameValueCollection();

            foreach (string line in File.ReadAllLines(path))
            {
                string[] data = line.Split('=');

                if (data.Length < 2) continue;

                string key = data[0];
                string value;

                if (data.Length == 2)
                {
                    value = data[1];
                }
                else
                {
                    value = string.Join(string.Empty, data, 1, data.Length - 1);
                }

                values[key] = value;
            }

            return values;
        }

        /// <summary>
        /// Sets values into an ini file. The value names are expected to already be there
        /// </summary>
        public static void SetIniFileValues(string path, Dictionary<string, object> values)
        {
            File.WriteAllLines(path, File.ReadAllLines(path).Select(line =>
            {
                string[] data = line.Split('=');

                if (data.Length < 2)
                {
                    return line;
                }

                string key = data[0];

                if (!values.ContainsKey(key))
                {
                    return line;
                }

                return key + "=" + values[key];

            }).ToArray());
        }
    }
}
