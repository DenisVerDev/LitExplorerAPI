namespace LitExplorerAPI.LitExplorerDTO
{
    public class SourceDTO
    {
        public int SourceId { get; set; }

        public string SourceName { get; set; } = null!;

        public string HomePageUrl { get; set; } = null!;

        public byte[]? Icon { get; set; }
    }
}
