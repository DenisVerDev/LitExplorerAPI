using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class TagsCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
