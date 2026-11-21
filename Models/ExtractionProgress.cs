namespace KMRLauncherMvvm.Models;

public class ExtractionProgress
{
    public double Percentage { get; set; }
    public string? CurrentFile { get; set; }
    public int FilesDone { get; set; }
    public int FilesTotal { get; set; }
    public long BytesWritten { get; set; }
    public long BytesTotal { get; set; }
}
