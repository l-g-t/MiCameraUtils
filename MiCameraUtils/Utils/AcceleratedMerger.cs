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

        if (files.Count > 1)
        {
            var mergedFileName = $"{Path.GetFileNameWithoutExtension(files[0].Name)}_To_{Path.GetFileNameWithoutExtension(files[^1].Name)}";
            var outputFilePath = Path.Combine(appSettings.OutputDirectory, mergedFileName + ".mp4");

            var conversion = FFmpeg.Conversions.New();

            // 创建一个临时文件列表
            var tempFileList = Path.Combine(appSettings.OutputDirectory, $"{mergedFileName}_filelist.txt");
            using (StreamWriter writer = new(tempFileList))
            {
                foreach (var file in files)
                {
                    writer.WriteLine($"file '{file}'");
                }
            }

            conversion.AddParameter($"-f concat -safe 0 -i \"{tempFileList}\" -c copy \"{outputFilePath}\"");
            conversion.UseMultiThread(appSettings.ThreadsCount);
            conversion.SetOverwriteOutput(appSettings.OverwriteOutput);

            conversion.OnProgress += async (sender, args) =>
            {
                await Console.Out.WriteLineAsync($"[{args.Duration}/{args.TotalLength}][{args.Percent}%]");
            };

            await conversion.Start(cancellationToken);

            await Console.Out.WriteLineAsync($"文件合并完成：{outputFilePath}");
        }
    }
}
