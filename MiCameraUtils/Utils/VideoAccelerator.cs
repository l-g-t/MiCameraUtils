public class VideoAccelerator
{
    private readonly AppSettings appSettings;
    private readonly CancellationToken cancellationToken;

    public VideoAccelerator(AppSettings appSettings, CancellationToken cancellationToken)
    {
        this.appSettings = appSettings;
        this.cancellationToken = cancellationToken;
    }

    public async Task AccelerateAsync()
    {
        var pattern = @"^\d{8}\.mp4$";
        var regex = new Regex(pattern, RegexOptions.Compiled);

        var files = Directory.GetFiles(appSettings.OutputDirectory)
            .OrderBy(d => d)
            .Select(u => new FileInfo(u))
            .Where(d => regex.IsMatch(Path.GetFileName(d.Name)))
            .ToArray();

        if (!files.Any())
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ 未找到符合格式的合并视频文件 (格式: YYYYMMDD.mp4)[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]⚡ 找到 {files.Length} 个视频文件需要加速处理[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[] 
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var totalTask = ctx.AddTask("[green]总体进度[/]", maxValue: files.Length);
                
                foreach (var file in files)
                {
                    var outputFilePath = Path.Combine(appSettings.OutputDirectory, $"{Path.GetFileNameWithoutExtension(file.Name)}_Accelerate.mp4");
                    var fileTask = ctx.AddTask($"[blue]加速 {file.Name}[/]", maxValue: 100);
                    
                    AnsiConsole.MarkupLine($"[yellow]⚡ 开始加速视频: {file.Name}[/]");
                    AnsiConsole.MarkupLine($"[dim]   输出文件: {Path.GetFileName(outputFilePath)}[/]");
                    AnsiConsole.MarkupLine($"[dim]   文件大小: {FormatFileSize(file.Length)}[/]");

                    var conversion = FFmpeg.Conversions.New();

                    // setpts=0.01*PTS fast forward 100x
                    // -r 30 -g 60 to unify the frame rate and key frame
                    // -hwaccel cuda -c:v hevc_cuvid -c:v hevc_nvenc for hardware acceleration
                    conversion.AddParameter(string.Format(appSettings.VideoAcceleratorSettings.Parameters, file.FullName, outputFilePath));
                    conversion.UseMultiThread(appSettings.ThreadsCount);
                    conversion.SetOverwriteOutput(appSettings.OverwriteOutput);

                    conversion.OnProgress += (sender, args) =>
                    {
                        fileTask.Value = Math.Min(args.Percent, 100);
                    };

                    await conversion.Start(cancellationToken);

                    fileTask.Value = 100;
                    AnsiConsole.MarkupLine($"[green]✓ 加速完成: {Path.GetFileName(outputFilePath)}[/]");
                    
                    totalTask.Increment(1);
                }
            });
        
        AnsiConsole.MarkupLine("[bold green]🚀 所有视频加速处理完成![/]");
        AnsiConsole.WriteLine();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
