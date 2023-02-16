using System;
using System.Runtime.InteropServices;
using ExtraSurf.Shared;

namespace ExtraSurf.Shared
{
    public class Song
    {
        public string Identifier { get; set; }
        
        public string Title { get; set; }
        
        public string Artist { get; set; }
        
        public float Duration { get; set; }
        
        public string Path { get; set; }
        
        public string ImageUrl { get; set; }
        
        public byte[] ImageBytes { get; set; }
    }
}

public static class SongExtensions
{
    public static Song SongStructToEntity(this SongInfo songInfo)
    {
        var song = new Song()
        {
            Identifier = Marshal.PtrToStringUni(songInfo.IdentifierPtr),
            Title = Marshal.PtrToStringUni(songInfo.TitlePtr),
            Artist = Marshal.PtrToStringUni(songInfo.ArtistPtr),
            Duration = songInfo.Duration,
            Path = Marshal.PtrToStringUni(songInfo.PathPtr),
            ImageUrl = songInfo.ImageUrlPtr != IntPtr.Zero ? Marshal.PtrToStringUni(songInfo.ImageUrlPtr) : null,
            ImageBytes = songInfo.ImageBytesDataPtr != IntPtr.Zero ? new byte[songInfo.ImageBytesDataLength] : null
        };
        
        if (song.ImageBytes != null)
            Marshal.Copy(songInfo.ImageBytesDataPtr, song.ImageBytes, 0, song.ImageBytes.Length);
        
        // Free the memory allocated by the "C++" side
        songInfo.FreeSongStruct();
        
        return song;
    }
    
    public static SongInfo SongEntityToStruct(this Song song)
    {
        var songInfo = new SongInfo()
        {
            IdentifierPtr = Marshal.StringToHGlobalUni(song.Identifier),
            TitlePtr = Marshal.StringToHGlobalUni(song.Title),
            ArtistPtr = Marshal.StringToHGlobalUni(song.Artist),
            Duration = song.Duration,
            PathPtr = Marshal.StringToHGlobalUni(song.Path),
            ImageUrlPtr = song.ImageUrl != null ? Marshal.StringToHGlobalUni(song.ImageUrl) : IntPtr.Zero,
            ImageBytesDataLength = song.ImageBytes?.Length ?? 0,
            ImageBytesDataPtr = song.ImageBytes != null ? Marshal.AllocHGlobal(song.ImageBytes.Length) : IntPtr.Zero
        };
        
        if (song.ImageBytes != null)
            Marshal.Copy(song.ImageBytes, 0, songInfo.ImageBytesDataPtr, song.ImageBytes.Length);
        
        return songInfo;
    }
    
    public static void FreeSongStruct(this SongInfo songInfo)
    {
        Marshal.FreeHGlobal(songInfo.IdentifierPtr);
        Marshal.FreeHGlobal(songInfo.TitlePtr);
        Marshal.FreeHGlobal(songInfo.ArtistPtr);
        Marshal.FreeHGlobal(songInfo.PathPtr);
        Marshal.FreeHGlobal(songInfo.ImageUrlPtr);
        Marshal.FreeHGlobal(songInfo.ImageBytesDataPtr);
    }
}