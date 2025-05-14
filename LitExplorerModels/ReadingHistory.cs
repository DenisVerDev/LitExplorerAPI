using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class ReadingHistory
{
    public int UserId { get; set; }

    public int BookSourceId { get; set; }

    public int? LastReadChapter { get; set; }

    public DateTime? LastReadingUpdateDate { get; set; }

    public virtual BooksSource BookSource { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
