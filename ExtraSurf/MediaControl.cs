using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using ManagedBass;

namespace ExtraSurf;

public class MediaControl
{
    private static ConcurrentDictionary<string, int> Streams = new();
    private static ConcurrentDictionary<string, float[][]> FftData = new();
    private static ConcurrentDictionary<string, float[]> SumsData = new();

    [UnmanagedCallersOnly(EntryPoint = "Init")]
    public static bool Init()
    {
        var bassInit = Bass.Init();
        if (!bassInit)
            return false;

        //get all filenames in current directory starting with "bass" except "bass" itself
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "bass*.dll")
            .Where(x => !x.EndsWith("bass.dll", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        //load all bass plugins, log errors
        foreach (var file in files)
        {
            Console.WriteLine($"Loading {file}...");
            var plugin = Bass.PluginLoad(file);
            if (plugin == 0)
                Console.WriteLine($"Failed to load plugin {file}: {Bass.LastError}");
        }

        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "StartBGM")]
    public static bool StartBGM(nint pathPtr, float volume)
    {
        if (Streams.TryGetValue("bgm", out var stream) && stream != 0)
            Bass.StreamFree(stream);

        var filename = Marshal.PtrToStringUni(pathPtr);
        stream = Bass.CreateStream(filename, 0, 0, BassFlags.Default);
        Bass.ChannelSetAttribute(stream, ChannelAttribute.Volume, volume);
        //loop
        Bass.ChannelFlags(stream, BassFlags.Loop, BassFlags.Loop);
        Streams.AddOrUpdate("bgm", stream, (_, _) => stream);
        Bass.ChannelPlay(stream);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "StopBGM")]
    public static bool StopBGM()
    {
        if (Streams.TryGetValue("bgm", out var stream) && stream != 0)
            Bass.StreamFree(stream);

        Streams.AddOrUpdate("bgm", 0, (_, _) => 0);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "SetBGMVolume")]
    public static bool SetBGMVolume(float volume)
    {
        if (Streams.TryGetValue("bgm", out var stream) && stream != 0)
            Bass.ChannelSetAttribute(stream, ChannelAttribute.Volume, volume);

        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "GetDuration")]
    public static double GetDuration(nint dataPtr, int dataLength)
    {
        var stream = Bass.CreateStream(dataPtr, 0, dataLength, BassFlags.Float | BassFlags.Decode | BassFlags.Prescan);
        var duration = Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream));
        Bass.StreamFree(stream);
        return duration;
    }

    [UnmanagedCallersOnly(EntryPoint = "PreScanSong")]
    public static bool PreScanSong(nint pathPtr, nint dataPtr, int dataLength)
    {
        var identifier = Marshal.PtrToStringUni(pathPtr);
        Console.WriteLine($"PreScanSong: {identifier}");
        var stream = Bass.CreateStream(dataPtr, 0, dataLength, BassFlags.Float | BassFlags.Decode | BassFlags.Prescan);
        Console.WriteLine($"PreScanSong: {identifier} - {stream}");
        if (stream == 0)
        {
            Console.WriteLine($"PreScanSong: {identifier} - {Bass.LastError}");
            return false;
        }
        var couldGetData = CollectStreamData(identifier, stream);
        Bass.StreamFree(stream);
        return couldGetData;
    }

    [UnmanagedCallersOnly(EntryPoint = "GetStreamByIdentifier")]
    public static int GetStreamId(nint pathPtr)
    {
        var filename = Marshal.PtrToStringUni(pathPtr);
        if (Streams.TryGetValue(filename, out var stream) && stream != 0)
            return stream;
        return -1;
    }

    [UnmanagedCallersOnly(EntryPoint = "GetAllStreamCount")]
    public static int GetAllStreamCount()
    {
        return Streams.Count;
    }

    [UnmanagedCallersOnly(EntryPoint = "GetAllStreamIds")]
    public static IntPtr GetAllStreamIds()
    {
        //get all stream ids as an array of strings and join them, seprated by a comma
        var ids = Streams.Keys.ToArray();
        var joined = string.Join(",", ids);
        return Marshal.StringToHGlobalUni(joined);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetStreamPositionSeconds")]
    public static double GetStreamPosition(int stream) =>
        Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetPosition(stream));
    
    [UnmanagedCallersOnly(EntryPoint = "GetStreamPositionBytes")]
    public static long GetStreamPositionBytes(int stream) =>
        Bass.ChannelGetPosition(stream);
    
    [UnmanagedCallersOnly(EntryPoint = "GetStreamLength")]
    public static long GetStreamLength(int stream) =>
        Bass.ChannelGetLength(stream);

    [UnmanagedCallersOnly(EntryPoint = "StopStream")]
    public static bool StopStream(int stream)
    {
        Bass.ChannelStop(stream);
        Bass.StreamFree(stream);

        //remove stream from dictionary
        var streamToRemove = Streams.FirstOrDefault(x => x.Value == stream).Key;
        if (streamToRemove != null)
            Streams.TryRemove(streamToRemove, out _);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "PauseStream")]
    public static bool PauseStream(int stream)
    {
        Bass.ChannelPause(stream);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "ResumeStream")]
    public static bool ResumeStream(int stream)
    {
        Bass.ChannelPlay(stream);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "SetStreamVolume")]
    public static bool SetStreamVolume(int stream, float volume)
    {
        Bass.ChannelSetAttribute(stream, ChannelAttribute.Volume, volume);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "PlayStream")]
    public static bool PlayStream(nint pathPtr, nint dataPtr, int dataLength, float volume)
    {
        var filename = Marshal.PtrToStringUni(pathPtr);
        var stream = Bass.CreateStream(dataPtr, 0, dataLength, BassFlags.Float | BassFlags.Prescan);
        if (stream == 0)
            return false;
        Bass.ChannelSetAttribute(stream, ChannelAttribute.Volume, volume);
        Streams.AddOrUpdate(filename, stream, (_, _) => stream);
        Bass.ChannelPlay(stream);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "GetLastBassError")]
    public static nint GetLastBassError()
    {
        var lastError = Bass.LastError.ToString();
        var lastErrorPtr = Marshal.StringToHGlobalUni(lastError);
        return lastErrorPtr;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetStreamDataLength")]
    public static int GetStreamData(nint pathPtr)
    {
        var filename = Marshal.PtrToStringUni(pathPtr);
        if (SumsData.TryGetValue(filename, out var data))
            return data.Length;
        return -1;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetStreamDataSums")]
    public static IntPtr GetStreamDataSums(nint pathPtr)
    {
        var filename = Marshal.PtrToStringUni(pathPtr);
        if (!SumsData.TryGetValue(filename, out var data)) 
            return IntPtr.Zero;
        
        var dataPtr = Marshal.AllocHGlobal(data.Length * sizeof(float));
        Marshal.Copy(data, 0, dataPtr, data.Length);
        return dataPtr;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "GetStreamDataFull")]
    public static IntPtr GetStreamDataFull(nint pathPtr, int pos)
    {
        var filename = Marshal.PtrToStringUni(pathPtr);
        if (!FftData.TryGetValue(filename, out var data)) 
            return IntPtr.Zero;
        
        if (pos >= data.Length)
            return IntPtr.Zero;
        
        var dataPtr = Marshal.AllocHGlobal(data[pos].Length * sizeof(float));
        Marshal.Copy(data[pos], 0, dataPtr, data[pos].Length);
        return dataPtr;
    }

    private static bool CollectStreamData(string identifier, int stream)
    {
        Console.WriteLine($"Collecting data for {identifier}");
        //try to get stream
        if (stream == 0)
            return false;

        List<float[]> fftSegments = new();

        //get fft data
        Console.WriteLine("Getting fft data");
        var fftData = new float[512];
        while (Bass.ChannelGetData(stream, fftData, (int)DataFlags.FFT1024) > 0)
        {
            fftSegments.Add(fftData);
            fftData = new float[512];
        }

        float[][] fftDataArray = fftSegments.ToArray();
        Console.WriteLine($"Got {fftDataArray.Length} fft segments");

        //get sum data
        var sumData = new float[fftDataArray.Length];
        for (int i = 0; i < fftDataArray.Length; i++)
        {
            float sum = 0;
            for (int j = 0; j < fftDataArray[i].Length; j++)
                sum += fftDataArray[i][j];

            sumData[i] = (float)Math.Max(0f, Math.Sqrt(sum));
        }

        //add to dictionary
        Console.WriteLine("Adding to dictionary");
        FftData.AddOrUpdate(identifier, fftDataArray, (_, _) => fftDataArray);
        SumsData.AddOrUpdate(identifier, sumData, (_, _) => sumData);
        return true;
    }
}