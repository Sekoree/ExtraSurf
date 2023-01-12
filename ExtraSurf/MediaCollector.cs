using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace ExtraSurf;

public class MediaCollector
{
    private static ConcurrentDictionary<string, byte[]?> CachedSongs { get; set; } = new();
    private static ConcurrentDictionary<string, double?> GetDataProgress { get; set; } = new();

    private static YoutubeClient Client { get; set; } = new();
    
    [UnmanagedCallersOnly(EntryPoint = "StartSongDataGet")]
    public static bool StartSongDataGet(nint pathOrIdentifier)
    {
        var path = Marshal.PtrToStringUni(pathOrIdentifier);
        if (CachedSongs.ContainsKey(path))
            return true;
        
        //check if YouTube video
        if (path.StartsWith("youtube:"))
        {
            //youtube identifier are formatted like this: youtube:videoId
            var videoId = VideoId.TryParse(path.Split(':')[1]);
            if (videoId == null)
                return false;


            CachedSongs.TryAdd(path, null);
            GetDataProgress.TryAdd(path, 0);
            _ = Task.Run(() => GetYouTubeSongDataAsync(path, videoId.Value));
            return true;
        }

        if (!File.Exists(path))
            return false;
        
        var data = File.ReadAllBytes(path);
        CachedSongs.TryAdd(path, data);
        GetDataProgress.TryAdd(path, 100);
        return true;
    }

    private static async Task GetYouTubeSongDataAsync(string identifier, VideoId videoId)
    {
        if (identifier == null) 
            throw new ArgumentNullException(nameof(identifier));
        var streamInfoSet = await Client.Videos.Streams.GetManifestAsync(videoId);
        var streamInfo = streamInfoSet.GetAudioOnlyStreams().GetWithHighestBitrate();
        var memoryStream = new MemoryStream();
        await Client.Videos.Streams.CopyToAsync(streamInfo, memoryStream, new Progress<double>((val) =>
        {
            GetDataProgress[identifier] = val * 100;
        }));
        GetDataProgress[identifier] = 100;
        CachedSongs[identifier] = memoryStream.GetBuffer();
    }
    
    [UnmanagedCallersOnly(EntryPoint = "IsSongCached")]
    public static bool IsSongCached(nint pathOrIdentifier)
    {
        var path = Marshal.PtrToStringUni(pathOrIdentifier);
        return CachedSongs.ContainsKey(path);
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetSongDataProgress")]
    public static double GetSongDataProgress(nint pathOrIdentifier)
    {
        var path = Marshal.PtrToStringUni(pathOrIdentifier);
        if (!GetDataProgress.ContainsKey(path))
            return -1;
        return GetDataProgress[path] ?? 0;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetSongData")]
    public static nint GetSongData(nint pathOrIdentifier)
    {
        var path = Marshal.PtrToStringUni(pathOrIdentifier);
        if (!CachedSongs.ContainsKey(path))
            return IntPtr.Zero;
        var data = CachedSongs[path];
        if (data == null)
            return IntPtr.Zero;
        
        var ptr = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, ptr, data.Length);
        return ptr;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetSongDataLength")]
    public static int GetSongDataLength(nint pathOrIdentifier)
    {
        var path = Marshal.PtrToStringUni(pathOrIdentifier);
        if (!CachedSongs.ContainsKey(path))
            return -1;
        var data = CachedSongs[path];
        if (data == null)
            return -1;
        return data.Length;
    }
    
    
}