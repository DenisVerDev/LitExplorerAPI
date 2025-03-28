using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class Book
{
    public int BookId { get; set; }

    public string Title { get; set; } = null!;

    public virtual ICollection<BooksSource> BooksSources { get; set; } = new List<BooksSource>();
}
