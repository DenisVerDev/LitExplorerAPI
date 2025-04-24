using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class Library
{
    public int UserId { get; set; }

    public int BookSourceId { get; set; }

    public int StatusId { get; set; }

    public DateTime AddedDate { get; set; }

    public DateTime? LastUpdateDate { get; set; }

    public int? LastReadChapter { get; set; }

    public virtual BooksSource BookSource { get; set; } = null!;

    public virtual LibraryStatus Status { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
