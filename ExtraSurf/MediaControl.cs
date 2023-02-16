using System.Runtime.InteropServices;
using ExtraSurf.Shared;
using ManagedBass;

namespace ExtraSurf;

public class MediaControl
{
    private static Callbacks.SongEndedCallback? SongEnded;

    private static int _bgmStream = 0;
    private static int _audioStream = 0;

    //should always be a float[512]
    private static IntPtr _fftFrameArrayPtr = IntPtr.Zero;

    private static Dictionary<string, float[]> _fftSumsCache = new();
    private static Dictionary<string, float[][]> _fftFullCache = new();

    [UnmanagedCallersOnly(EntryPoint = "DebugInfo")]
    public static void DebugInfo()
    {
        Console.WriteLine("Hello from .NET7 AOT!");
        Console.WriteLine($"Running on {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Runtime: {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"Bass Version: {Bass.Version}");
    }

    [UnmanagedCallersOnly(EntryPoint = "InitBass")]
    public static bool Init()
    {
        var couldBass = Bass.Init();
        if (!couldBass)
            throw new Exception("[Bass Init] Could not initialize bass: " + Bass.LastError);

        //get all bass dlls, except bass.dll
        var bassDlls = Directory.GetFiles(Environment.CurrentDirectory, "bass*.dll");
        foreach (var bassDll in bassDlls)
        {
            if (bassDll.EndsWith("bass.dll"))
                continue;
            Console.WriteLine("[Bass Init]Loading " + bassDll);
            var pluginIndex = Bass.PluginLoad(bassDll);
            if (pluginIndex == 0)
                Console.WriteLine("[Bass Init] Could not load " + bassDll + " Error: " + Bass.LastError);
        }

        Console.WriteLine("[Bass Init] Initialized");
        return couldBass;
    }

    [UnmanagedCallersOnly(EntryPoint = "SetSongEndedCallback")]
    public static void SetSongEndedCallback(nint callback)
    {
        SongEnded = Marshal.GetDelegateForFunctionPointer<Callbacks.SongEndedCallback>(callback);
        Console.WriteLine("[Set Song Ended Callback] Set to " + callback);
    }

    [UnmanagedCallersOnly(EntryPoint = "SetFFTFrameArrayPtr")]
    public static void SetFFTFrameArrayPtr(nint ptr)
    {
        _fftFrameArrayPtr = ptr;
        Console.WriteLine("[Set FFT Frame Array Ptr] Set to " + ptr);
    }

    [UnmanagedCallersOnly(EntryPoint = "PlayBGM")]
    public static bool PlayBGM(nint pathPtr, float volume)
    {
        if (_bgmStream != 0)
            Bass.StreamFree(_bgmStream);

        var path = Marshal.PtrToStringUni(pathPtr);
        if (path == null)
            return false;

        _bgmStream = Bass.CreateStream(path, 0, 0, BassFlags.Float);
        if (_bgmStream == 0)
        {
            Console.WriteLine("[Play BGM] Could not create stream: " + Bass.LastError);
            return false;
        }

        Bass.ChannelSetAttribute(_bgmStream, ChannelAttribute.Volume, volume);
        Bass.ChannelFlags(_bgmStream, BassFlags.Loop, BassFlags.Loop);
        Bass.ChannelPlay(_bgmStream);
        Console.WriteLine("[Play BGM] Playing " + path);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "SetBGMVolume")]
    public static void SetBGMVolume(float volume)
    {
        Bass.ChannelSetAttribute(_bgmStream, ChannelAttribute.Volume, volume);
        Console.WriteLine("[Set BGM Volume] Volume: " + volume);
    }

    [UnmanagedCallersOnly(EntryPoint = "StopBGM")]
    public static void StopBGM()
    {
        Bass.ChannelStop(_bgmStream);
        Console.WriteLine("[Stop BGM] Stopped");
    }

    [UnmanagedCallersOnly(EntryPoint = "PreScanSong")]
    public static bool PreScanSong(nint identifierPtr, nint dataPtr, long dataLength)
    {
        var identifier = Marshal.PtrToStringUni(identifierPtr);
        if (identifier == null)
            return false;

        var tempChannel =
            Bass.CreateStream(dataPtr, 0, dataLength, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
        if (tempChannel == 0)
        {
            Console.WriteLine("[PreScan Song] Could not create stream: " + Bass.LastError);
            return false;
        }

        var fftSums = new List<float>();
        var fftFull = new List<float[]>();
        var fft = new float[512]; //use with FFT1024 flag

        while (Bass.ChannelGetData(tempChannel, fft, (int)(DataFlags.FFT1024 | DataFlags.Float)) > 0)
        {
            // do Dylan thing aka divide each value by 4
            for (var i = 0; i < fft.Length; i++)
                fft[i] /= 4;

            fftFull.Add(fft);
            var sum = fft.Sum(t => (float)Math.Sqrt(Math.Max(0f, t)));
            fftSums.Add(Math.Max(0f, sum));
            fft = new float[512];
        }

        Bass.StreamFree(tempChannel);

        _fftSumsCache.Add(identifier, fftSums.ToArray());
        _fftFullCache.Add(identifier, fftFull.ToArray());
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "IsSongCached")]
    public static bool IsSongCached(nint identifierPtr)
    {
        var identifier = Marshal.PtrToStringUni(identifierPtr);
        return identifier != null && _fftSumsCache.ContainsKey(identifier);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetSongFftSums")]
    public static SongFftDataSums GetSongFftSums(nint identifierPtr)
    {
        var identifier = Marshal.PtrToStringUni(identifierPtr);
        if (identifier == null)
            return new SongFftDataSums();

        if (!_fftSumsCache.ContainsKey(identifier))
            return new SongFftDataSums();

        var fftSums = _fftSumsCache[identifier];
        var fftSumsPtr = Marshal.AllocHGlobal(fftSums.Length * sizeof(float));
        Marshal.Copy(fftSums, 0, fftSumsPtr, fftSums.Length);
        return new SongFftDataSums()
        {
            IdentifierPtr = identifierPtr,
            DataPtr = fftSumsPtr,
            DataLength = fftSums.Length
        };
    }

    [UnmanagedCallersOnly(EntryPoint = "GetSongFftFull")]
    public static SongFftDataFull GetSongFftFull(nint identifierPtr, int index)
    {
        var identifier = Marshal.PtrToStringUni(identifierPtr);
        if (identifier == null)
            return new SongFftDataFull();

        if (!_fftFullCache.ContainsKey(identifier))
            return new SongFftDataFull();

        var fftFull = _fftFullCache[identifier];
        if (index >= fftFull.Length)
            return new SongFftDataFull();

        var fft = fftFull[index];
        var fftPtr = Marshal.AllocHGlobal(fft.Length * sizeof(float));
        Marshal.Copy(fft, 0, fftPtr, fft.Length);
        return new SongFftDataFull()
        {
            IdentifierPtr = identifierPtr,
            DataPtr = fftPtr,
            FullDataLength = fftFull.Length,
            DataIndex = index
        };
    }

    [UnmanagedCallersOnly(EntryPoint = "PlaySong")]
    public static bool PlaySong(nint identifierPtr, nint dataPtr, long dataLength, float volume)
    {
        var identifier = Marshal.PtrToStringUni(identifierPtr);
        if (identifier == null)
            return false;

        if (_audioStream != 0)
            Bass.StreamFree(_audioStream);

        _audioStream = Bass.CreateStream(dataPtr, 0, dataLength, BassFlags.Float | BassFlags.Prescan);
        if (_audioStream == 0)
        {
            Console.WriteLine("[Play Song] Could not create stream: " + Bass.LastError);
            return false;
        }

        Bass.ChannelSetAttribute(_audioStream, ChannelAttribute.Volume, volume);
        //set granule to 512
        Bass.ChannelSetAttribute(_audioStream, ChannelAttribute.Granule, 512);
        //set sync to call end callback
        Bass.ChannelSetSync(_audioStream, SyncFlags.End, 0, (_, _, _, _) => SongEnded?.Invoke());
        Bass.ChannelPlay(_audioStream);
        Console.WriteLine("[Play Song] Playing " + identifier);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "PauseSong")]
    public static void PauseSong()
    {
        Bass.ChannelPause(_audioStream);
        Console.WriteLine("[Pause Song] Paused");
    }
    
    [UnmanagedCallersOnly(EntryPoint = "ResumeSong")]
    public static void ResumeSong()
    {
        Bass.ChannelPlay(_audioStream);
        Console.WriteLine("[Resume Song] Resumed");
    }
    
    [UnmanagedCallersOnly(EntryPoint = "SetSongVolume")]
    public static void SetSongVolume(float volume)
    {
        Bass.ChannelSetAttribute(_audioStream, ChannelAttribute.Volume, volume);
        Console.WriteLine("[Set Song Volume] Volume: " + volume);
    }

    [UnmanagedCallersOnly(EntryPoint = "StopSong")]
    public static void StopSong()
    {
        Bass.ChannelStop(_audioStream);
        Console.WriteLine("[Stop Song] Stopped");
    }

    [UnmanagedCallersOnly(EntryPoint = "GetSongPosition")]
    public static long GetSongPosition()
    {
        if (Bass.ChannelIsActive(_audioStream) == PlaybackState.Playing)
            return -1;

        return Bass.ChannelGetPosition(_audioStream);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetSongPositionSeconds")]
    public static double GetSongPositionSeconds()
    {
        if (Bass.ChannelIsActive(_audioStream) == PlaybackState.Playing)
            return -1;

        return Bass.ChannelBytes2Seconds(_audioStream, Bass.ChannelGetPosition(_audioStream));
    }

    [UnmanagedCallersOnly(EntryPoint = "GetLiveFft")]
    public static bool GetLiveFft()
    {
        //check if stream is playing
        if (Bass.ChannelIsActive(_audioStream) == PlaybackState.Playing)
            return false;

        var fft = new float[512];
        Bass.ChannelGetData(_audioStream, fft, (int)(DataFlags.FFT1024 | DataFlags.Float));
        // do Dylan thing aka divide each value by 4
        for (var i = 0; i < fft.Length; i++)
            fft[i] /= 4;

        Marshal.Copy(fft, 0, _fftFrameArrayPtr, fft.Length);
        return true;
    }
}