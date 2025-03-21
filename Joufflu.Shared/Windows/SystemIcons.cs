﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Joufflu.Shared.Windows
{
    public class SystemIcons
    {
        public static class Interop
        {
            /// <summary>Maximal Length of unmanaged Windows-Path-strings</summary>
            private const int MAX_PATH = 260;
            /// <summary>Maximal Length of unmanaged Typename</summary>
            private const int MAX_TYPE = 80;

            public enum FileAttribute
            {
                FILE_ATTRIBUTE_DIRECTORY = 0x10,
                FILE_ATTRIBUTE_NORMAL = 0x80
            }

            [Flags]
            public enum SHGFI : int
            {
                /// <summary>get icon</summary>
                Icon = 0x000000100,
                /// <summary>get display name</summary>
                DisplayName = 0x000000200,
                /// <summary>get type name</summary>
                TypeName = 0x000000400,
                /// <summary>get attributes</summary>
                Attributes = 0x000000800,
                /// <summary>get icon location</summary>
                IconLocation = 0x000001000,
                /// <summary>return exe type</summary>
                ExeType = 0x000002000,
                /// <summary>get system icon index</summary>
                SysIconIndex = 0x000004000,
                /// <summary>put a link overlay on icon</summary>
                LinkOverlay = 0x000008000,
                /// <summary>show icon in selected state</summary>
                Selected = 0x000010000,
                /// <summary>get only specified attributes</summary>
                Attr_Specified = 0x000020000,
                /// <summary>get large icon</summary>
                LargeIcon = 0x000000000,
                /// <summary>get small icon</summary>
                SmallIcon = 0x000000001,
                /// <summary>get open icon</summary>
                OpenIcon = 0x000000002,
                /// <summary>get shell size icon</summary>
                ShellIconSize = 0x000000004,
                /// <summary>pszPath is a pidl</summary>
                PIDL = 0x000000008,
                /// <summary>use passed dwFileAttribute</summary>
                UseFileAttributes = 0x000000010,
                /// <summary>apply the appropriate overlays</summary>
                AddOverlays = 0x000000020,
                /// <summary>Get the index of the overlay in the upper 8 bits of the iIcon</summary>
                OverlayIndex = 0x000000040,
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct SHFILEINFO
            {
                public SHFILEINFO(bool b)
                {
                    hIcon = IntPtr.Zero;
                    iIcon = 0;
                    dwAttributes = 0;
                    szDisplayName = "";
                    szTypeName = "";
                }
                public IntPtr hIcon;
                public int iIcon;
                public uint dwAttributes;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
                public string szDisplayName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_TYPE)]
                public string szTypeName;
            };

            [DllImport("shell32.dll", CharSet = CharSet.Auto)]
            public static extern int SHGetFileInfo(
              string pszPath,
              int dwFileAttributes,
              out SHFILEINFO psfi,
              uint cbfileInfo,
              SHGFI uFlags);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyIcon(IntPtr hIcon);
        }

        /// <summary>
        /// Récupération de l'icone d'un fichier ou dossier
        /// </summary>
        /// <param name="strPath">Chemin du fichier dont ont veut l'icone</param>
        /// <param name="bSmall">Taille de l'iconne</param>
        /// <returns>Une ImageSource contenant l'icone ou null si le fichier n'existe pas</returns>
        public static ImageSource? GetIcon(string strPath, bool bSmall)
        {
            Interop.FileAttribute fileAttribute;

            // Cas de fichiers temporaires créer et supprimé immédiatement
            if (!File.Exists(strPath) && !Directory.Exists(strPath))
                return null;

            if (File.GetAttributes(strPath).HasFlag(FileAttributes.Directory))
                fileAttribute = Interop.FileAttribute.FILE_ATTRIBUTE_DIRECTORY;
            else
                fileAttribute = Interop.FileAttribute.FILE_ATTRIBUTE_NORMAL;

            Interop.SHFILEINFO info = new Interop.SHFILEINFO(true);
            int cbFileInfo = Marshal.SizeOf(info);
            Interop.SHGFI flags;
            if (bSmall)
                flags = Interop.SHGFI.Icon | Interop.SHGFI.SmallIcon | Interop.SHGFI.UseFileAttributes;
            else
                flags = Interop.SHGFI.Icon | Interop.SHGFI.LargeIcon | Interop.SHGFI.UseFileAttributes;

            Interop.SHGetFileInfo(strPath, (int)fileAttribute, out info, (uint)cbFileInfo, flags);

            IntPtr iconHandle = info.hIcon;
            //if (IntPtr.Zero == iconHandle) // not needed, always return icon (blank)
            //  return DefaultImgSrc;
            ImageSource img = Imaging.CreateBitmapSourceFromHIcon(
                        iconHandle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
            Interop.DestroyIcon(iconHandle);
            return img;
        }
    }
}
