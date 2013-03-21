using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.UserInput
{
    /// <summary>
    /// Provides a basic low-level keyboard listener
    /// Inspired by http://blogs.msdn.com/b/toub/archive/2006/05/03/589423.aspx
    /// Use the KeyDown event to listen for keys.
    /// Make sure to detach from the event when not needed.
    /// </summary>
    public class KeyboardListener
    {
        #region Instance
        private static KeyboardListener _Instance;
        public static KeyboardListener Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new KeyboardListener();
                }

                return _Instance;
            }
        }
        #endregion

        #region KeyDown EventHandler
        volatile EventHandler<KeyEventArgs> _KeyDown;
        /// <summary>
        /// Fires whenever CurrentItem changes
        /// </summary>
        public event EventHandler<KeyEventArgs> KeyDown
        {
            add
            {
                if (_KeyDown == null)
                {
                    // Need to attach/detach on the UI
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                    {
                        StartListening();
                    });
                } 
                
                _KeyDown += value;
            }
            remove
            {
                _KeyDown -= value;

                if (_KeyDown == null && _hookID != IntPtr.Zero)
                {
                    // Need to attach/detach on the UI
                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                    {
                        StopListening();
                    });                     
                }
            }
        }

        private void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = false;

            if (_KeyDown != null)
            {
                // For now, don't async this
                // This will give listeners a chance to modify SuppressKeyPress if they want
                try
                {
                    _KeyDown(this, e);
                }
                catch (Exception ex)
                {
                    Logger.ReportException("KeyDown event listener had an error: ", ex);
                }
            }
        }
        #endregion

        private const int WH_SHELL = 10;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;

        private static void StartListening()
        {
            Logger.ReportVerbose("Attaching low-level keyboard hook");
            _hookID = SetHook(_proc);
        }

        private void StopListening()
        {
            Logger.ReportVerbose("Detaching low-level keyboard hook");
            
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            bool suppressKeyPress = false;

            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    Keys keyData = (Keys)vkCode;

                    KeyEventArgs e = new KeyEventArgs(keyData);

                    KeyboardListener.Instance.OnKeyDown(e);

                    suppressKeyPress = e.SuppressKeyPress;
                }
            }

            if (suppressKeyPress)
            {
                return IntPtr.Zero;
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        #region Imports
        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}
