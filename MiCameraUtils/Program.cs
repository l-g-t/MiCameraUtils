// See https://aka.ms/new-console-template for more information

try
{
    // 显示欢迎横幅
    ShowWelcomeBanner();

    // 创建配置构建器
    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.MiCameraUtils.json", optional: false, reloadOnChange: true);

    IConfiguration config = builder.Build();

    // 绑定配置到类
    var appSettings = config.Get<AppSettings>() ?? throw new ArgumentNullException(nameof(AppSettings));

    // 验证配置
    ValidateConfiguration(appSettings);

    CancellationTokenSource cancellationTokenSource;
    var finishedRunning = false;

    cancellationTokenSource = new CancellationTokenSource();

    // 设置优雅退出处理
    AppDomain.CurrentDomain.ProcessExit += new EventHandler((u, v) =>
    {
        cancellationTokenSource.Cancel();

        if (!finishedRunning)
        {
            AnsiConsole.MarkupLine("[yellow]正在等待 FFmpeg 进程退出...[/]");
            Thread.Sleep(100);
        }

        cancellationTokenSource.Dispose();
    });

    if (appSettings.ThreadsCount < 0)
    {
        appSettings.ThreadsCount = Environment.ProcessorCount / 2;
    }

    // 下载 FFmpeg with progress
    await DownloadFFmpegWithProgress();

    var choicesMapping = new Dictionary<string, MergeMode>
    {
        { "📹 从摄像头文件按天合并为单个文件", MergeMode.MergeFromCamera },
        { "⚡ 将合并完的摄像头文件加速并输出", MergeMode.VideoAccelerate },
        { "🔗 合并加速完的摄像头文件为单个文件", MergeMode.MergeFromAccelerate }
    };

    // 显示配置信息
    DisplayConfiguration(appSettings);

    // 使用 Spectre.Console 读取用户输入的 MergeMode
    var modeTitle = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold cyan]请选择操作模式:[/]")
            .PageSize(10)
            .AddChoices(choicesMapping.Keys)
            .HighlightStyle(new Style(Color.Green)));

    appSettings.Mode = choicesMapping[modeTitle];

    // 确认执行
    var confirm = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title($"[bold yellow]确认信息:[/]\n" +
                  $"[green]操作模式:[/] {modeTitle}\n" +
                  $"[green]摄像机文件夹:[/] {appSettings.CameraDirectory}\n" +
                  $"[green]输出文件夹:[/] {appSettings.OutputDirectory}\n" +
                  $"[green]线程数:[/] {appSettings.ThreadsCount}\n" +
                  $"[green]覆盖同名文件:[/] {(appSettings.OverwriteOutput ? "是" : "否")}\n" +
                  $"[bold cyan]是否开始处理?[/]")
            .AddChoices(["✅ 是的，开始处理", "❌ 不，退出程序"])
            .HighlightStyle(new Style(Color.Green)));

    if (confirm == "✅ 是的，开始处理")
    {
        AnsiConsole.MarkupLine("[bold green]开始处理...[/]");
        
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
        
        ShowCompletionMessage();
    }
    else
    {
        AnsiConsole.MarkupLine("[yellow]用户取消操作，程序退出。[/]");
    }

    finishedRunning = true;
    
    AnsiConsole.MarkupLine("[dim]按任意键退出...[/]");
    Console.ReadLine();
}
catch (FileNotFoundException ex)
{
    AnsiConsole.MarkupLine($"[red]错误: 找不到配置文件[/]");
    AnsiConsole.MarkupLine($"[yellow]详细信息:[/] {ex.Message}");
    AnsiConsole.MarkupLine("[dim]按任意键退出...[/]");
    Console.ReadLine();
}
catch (DirectoryNotFoundException ex)
{
    AnsiConsole.MarkupLine($"[red]错误: 找不到指定目录[/]");
    AnsiConsole.MarkupLine($"[yellow]详细信息:[/] {ex.Message}");
    AnsiConsole.MarkupLine($"[cyan]请检查配置文件中的目录路径是否正确。[/]");
    AnsiConsole.MarkupLine("[dim]按任意键退出...[/]");
    Console.ReadLine();
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]程序运行失败[/]");
    AnsiConsole.MarkupLine($"[yellow]错误信息:[/] {ex.Message}");
    AnsiConsole.MarkupLine("[dim]按任意键退出...[/]");
    Console.ReadLine();
    throw;
}

static void ShowWelcomeBanner()
{
    var rule = new Rule("[bold blue]Mi Camera Utils[/]");
    rule.Justification = Justify.Center;
    AnsiConsole.Write(rule);
    
    AnsiConsole.MarkupLine("[dim]小米摄像头工具 - 用于合并视频, 制作倍速视频[/]");
    AnsiConsole.MarkupLine("[dim]版本: 1.0.0[/]");
    AnsiConsole.WriteLine();
}

static void ValidateConfiguration(AppSettings appSettings)
{
    if (!Directory.Exists(appSettings.CameraDirectory))
    {
        throw new DirectoryNotFoundException($"摄像机目录不存在: {appSettings.CameraDirectory}");
    }

    if (!Directory.Exists(appSettings.OutputDirectory))
    {
        throw new DirectoryNotFoundException($"输出目录不存在: {appSettings.OutputDirectory}");
    }
}

static void DisplayConfiguration(AppSettings appSettings)
{
    var table = new Table();
    table.AddColumn("[bold]配置项[/]");
    table.AddColumn("[bold]值[/]");
    table.Border(TableBorder.Rounded);
    table.Title("[bold cyan]当前配置[/]");
    
    table.AddRow("摄像机目录", $"[green]{appSettings.CameraDirectory}[/]");
    table.AddRow("输出目录", $"[green]{appSettings.OutputDirectory}[/]");
    table.AddRow("线程数", $"[yellow]{appSettings.ThreadsCount}[/]");
    table.AddRow("覆盖同名文件", $"[{(appSettings.OverwriteOutput ? "green" : "red")}]{(appSettings.OverwriteOutput ? "是" : "否")}[/]");
    
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static async Task DownloadFFmpegWithProgress()
{
    await AnsiConsole.Progress()
        .Columns(new ProgressColumn[] 
        {
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new SpinnerColumn(),
        })
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]下载 FFmpeg[/]");
            task.MaxValue = 100;
            
            try
            {
                // 尝试下载 FFmpeg
                for (int i = 0; i <= 50; i += 10)
                {
                    task.Value = i;
                    await Task.Delay(100);
                }
                
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
                
                for (int i = 60; i <= 100; i += 10)
                {
                    task.Value = i;
                    await Task.Delay(100);
                }
                
                task.Value = 100;
                AnsiConsole.MarkupLine("[green]✓[/] FFmpeg 下载完成");
            }
            catch (Exception ex)
            {
                task.Value = 100;
                AnsiConsole.MarkupLine($"[yellow]⚠️[/] FFmpeg 下载失败: {ex.Message}");
                AnsiConsole.MarkupLine("[dim]如果系统已安装 FFmpeg，可以忽略此警告[/]");
            }
        });
    
    AnsiConsole.WriteLine();
}

static void ShowCompletionMessage()
{
    var panel = new Panel("[bold green]✓ 所有任务已完成![/]")
        .Border(BoxBorder.Rounded)
        .BorderColor(Color.Green);
    
    AnsiConsole.Write(panel);
    AnsiConsole.WriteLine();
}
