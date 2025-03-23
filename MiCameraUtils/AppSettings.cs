public class AppSettings
{
    public required string CameraDirectory { get; set; }
    public required string OutputDirectory { get; set; }
    public int ThreadsCount { get; set; }
    [JsonIgnore]
    public MergeMode Mode { get; set; }
    public bool OverwriteOutput { get; set; }

    public required VideoAcceleratorSettings VideoAcceleratorSettings { get; set; }
}

public class VideoAcceleratorSettings
{
    public required string Parameters { get; set; }
}
