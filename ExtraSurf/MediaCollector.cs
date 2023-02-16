using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ATL;
using ExtraSurf.Shared;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace ExtraSurf;

public class MediaCollector
{
    public static YoutubeClient YoutubeClient { get; } = new();
    public static HttpClient HttpClient { get; } = new();

    public static ConcurrentDictionary<string, Song> CachedSongInfos { get; } = new();

    public static ConcurrentDictionary<string, byte[]> CachedSongData { get; } = new();

    [UnmanagedCallersOnly(EntryPoint = "GetSongInfo")]
    public static bool GetSongInfo(nint pathPtr, nint callback)
    {
        var songInfoCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.SongInfoCallback>(callback);
        var path = Marshal.PtrToStringUni(pathPtr);

        var videoId = VideoId.TryParse(path);

        if (videoId is not null)
        {
            _ = Task.Run(async () =>
            {
                if (!CachedSongInfos.TryGetValue($"youtube:{videoId.Value.Value}", out var songInfo))
                    songInfo = await CacheYoutubeSongInfoAsync(videoId.Value);
                songInfoCallback(songInfo.SongEntityToStruct());
            });
            return true;
        }

        var fileExists = File.Exists(path);
        if (!fileExists)
            return false;

        _ = Task.Run(async () =>
        {
            var hashString = await GetLocalSongHashAsync(path!);
            if (!CachedSongInfos.TryGetValue(hashString, out var songInfo))
                songInfo = await CacheLocalSongInfoAsync(path!);
            songInfoCallback(songInfo.SongEntityToStruct());
        });
        return true;
    }

    private static async Task<Song> CacheYoutubeSongInfoAsync(VideoId videoId)
    {
        var video = await YoutubeClient.Videos.GetAsync(videoId);

        var song = new Song()
        {
            Title = video.Title,
            Artist = video.Author.ChannelTitle,
            Duration = video.Duration != null ? (float)video.Duration.Value.TotalSeconds : 0,
            Path = $"https://www.youtube.com/watch?v={videoId.Value}",
            ImageUrl = video.Thumbnails.TryGetWithHighestResolution() is { } thumb ? thumb.Url : null,
            Identifier = videoId.Value
        };

        CachedSongInfos.TryAdd($"youtube:{videoId.Value}", song);

        return song;
    }

    private static async Task<Song> CacheLocalSongInfoAsync(string path)
    {
        var track = new Track(path);
        byte[] thumbnail = null!;
        if (track.EmbeddedPictures.Count > 0)
        {
            var bigImg = track.EmbeddedPictures.MaxBy(x => x.PictureData.Length)!;
            thumbnail = bigImg.PictureData;
        }

        var hashString = await GetLocalSongHashAsync(path);
        Console.WriteLine($"Hash: {hashString}");

        var song = new Song()
        {
            Title = track.Title,
            Artist = track.Artist,
            Duration = track.Duration,
            Path = path,
            ImageBytes = thumbnail,
            Identifier = hashString
        };

        CachedSongInfos.TryAdd(hashString, song);

        return song;
    }

    private static async Task<string> GetLocalSongHashAsync(string path)
    {
        var fileBytes = await File.ReadAllBytesAsync(path);
        var hash = MD5.HashData(fileBytes);
        var asUInt64 = BitConverter.ToUInt64(hash, 0);
        return asUInt64.ToString();
    }

    [UnmanagedCallersOnly(EntryPoint = "GetSongData")]
    public static bool GetSongData(nint pathPtr, nint callbackPtr, nint progressCallbackPtr)
    {
        var songDataCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.SongDataCallback>(callbackPtr);
        var progressCallback =
            Marshal.GetDelegateForFunctionPointer<Callbacks.GetDataProgressCallback>(progressCallbackPtr);
        var path = Marshal.PtrToStringUni(pathPtr);

        if (path is null)
            return false;
        var vidId = VideoId.TryParse(path);
        if (vidId is null && !File.Exists(path))
            return false;

        _ = Task.Run(async () =>
        {
            var identifier = vidId is not null ? $"youtube:{vidId.Value.Value}" : await GetLocalSongHashAsync(path!);
            if (!CachedSongData.TryGetValue(identifier, out var songData))
            {
                songData = vidId is not null
                    ? await GetYoutubeSongDataAsync(vidId.Value, identifier, progressCallback)
                    : await GetLocalSongDataAsync(path!, identifier, progressCallback);
                CachedSongData.TryAdd(identifier, songData);
            }

            var dataPtr = Marshal.AllocHGlobal(songData.Length);
            Marshal.Copy(songData, 0, dataPtr, songData.Length);
            songDataCallback(new SongData()
            {
                IdentifierPtr = Marshal.StringToHGlobalUni(identifier),
                DataPtr = dataPtr,
                DataLength = songData.Length
            });
        });

        return true;
    }

    private static async Task<byte[]> GetYoutubeSongDataAsync(VideoId videoId, string identifier,
        Callbacks.GetDataProgressCallback progressCallback)
    {
        var streamManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(videoId);
        var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        var ms = new MemoryStream();
        var identifierPtr = Marshal.StringToHGlobalUni(identifier);
        await YoutubeClient.Videos.Streams.CopyToAsync(streamInfo, ms,
            new Progress<double>((progressVal) => progressCallback(identifierPtr, progressVal)));
        return ms.GetBuffer();
    }

    private static async Task<byte[]> GetLocalSongDataAsync(string path, string identifier,
        Callbacks.GetDataProgressCallback progressCallback)
    {
        var fileBytes = await File.ReadAllBytesAsync(path);
        var identifierPtr = Marshal.StringToHGlobalUni(identifier);
        progressCallback(identifierPtr, 1.0);
        return fileBytes;
    }

    [UnmanagedCallersOnly(EntryPoint = "GetImageBytesFromUrl")]
    public static bool GetImageBytesFromUrl(nint identifierPtr, nint urlPtr, nint callbackPtr)
    {
        var imageBytesCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.ImageBytesCallback>(callbackPtr);
        var url = Marshal.PtrToStringUni(urlPtr);

        //check if url is valid
        if (url is null)
            return false;

        _ = Task.Run(async () =>
        {
            var data = await HttpClient.GetByteArrayAsync(url);
            var dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);
            imageBytesCallback(identifierPtr, dataPtr, data.Length);
        });

        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "SearchYoutube")]
    public static bool SearchYoutube(nint queryPtr, int resultCount, nint resultCallbackPtr, nint endCallbackPtr)
    {
        var searchEndedCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.SearchEndCallback>(endCallbackPtr);
        var resultCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.SearchResultCallback>(resultCallbackPtr);
        var query = Marshal.PtrToStringUni(queryPtr);

        if (query is null)
            return false;

        _ = Task.Run(async () =>
        {
            var searchResults = YoutubeClient.Search.GetResultBatchesAsync(query, SearchFilter.Video);
            var index = 0;
            await foreach (var batch in searchResults)
            {
                foreach (var searchResult in batch.Items)
                {
                    var video = searchResult as VideoSearchResult;
                    if (video is null)
                        continue;

                    var hasSong = CachedSongInfos.TryGetValue($"youtube:{video.Id.Value}", out var song);
                    if (!hasSong)
                    {
                        song = new Song()
                        {
                            Identifier = $"youtube:{video.Id.Value}",
                            Title = video.Title,
                            Artist = video.Author.ChannelTitle,
                            Duration = (float)(video.Duration?.TotalSeconds ?? 0),
                            ImageUrl = video.Thumbnails.GetWithHighestResolution().Url,
                            Path = video.Url
                        };
                        CachedSongInfos.TryAdd(song.Identifier, song);
                    }
                    resultCallback(song.SongEntityToStruct());
                    index++;
                    if (index >= resultCount)
                        break;
                }

                if (index >= resultCount)
                    break;
            }
            
            searchEndedCallback();
        });

        return true;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetYoutubePlaylist")]
    public static bool GetYoutubePlaylist(nint playlistUrlPtr, int limit, nint resultCallbackPtr, nint endCallbackPtr)
    {
        var resultCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.PlaylistResultCallback>(resultCallbackPtr);
        var endCallback = Marshal.GetDelegateForFunctionPointer<Callbacks.PlaylistEndCallback>(endCallbackPtr);
        var playlistId = Marshal.PtrToStringUni(playlistUrlPtr);
        
        if (playlistId is null)
            return false;
        
        var plId = PlaylistId.TryParse(playlistId);
        if (plId is null)
            return false;

        _ = Task.Run(async () =>
        {
            var playlist = YoutubeClient.Playlists.GetVideoBatchesAsync(plId.Value);
            var index = 0;
            await foreach (var batch in playlist)
            {
                foreach (var video in batch.Items)
                {
                    var hasSong = CachedSongInfos.TryGetValue($"youtube:{video.Id.Value}", out var song);
                    if (!hasSong)
                    {
                        song = new Song()
                        {
                            Identifier = $"youtube:{video.Id.Value}",
                            Title = video.Title,
                            Artist = video.Author.ChannelTitle,
                            Duration = (float)(video.Duration?.TotalSeconds ?? 0),
                            ImageUrl = video.Thumbnails.GetWithHighestResolution().Url,
                            Path = video.Url
                        };
                        CachedSongInfos.TryAdd(song.Identifier, song);
                    }

                    resultCallback(song.SongEntityToStruct());
                    index++;
                    if (index >= limit)
                        break;
                }

                if (index >= limit)
                    break;
            }
            endCallback();
        });

        return true;
    }
}