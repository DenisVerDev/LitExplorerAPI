namespace LitExplorerAPI.LitExplorerDTO
{
    public class AuthorDTO
    {
        public int AuthorId { get; set; }

        public string AuthorName { get; set; } = null!;

        public List<BookMetaDTO> BooksMeta { get; set; } = null!;
    }
}
