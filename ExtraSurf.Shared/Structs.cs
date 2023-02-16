using System;
using System.Runtime.InteropServices;

namespace ExtraSurf.Shared
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SongFftDataSums
    {
        public IntPtr IdentifierPtr { get; set; }

        public long DataLength { get; set; }

        public IntPtr DataPtr { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SongFftDataFull
    {
        public IntPtr IdentifierPtr { get; set; }

        public long FullDataLength { get; set; }

        public long DataIndex { get; set; }

        public IntPtr DataPtr { get; set; }
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SongData
    {
        public IntPtr IdentifierPtr { get; set; }

        public long DataLength { get; set; }

        public IntPtr DataPtr { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SongInfo
    {
        public IntPtr IdentifierPtr { get; set; }
        
        public IntPtr TitlePtr { get; set; }

        public IntPtr ArtistPtr { get; set; }

        public float Duration { get; set; }

        public IntPtr PathPtr { get; set; }
        
        public IntPtr ImageUrlPtr { get; set; }

        public long ImageBytesDataLength { get; set; }

        public IntPtr ImageBytesDataPtr { get; set; }
    }
}