using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics; 
using System.Text;

namespace MediaBrowser.Library.Input

    //class borrowed from OML - thx MSBob
    // re-worked for MB by ebr
{
    /// <summary> 
    /// simple boolean eventargs for our mouseactive hooker 
    /// </summary> 
    public class MouseActiveEventArgs : EventArgs
    {
        /// <summary> 
        /// is the mouse active in the mce window 
        /// </summary> 
        public bool MouseActive { get; set; }
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

        public IsMouseActiveHooker()
        {
            _mouseHookID = SetMouseHook(_proc);
            Tick += IsMouseActiveHooker_Tick;
        }

        void IsMouseActiveHooker_Tick(object o, MouseActiveEventArgs e)
        {
            if (e.MouseActive)
            {
                if (MouseActive != null)
                {
                    var args = new MouseActiveEventArgs {MouseActive = true};
                    MouseActive(this, args);
                }
            }
            else
            {
                if (MouseActive != null)
                {
                    var args = new MouseActiveEventArgs {MouseActive = false};
                    MouseActive(this, args);
                }
            }

        }

        /// <summary>Gets a reference to the Process instance for the running ehshell.exe</summary> 
        private static Process GetEhShellProcess()
        {
            // Get the current terminal services session ID 
            int currentSessionId;
            using (Process currentProcess = Process.GetCurrentProcess()) currentSessionId = currentProcess.SessionId;

            // Get all ehome processes on the machine, and find the one in the current session 
            var procs = Process.GetProcessesByName("ehshell");
            Process ehshell = null;
            foreach (var proc in procs)
            {
                if (ehshell == null && proc.SessionId == currentSessionId) ehshell = proc;
                else proc.Dispose();
            }
            return ehshell;
        }

        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _mouseHookID = IntPtr.Zero;

        private static IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = GetEhShellProcess() ?? Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
            {
                if (Tick != null)
                {
                    var args = new MouseActiveEventArgs {MouseActive = true};
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
        }

        #endregion
    }
}