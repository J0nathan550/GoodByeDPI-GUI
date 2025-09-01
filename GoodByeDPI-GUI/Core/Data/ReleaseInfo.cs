namespace GoodByeDPI_GUI.Core.Data
{
    public class ReleaseInfo
    {
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public string Version { get; set; }
        public long Size { get; set; }
        public bool IsPreRelease { get; set; }
    }
}