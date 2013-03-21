using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Configurator {
      					

    internal class WinAPI
    {
    // C# representation of the IMalloc interface.
    [InterfaceType ( ComInterfaceType.InterfaceIsIUnknown ),
       Guid ( "00000002-0000-0000-C000-000000000046" )]
    public interface IMalloc
    {
       [PreserveSig] IntPtr Alloc ( [In] int cb );
       [PreserveSig] IntPtr Realloc ( [In] IntPtr pv, [In] int cb );
       [PreserveSig] void   Free ( [In] IntPtr pv );
       [PreserveSig] int    GetSize ( [In] IntPtr pv );
       [PreserveSig] int    DidAlloc ( IntPtr pv );
       [PreserveSig] void   HeapMinimize ( );
    }

    [DllImport("User32.DLL")]
    public static extern IntPtr GetActiveWindow ( );

    public class Shell32
    {
       // Styles used in the BROWSEINFO.ulFlags field.
       [Flags]    
       public enum BffStyles 
       {
          RestrictToFilesystem = 0x0001, // BIF_RETURNONLYFSDIRS
          RestrictToDomain =     0x0002, // BIF_DONTGOBELOWDOMAIN
          RestrictToSubfolders = 0x0008, // BIF_RETURNFSANCESTORS
          ShowTextBox =          0x0010, // BIF_EDITBOX
          ValidateSelection =    0x0020, // BIF_VALIDATE
          NewDialogStyle =       0x0040, // BIF_NEWDIALOGSTYLE
          BrowseForComputer =    0x1000, // BIF_BROWSEFORCOMPUTER
          BrowseForPrinter =     0x2000, // BIF_BROWSEFORPRINTER
          BrowseForEverything =  0x4000, // BIF_BROWSEINCLUDEFILES
       }

       // Delegate type used in BROWSEINFO.lpfn field.
       public delegate int BFFCALLBACK ( IntPtr hwnd, uint uMsg, IntPtr lParam, IntPtr lpData );

       [StructLayout ( LayoutKind.Sequential, Pack=8 )]
          public struct BROWSEINFO
       {
          public IntPtr       hwndOwner;
          public IntPtr       pidlRoot;
          public IntPtr       pszDisplayName;
          [MarshalAs ( UnmanagedType.LPTStr )]
          public string       lpszTitle;
          public int          ulFlags;
          [MarshalAs ( UnmanagedType.FunctionPtr )]
          public BFFCALLBACK  lpfn;
          public IntPtr       lParam;
          public int          iImage;
       }

       [DllImport ( "Shell32.DLL" )]
       public static extern int SHGetMalloc ( out IMalloc ppMalloc );

       [DllImport ( "Shell32.DLL" )]
       public static extern int SHGetSpecialFolderLocation ( 
                   IntPtr hwndOwner, int nFolder, out IntPtr ppidl );

       [DllImport ( "Shell32.DLL" )]
       public static extern int SHGetPathFromIDList ( 
                   IntPtr pidl, StringBuilder Path );

       [DllImport ( "Shell32.DLL", CharSet=CharSet.Auto )]
       public static extern IntPtr SHBrowseForFolder ( ref BROWSEINFO bi );
    }
    }
      					
}
