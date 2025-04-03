namespace LitExplorerAPI.LitExplorerDTO
{
    public class BookSourceDTO
    {
        public int BookSourceId { get; set; }

        public int BookId { get; set; }

        public int SourceId { get; set; }

        public string SiteUrl { get; set; } = null!;

        public BookMetaDTO BookMeta { get; set; } = null!;

        public List<TagDTO> Tags { get; set; } = null!;
    }
}
