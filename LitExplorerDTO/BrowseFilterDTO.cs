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

        public List<int>? Tags { get; set; }

        public List<int>? Sources { get; set; }

        public KeyValuePair<double, double>? AverageRatingRange { get; set; }

        public KeyValuePair<int, int>? ChaptersCountRange { get; set; }

        public KeyValuePair<int, int>? ActivityYearRange { get; set; } // range in which novel started/ended or is active

        public SortByOptions SortByOption { get; set; }

        public SortByType SortByType { get; set; }
    }
}
