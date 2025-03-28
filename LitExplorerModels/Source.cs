using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class Source
{
    public int SourceId { get; set; }

    public string SourceName { get; set; } = null!;

    public string HomePageUrl { get; set; } = null!;

    public byte[]? Icon { get; set; }

    public virtual ICollection<BooksSource> BooksSources { get; set; } = new List<BooksSource>();
}
