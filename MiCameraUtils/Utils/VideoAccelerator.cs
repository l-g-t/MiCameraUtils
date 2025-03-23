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

        foreach (var file in files)
        {
            var outputFilePath = Path.Combine(appSettings.OutputDirectory, $"{Path.GetFileNameWithoutExtension(file.Name)}_Accelerate.mp4");
            await Console.Out.WriteLineAsync($"加速视频：{outputFilePath}");

            var conversion = FFmpeg.Conversions.New();

            // setpts=0.01*PTS fast forward 100x
            // -r 30 -g 60 to unify the frame rate and key frame
            // -hwaccel cuda -c:v hevc_cuvid -c:v hevc_nvenc for hardware acceleration
            // $"-hwaccel cuda -c:v hevc_cuvid -i \"{file.FullName}\" -vf \"setpts=0.01*PTS\" -r 30 -g 60 -preset p1 -an -c:v hevc_nvenc \"{outputFilePath}\""
            conversion.AddParameter(string.Format(appSettings.VideoAcceleratorSettings.Parameters, file.FullName, outputFilePath));
            conversion.UseMultiThread(appSettings.ThreadsCount);
            conversion.SetOverwriteOutput(appSettings.OverwriteOutput);

            conversion.OnProgress += async (sender, args) =>
            {
                await Console.Out.WriteLineAsync($"[{args.Duration}/{args.TotalLength}][{args.Percent}%]");
            };

            await conversion.Start(cancellationToken);

            await Console.Out.WriteLineAsync($"加速完成：{outputFilePath}");
        }
    }
}
