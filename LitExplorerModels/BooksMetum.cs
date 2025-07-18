using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class BooksMetum
{
    public int BookSourceId { get; set; }

    public int AuthorId { get; set; }

    public string? Description { get; set; }

    public double? AverageRating { get; set; }

    public int? RatingsCount { get; set; }

    public int? TotalViewsCount { get; set; }

    public int? ReadersCount { get; set; }

    public int? ChaptersCount { get; set; }

    public DateTime? FirstChapterReleaseDate { get; set; }

    public DateTime? LastChapterReleaseDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public virtual Author Author { get; set; } = null!;

    public virtual BooksSource BookSource { get; set; } = null!;
}
