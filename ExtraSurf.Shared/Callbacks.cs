using System;

namespace ExtraSurf.Shared
{
    public class Callbacks
    {
        public delegate void SongInfoCallback(SongInfo songInfo);

        public delegate void SongDataCallback(SongData songData);
    
        public delegate void GetDataProgressCallback(IntPtr identifier, double percentage);
        
        
        public delegate void SongEndedCallback();

        public delegate void ImageBytesCallback(IntPtr identifier, IntPtr data, long length);

        public delegate void SearchResultCallback(SongInfo song);
        public delegate void SearchEndCallback();

        public delegate void PlaylistResultCallback(SongInfo song);
        public delegate void PlaylistEndCallback();
    }
}