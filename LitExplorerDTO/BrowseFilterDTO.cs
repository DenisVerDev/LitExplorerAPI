namespace LitExplorerAPI.LitExplorerDTO
{
    public enum SortByOptions
    { 
        ByPopularity,   // RatingsCount and ReadersCount
        ByRating,       // AverageRating
        ByViews,        // TotalViewsCount
        ByChapters,     // ChaptersCount
        ByReleaseDate,  // FirstChapterReleaseDate
        ByUpdateDate,   // LastChapterReleaseDate
        ByTitle         // Title alphabetical order (A-Z) or (Z-A)
    }

    public enum SortByType
    { 
        DESC,
        ASC
    }


    public class BrowseFilterDTO
    {
        public string? Title { get; set; }

        public List<int>? Tags { get; set; } // tags ids

        public List<int>? Sources { get; set; } // source ids

        public KeyValuePair<double, double>? AverageRatingRange { get; set; } // range for average rating score

        public KeyValuePair<int, int>? ChaptersCountRange { get; set; } // range for chapters count

        public KeyValuePair<int, int>? ReleaseYearRange { get; set; }

        public SortByOptions SortByOption { get; set; }

        public SortByType SortByType { get; set; }
    }
}
