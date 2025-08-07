public class AcceleratedMerger
{
    private readonly AppSettings appSettings;
    private readonly CancellationToken cancellationToken;

    public AcceleratedMerger(AppSettings appSettings, CancellationToken cancellationToken)
    {
        this.appSettings = appSettings;
        this.cancellationToken = cancellationToken;
    }

    public async Task MergeAsync()
    {
        var pattern = @"^\d{8}_Accelerate\.mp4$";
        var regex = new Regex(pattern, RegexOptions.Compiled);
        var files = Directory.GetFiles(appSettings.OutputDirectory)
            .OrderBy(d => d)
            .Select(u => new FileInfo(u))
            .Where(d => regex.IsMatch(Path.GetFileName(d.Name)))
            .ToList();

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ 未找到符合格式的加速视频文件 (格式: YYYYMMDD_Accelerate.mp4)[/]");
            return;
        }

        if (files.Count == 1)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ 只找到一个加速视频文件，无需合并[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]🔗 找到 {files.Count} 个加速视频文件需要合并[/]");
        
        var mergedFileName = $"{Path.GetFileNameWithoutExtension(files[0].Name)}_To_{Path.GetFileNameWithoutExtension(files[^1].Name)}";
        var outputFilePath = Path.Combine(appSettings.OutputDirectory, mergedFileName + ".mp4");
        
        AnsiConsole.MarkupLine($"[dim]输出文件: {Path.GetFileName(outputFilePath)}[/]");
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
                var mergeTask = ctx.AddTask("[green]合并加速视频[/]", maxValue: 100);
                
                var conversion = FFmpeg.Conversions.New();

                // 创建一个临时文件列表
                var tempFileList = Path.Combine(appSettings.OutputDirectory, $"{mergedFileName}_filelist.txt");
                using (StreamWriter writer = new(tempFileList))
                {
                    foreach (var file in files)
                    {
                        writer.WriteLine($"file '{file.FullName}'");
                    }
                }

                conversion.AddParameter($"-f concat -safe 0 -i \"{tempFileList}\" -c copy \"{outputFilePath}\"");
                conversion.UseMultiThread(appSettings.ThreadsCount);
                conversion.SetOverwriteOutput(appSettings.OverwriteOutput);

                conversion.OnProgress += (sender, args) =>
                {
                    mergeTask.Value = Math.Min(args.Percent, 100);
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

                mergeTask.Value = 100;
            });

        AnsiConsole.MarkupLine($"[bold green]🎉 加速视频合并完成: {Path.GetFileName(outputFilePath)}[/]");
        AnsiConsole.WriteLine();
    }
}
