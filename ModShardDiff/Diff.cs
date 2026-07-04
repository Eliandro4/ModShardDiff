using System.Diagnostics;
using Serilog;
using UndertaleModLib;
using ImageMagick;
namespace ModShardDiff;
internal static class MainOperations
{
    public static async Task MainCommand(string name, string reference, string? outputFolder)
    {
        outputFolder ??= Path.Join(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(), "results");

        LoggerConfiguration logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(string.Format("logs/log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmm")));

        Log.Logger = logger.CreateLogger();

        Console.WriteLine($"Exporting differences between {name} and {reference} in {outputFolder}.");

        Stopwatch stopWatch = new();
        stopWatch.Start();
        Task<bool> task = FileReader.Diff(name, reference, outputFolder);
        try
        {
            await task;
        }
        catch(Exception ex)
        {
            Log.Error(ex, "Something went wrong");
        }

        if (task.Result)
        {
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine($"Process completed successfully in {elapsedTime}.");
        }
        else
        {
            Console.WriteLine("Failed exporting differences.");
        }

        await Log.CloseAndFlushAsync();
    }
}
internal static class FileReader
{
    public static async Task<bool> Diff(string name, string reference, string outputFolder)
    {
        DirectoryInfo dir = new(outputFolder);
        if (dir.Exists) dir.Delete(true);
        dir.Create();

        if (!File.Exists(name)) throw new FileNotFoundException($"File {name} does not exist.");
        if (!File.Exists(reference)) throw new FileNotFoundException($"File {reference} does not exist.");

        Task<UndertaleData?> taskName =  LoadFile(name);
        await taskName; 
        Task<UndertaleData?> taskRef =  LoadFile(reference);
        await taskRef;

        if (taskName.Result == null || taskRef.Result == null) throw new FormatException($"Cannot load {name} and {outputFolder}.");

        void FinishedDiff(Stopwatch watch, string part)
        {
            watch.Stop();
            TimeSpan ts = watch.Elapsed;
            var elapsedTime = string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
            Console.WriteLine($"Sucessfully compared all the {part} in {elapsedTime}");
            watch.Reset();
            watch.Start();
        }

        Stopwatch watch = new();
        watch.Start();
        DiffUtils.DiffCodes(taskName.Result, taskRef.Result, dir);
        FinishedDiff(watch, "Codes");
        DiffUtils.DiffObjects(taskName.Result, taskRef.Result, dir);
        FinishedDiff(watch, "Objects");
        DiffUtils.DiffRooms(taskName.Result, taskRef.Result, dir);
        FinishedDiff(watch, "Rooms");
        DiffUtils.DiffSounds(taskName.Result, taskRef.Result, dir);
        FinishedDiff(watch, "Sounds");
        DiffUtils.DiffSprites(taskName.Result, taskRef.Result, dir);
        FinishedDiff(watch, "Sprites");
        DiffUtils.DiffTexturePageItems(taskName.Result, taskRef.Result, dir);
        FinishedDiff(watch, "TexturePageItems");
        return true;
    }
    private static UndertaleData? LoadUmt(string filename)
    {
        UndertaleData? data = null;
        using (FileStream stream = new(filename, FileMode.Open, FileAccess.Read))
        {
            data = UndertaleIO.Read(stream);
        }

        Log.Information(string.Format("Successfully load: {0}.", filename));

        return data;
    }
    public static async Task<UndertaleData?> LoadFile(string filename)
    {
        UndertaleData? data =  null;
        // task load a data.win with umt
        Task taskLoadDataWinWithUmt = Task.Run(() =>
        {
            try
            {
                data = LoadUmt(filename);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Something went wrong");
                throw;
            }
        });
        // run
        await taskLoadDataWinWithUmt;
        return data;
    }
}

