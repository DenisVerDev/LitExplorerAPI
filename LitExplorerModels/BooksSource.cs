using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class BooksSource
{
    public int BookSourceId { get; set; }

    public int BookId { get; set; }

    public int SourceId { get; set; }

    public string SiteUrl { get; set; } = null!;

    public virtual Book Book { get; set; } = null!;

    public virtual BooksMetum? BooksMetum { get; set; }

    public virtual ICollection<Library> Libraries { get; set; } = new List<Library>();

    public virtual Source Source { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
