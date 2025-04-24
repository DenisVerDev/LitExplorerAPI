using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class LibraryStatus
{
    public int StatusId { get; set; }

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Library> Libraries { get; set; } = new List<Library>();
}
