
// See https://aka.ms/new-console-template for more information

try
{
    // 创建配置构建器
    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.MiCameraUtils.json", optional: false, reloadOnChange: true);

    IConfiguration config = builder.Build();

    // 绑定配置到类
    var appSettings = config.Get<AppSettings>() ?? throw new ArgumentNullException(nameof(AppSettings));

    // 读取配置
    var cameraDirectory = appSettings.CameraDirectory;
    var outputDirectory = appSettings.OutputDirectory;

    if (!Directory.Exists(cameraDirectory))
    {
        throw new DirectoryNotFoundException($"Camera directory not found: {cameraDirectory}");
    }

    if (!Directory.Exists(outputDirectory))
    {
        throw new DirectoryNotFoundException($"Output directory not found: {outputDirectory}");
    }

    CancellationTokenSource cancellationTokenSource;
    var finishedRunning = false;

    cancellationTokenSource = new CancellationTokenSource();


    // See https://aka.ms/new-console-template for more information
    AppDomain.CurrentDomain.ProcessExit += new EventHandler((u, v) =>
    {
        cancellationTokenSource.Cancel();

        if (!finishedRunning)
        {
            Console.WriteLine("Wait ffmpeg exiting...");
            Thread.Sleep(100);
        }

        cancellationTokenSource.Dispose();
    });

    if (appSettings.ThreadsCount < 0)
    {
        appSettings.ThreadsCount = Environment.ProcessorCount / 2;
    }

    // 下载 FFmpeg
    Console.WriteLine("下载 FFmpeg...");
    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
    AnsiConsole.Clear();

    var choicesMapping = new Dictionary<string, MergeMode>
{
    { "1. 从摄像头文件按天合并为单个文件", MergeMode.MergeFromCamera },
    { "2. 将合并完的摄像头文件加速并输出", MergeMode.VideoAccelerate },
    { "3. 合并加速完的摄像头文件为单个文件", MergeMode.MergeFromAccelerate }
};

    // 使用 Spectre.Console 读取用户输入的 MergeMode
    var modeTitle = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("请选择操作模式:")
            .AddChoices(choicesMapping.Keys));

    appSettings.Mode = choicesMapping[modeTitle];

    // 确认执行
    var confirm = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title($"模式: {modeTitle}{Environment.NewLine}摄像机文件夹: {appSettings.CameraDirectory}{Environment.NewLine}输出文件夹: {appSettings.OutputDirectory}{Environment.NewLine}覆盖同名文件: {appSettings.OverwriteOutput}{Environment.NewLine}是否运行?")
            .AddChoices(["Yes", "No"]));

    if (confirm == "Yes")
    {
        switch (appSettings.Mode)
        {
            case MergeMode.MergeFromCamera:
                var cameraMerger = new CameraMerger(appSettings, cancellationTokenSource.Token);
                await cameraMerger.MergeAsync();
                break;
            case MergeMode.VideoAccelerate:
                var videoAccelerator = new VideoAccelerator(appSettings, cancellationTokenSource.Token);
                await videoAccelerator.AccelerateAsync();
                break;
            case MergeMode.MergeFromAccelerate:
                var acceleratedMerger = new AcceleratedMerger(appSettings, cancellationTokenSource.Token);
                await acceleratedMerger.MergeAsync();
                break;
        }
    }

    finishedRunning = true;
    Console.ReadLine();
}
catch (Exception)
{
    Console.WriteLine("Running Failed");
    Console.ReadLine();
    throw;
}
