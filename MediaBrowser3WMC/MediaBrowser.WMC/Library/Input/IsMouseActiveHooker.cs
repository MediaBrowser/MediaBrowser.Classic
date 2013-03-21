using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics; 
using System.Text;

namespace MediaBrowser.Library.Input

    //class borrowed from OML - thx MSBob
{
    /// <summary> 
    /// simple boolean eventargs for our mouseactive hooker 
    /// </summary> 
    public class MouseActiveEventArgs : EventArgs
    {
        private bool mouseActive;
        /// <summary> 
        /// is the mouse active in the mce window 
        /// </summary> 
        public bool MouseActive
        {
            set
            {
                mouseActive = value;
            }
            get
            {
                return this.mouseActive;
            }
        }
    }

    /// <summary> 
    /// replicates the functionality of the Environment.IsMouseActive prop in MCE 
    /// </summary> 
    public class IsMouseActiveHooker : IDisposable
    {
        public event MouseActiveHandler MouseActive;
        public delegate void MouseActiveHandler(IsMouseActiveHooker m, MouseActiveEventArgs e);

        private static event TickHandler Tick;
        private delegate void TickHandler(object o, MouseActiveEventArgs e);

        private System.Timers.Timer mouseMoveTimer;

        public IsMouseActiveHooker()
        {
            this.mouseMoveTimer = new System.Timers.Timer();
            this.mouseMoveTimer.Elapsed += new System.Timers.ElapsedEventHandler(mouseMoveTimer_Elapsed);
            this.mouseMoveTimer.Interval = 5000;
            this.mouseMoveTimer.AutoReset = false;
            _mouseHookID = SetMouseHook(_proc);
            _kbHookID = SetKBHook(_proc);
            Tick += new TickHandler(IsMouseActiveHooker_Tick);
        }

        void mouseMoveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (MouseActive != null)
            {
                MouseActiveEventArgs TOT = new MouseActiveEventArgs();
                TOT.MouseActive = false;
            }
        }

        void IsMouseActiveHooker_Tick(object o, MouseActiveEventArgs e)
        {
            this.mouseMoveTimer.Stop();
            if (e.MouseActive == true)
            {
                this.mouseMoveTimer.Interval = 5000;
                if (MouseActive != null)
                {
                    MouseActiveEventArgs TOT = new MouseActiveEventArgs();
                    TOT.MouseActive = true;
                    MouseActive(this, TOT);
                }
                this.mouseMoveTimer.Start();
            }
            else
            {
                if (MouseActive != null)
                {
                    MouseActiveEventArgs TOT = new MouseActiveEventArgs();
                    TOT.MouseActive = false;
                    MouseActive(this, TOT);
                }
            }

        }

        /// <summary>Gets a reference to the Process instance for the running ehshell.exe</summary> 
        private Process GetEhShellProcess()
        {
            // Get the current terminal services session ID 
            int currentSessionId;
            using (Process currentProcess = Process.GetCurrentProcess()) currentSessionId = currentProcess.SessionId;

            // Get all ehome processes on the machine, and find the one in the current session 
            Process[] procs = Process.GetProcessesByName("ehshell");
            Process ehshell = null;
            for (int i = 0; i < procs.Length; i++)
            {
                if (ehshell == null && procs[i].SessionId == currentSessionId) ehshell = procs[i];
                else procs[i].Dispose();
            }
            return ehshell;
        }

        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _mouseHookID = IntPtr.Zero;
        private static IntPtr _kbHookID = IntPtr.Zero;

        private static IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr SetKBHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
            {
                if (Tick != null)
                {
                    MouseActiveEventArgs args = new MouseActiveEventArgs();
                    args.MouseActive = true;
                    Tick(null, args);
                }
            }

            //if the kb is pressed we clear the mousemovement 
            else if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                if (Tick != null)
                {
                    MouseActiveEventArgs args = new MouseActiveEventArgs();
                    args.MouseActive = false;
                    Tick(null, args);
                }
            }
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #region IDisposable Members

        public void Dispose()
        {
            UnhookWindowsHookEx(_mouseHookID);
            UnhookWindowsHookEx(_kbHookID);
        }

        #endregion
    }
}