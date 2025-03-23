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
            .GroupBy(d => Path.GetFileName(d).Substring(0, 8)); // 按日期分组

        foreach (var group in directories)
        {
            var date = group.Key;
            var outputFilePath = Path.Combine(appSettings.OutputDirectory, $"{date}.mp4");
            var files = group.SelectMany(directory => Directory.GetFiles(directory, "*.mp4")
                        .OrderBy(f => f))
                        .ToList();

            if (files.Count > 0)
            {
                await Console.Out.WriteLineAsync($"开始合并文件到：{outputFilePath}, 文件信息: {files.Count}");

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

                conversion.OnProgress += async (sender, args) =>
                {
                    await Console.Out.WriteLineAsync($"[{args.Duration}/{args.TotalLength}][{args.Percent}%]");
                };

                await conversion.Start(cancellationToken);

                await Console.Out.WriteLineAsync($"文件合并完成：{outputFilePath}");
            }
        }
    }
}
