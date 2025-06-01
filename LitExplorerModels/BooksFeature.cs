using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class BooksFeature
{
    public int BookId { get; set; }

    public byte[] VectorBlob { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Book Book { get; set; } = null!;
}
