// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using ExtraSurf.Shared;

internal class Program
{
    public delegate void TestDelegate();
    
    public static void Main(string[] args)
    {
        var hmm =
            "\r\n-- each DoString is a different Lua scope, no need for a giant do...end\r\nlocal auto, assign\r\n\r\nfunction auto(tab, key)\r\n    return setmetatable({}, {\r\n            __index = auto,\r\n            __newindex = assign,\r\n            parent = tab,\r\n            key = key\r\n    })\r\nend\r\n\r\nlocal meta = {__index = auto}\r\n\r\n-- The if statement below prevents the table from being\r\n-- created if the value assigned is nil. This is, I think,\r\n-- technically correct but it might be desirable to use\r\n-- assignment to nil to force a table into existence.\r\n\r\nfunction assign(tab, key, val)\r\n    -- if val ~= nil then\r\n    local oldmt = getmetatable(tab)\r\n    oldmt.parent[oldmt.key] = tab\r\n    setmetatable(tab, meta)\r\n    tab[key] = val\r\n    -- end\r\nend\r\n\r\nfunction AutomagicTable()\r\n    return setmetatable({}, meta)\r\nend\r\n\r\nfunction fif(test, if_true, if_false)\r\n    if test then return if_true else return if_false end\r\nend\r\n\r\nfunction deepcopy(orig)\r\n    local orig_type = type(orig)\r\n    local copy\r\n    if orig_type == 'table' then\r\n        copy = {}\r\n        for orig_key, orig_value in next, orig, nil do\r\n            copy[deepcopy(orig_key)] = deepcopy(orig_value)\r\n        end\r\n        setmetatable(copy, deepcopy(getmetatable(orig)))\r\n    else -- number, string, boolean, etc\r\n        copy = orig\r\n    end\r\n    return copy\r\nend\r\n\r\n\r\n-- start coroutine helpers\r\n-- This table is indexed by coroutine and simply contains the time at which the coroutine\r\n-- should be woken up.\r\nlocal WAITING_ON_TIME = WAITING_ON_TIME or {}\r\n\r\n-- Keep track of how long the game has been running.\r\nlocal CURRENT_TIME = CURRENT_TIME or 0\r\n\r\nfunction waitSeconds(seconds)\r\n    -- Grab a reference to the current running coroutine.\r\n    local co = coroutine.running()\r\n\r\n    -- If co is nil, that means we're on the main process, which isn't a coroutine and can't yield\r\n    assert(co ~= nil, 'The main thread cannot wait!')\r\n\r\n    -- Store the coroutine and its wakeup time in the WAITING_ON_TIME table\r\n    local wakeupTime = CURRENT_TIME + seconds\r\n    WAITING_ON_TIME[co] = wakeupTime\r\n\r\n    -- And suspend the process\r\n    return coroutine.yield(co)\r\nend\r\n\r\nfunction wakeUpWaitingThreads(deltaTime)\r\n    -- This function should be called once per game logic update with the amount of time\r\n    -- that has passed since it was last called\r\n    CURRENT_TIME = CURRENT_TIME + deltaTime\r\n\r\n    -- First, grab a list of the threads that need to be woken up. They'll need to be removed\r\n    -- from the WAITING_ON_TIME table which we don't want to try and do while we're iterating\r\n    -- through that table, hence the list.\r\n    local threadsToWake = {}\r\n    for co, wakeupTime in pairs(WAITING_ON_TIME) do\r\n        if wakeupTime < CURRENT_TIME then\r\n            table.insert(threadsToWake, co)\r\n        end\r\n    end\r\n\r\n    -- Now wake them all up.\r\n    for _, co in ipairs(threadsToWake) do\r\n        WAITING_ON_TIME[co] = nil -- Setting a field to nil removes it from the table\r\n        coroutine.resume(co)\r\n    end\r\nend\r\n\r\nfunction runProcess(func)\r\n    -- This function is just a quick wrapper to start a coroutine.\r\n    local co = coroutine.create(func)\r\n    return coroutine.resume(co)\r\nend\r\n\r\nlocal WAITING_ON_SIGNAL = WAITING_ON_SIGNAL or {}\r\n\r\nfunction waitSignal(signalName)\r\n    -- Same check as in waitSeconds; the main thread cannot wait\r\n    local co = coroutine.running()\r\n    assert(co ~= nil, 'The main thread cannot wait!')\r\n\r\n    if WAITING_ON_SIGNAL[signalStr] == nil then\r\n        -- If there wasn't already a list for this signal, start a new one.\r\n        WAITING_ON_SIGNAL[signalName] = { co }\r\n    else\r\n        table.insert(WAITING_ON_SIGNAL[signalName], co)\r\n    end\r\n\r\n    return coroutine.yield()\r\nend\r\n\r\nfunction signal(signalName)\r\n    local threads = WAITING_ON_SIGNAL[signalName]\r\n    if threads == nil then return end\r\n\r\n    WAITING_ON_SIGNAL[signalName] = nil\r\n    for _, co in ipairs(threads) do\r\n        coroutine.resume(co)\r\n    end\r\nend\r\n--end coroutine helpers\r\n\r\n-- iykyk\r\nmath.tau = math.pi * 2\r\n";

        Console.WriteLine(hmm);
        
        return;
        
        
        _ = Task.Run(() =>
        {
            InitBass();
            //DebugInfo();
            var songPath = Path.Combine(Environment.CurrentDirectory, "song.mp3");
            songPath = "https://www.youtube.com/watch?v=mRPdl8H7AQ8&list=RD7Ms83TWACyE&index=6";
            var lastPercentage = 0d;
            //var sw = Stopwatch.StartNew();
            var isGetting = GetSongData(songPath, data =>
            {
                var sw = Stopwatch.StartNew();
                PlaySong(data.IdentifierPtr, data.DataPtr, (int)data.DataLength, 1f);
                sw.Stop();
                Console.WriteLine($"Got song data and played in {sw.ElapsedMilliseconds}ms");
            }, (identifier, percentage) =>
            {
                if (percentage <= lastPercentage)
                    return;
                //Console.WriteLine($"Progress: {percentage * 100}%");
                lastPercentage = percentage;
            });
            Console.WriteLine($"Is getting: {isGetting}");
        });
        
        Console.ReadLine();
    }
    
    [DllImport("ExtraSurf.dll", EntryPoint = "SetSongEndedCallback", CharSet = CharSet.Unicode)]
    public static extern void SetCallBack(TestDelegate callback);
    
    [DllImport("ExtraSurf.dll", EntryPoint = "InitBass", CharSet = CharSet.Unicode)]
    public static extern bool InitBass();
    
    [DllImport("ExtraSurf.dll", EntryPoint = "GetSongInfo", CharSet = CharSet.Unicode)]
    public static extern bool GetSongInfo(string path, Callbacks.SongInfoCallback callback);
    
    [DllImport("ExtraSurf.dll", EntryPoint = "GetSongData", CharSet = CharSet.Unicode)]
    public static extern bool GetSongData(string path, Callbacks.SongDataCallback callback, Callbacks.GetDataProgressCallback progressCallback);
    
    [DllImport("ExtraSurf.dll", EntryPoint = "PlaySong", CharSet = CharSet.Unicode)]
    public static extern bool PlaySong(nint identifierPtr, nint dataPtr, int dataLength, float volume);
}