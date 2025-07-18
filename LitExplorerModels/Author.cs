using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class Author
{
    public int AuthorId { get; set; }

    public string AuthorName { get; set; } = null!;

    public virtual ICollection<BooksMetum> BooksMeta { get; set; } = new List<BooksMetum>();
}
