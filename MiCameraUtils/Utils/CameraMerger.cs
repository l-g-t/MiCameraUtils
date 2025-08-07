public class CameraMerger
{
    readonly AppSettings appSettings;
    readonly CancellationToken cancellationToken;

    public CameraMerger(AppSettings appSettings, CancellationToken cancellationToken)
    {
        this.appSettings = appSettings;
        this.cancellationToken = cancellationToken;
    }

    public async Task MergeAsync()
    {
        string pattern = @"^\d{8}\d{2}$"; // 例如：匹配 YYYYMMddHH 格式的文件夹名称
        var regex = new Regex(pattern, RegexOptions.Compiled);

        var directories = Directory.GetDirectories(appSettings.CameraDirectory)
            .Where(d => regex.IsMatch(Path.GetFileName(d)))
            .OrderBy(d => d)
            .GroupBy(d => Path.GetFileName(d).Substring(0, 8)) // 按日期分组
            .ToList();

        if (!directories.Any())
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ 未找到符合格式的摄像头文件夹[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]📂 找到 {directories.Count} 个日期的视频文件[/]");
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
                var totalTask = ctx.AddTask("[green]总体进度[/]", maxValue: directories.Count);
                
                foreach (var group in directories)
                {
                    var date = group.Key;
                    var outputFilePath = Path.Combine(appSettings.OutputDirectory, $"{date}.mp4");
                    var files = group.SelectMany(directory => Directory.GetFiles(directory, "*.mp4")
                                .OrderBy(f => f))
                                .ToList();

                    if (files.Count > 0)
                    {
                        var dateTask = ctx.AddTask($"[blue]合并 {date}[/]", maxValue: 100);
                        
                        AnsiConsole.MarkupLine($"[yellow]🎬 开始合并文件到: {Path.GetFileName(outputFilePath)}[/]");
                        AnsiConsole.MarkupLine($"[dim]   文件数量: {files.Count} 个[/]");

                        var conversion = FFmpeg.Conversions.New();

                        // 创建一个临时文件列表
                        var tempFileList = Path.Combine(appSettings.OutputDirectory, $"{date}_filelist.txt");
                        using (StreamWriter writer = new(tempFileList))
                        {
                            foreach (var file in files)
                            {
                                writer.WriteLine($"file '{Path.GetFullPath(file)}'");
                            }
                        }

                        conversion.AddParameter($"-f concat -safe 0 -i \"{tempFileList}\" -c copy \"{outputFilePath}\"");
                        conversion.UseMultiThread(appSettings.ThreadsCount);
                        conversion.SetOverwriteOutput(appSettings.OverwriteOutput);

                        conversion.OnProgress += (sender, args) =>
                        {
                            dateTask.Value = Math.Min(args.Percent, 100);
                        };

                        await conversion.Start(cancellationToken);
                        
                        // 清理临时文件
                        try
                        {
                            File.Delete(tempFileList);
                        }
                        catch
                        {
                            // 忽略删除临时文件的错误
                        }

                        dateTask.Value = 100;
                        AnsiConsole.MarkupLine($"[green]✓ 文件合并完成: {Path.GetFileName(outputFilePath)}[/]");
                    }
                    
                    totalTask.Increment(1);
                }
            });
        
        AnsiConsole.MarkupLine("[bold green]🎉 所有摄像头文件合并完成![/]");
        AnsiConsole.WriteLine();
    }
}
