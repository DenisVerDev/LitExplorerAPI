using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class Tag
{
    public int TagId { get; set; }

    public int CategoryId { get; set; }

    public string TagName { get; set; } = null!;

    public virtual TagsCategory Category { get; set; } = null!;

    public virtual ICollection<BooksSource> BookSources { get; set; } = new List<BooksSource>();
}
