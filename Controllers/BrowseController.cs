using LitExplorerAPI.LitExplorerDTO;
using LitExplorerAPI.LitExplorerModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LitExplorerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrowseController : ControllerBase
    {
        private readonly LitExplorerContext litExplorerContext;

        public BrowseController(LitExplorerContext litExplorerContext)
            => this.litExplorerContext = litExplorerContext;

        [HttpPost("pages")]
        public async Task<IActionResult> GetPagesCount([FromBody] BrowseFilterDTO filter, int pageSize)
        {
            try
            {
                if (filter == null)
                    return NotFound();

                if (pageSize <= 0)
                    throw new Exception("pageSize should be better than 0");

                var query = litExplorerContext.BooksMeta
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .GroupBy(bm => bm.BookSource.BookId)
                    .AsQueryable();

                query = ApplyFilters(query, filter);

                int totalBooks = await query.CountAsync();
                int result = totalBooks / pageSize;

                if (result > 0 && result * pageSize < totalBooks)
                    result++;

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving number of pages", Error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BrowseBooks([FromBody] BrowseFilterDTO filter, int page, int count, int userId)
        {
            try
            {
                if (filter == null)
                    return NotFound();

                // 1st part - filtering candidates
                var query = litExplorerContext.BooksMeta
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                            .ThenInclude(b => b.Libraries.Where(lib => lib.UserId == userId))
                                .ThenInclude(lib => lib.Status)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.ReadingHistories.Where(rh => rh.UserId == userId))
                    .Include(bm => bm.Author)
                    .AsQueryable();

                var queryFilter = ApplyFilters(query, filter);

                var queryGroup = queryFilter.GroupBy(bm => bm.BookSource.BookId);
                queryGroup = ApplySorting(queryGroup, filter);

                // 2nd part
                var bookIds = await queryGroup.Select(g => g.First().BookSource.BookId).Skip(page*count).Take(count).ToListAsync();
                var booksMeta = new List<BooksMetum>();
                foreach (var id in bookIds)
                {
                    // 1st - find the first BooksMeta that satisfies all filters and sorting
                    var queryMeta = queryFilter.Where(bm => bm.BookSource.BookId == id);
                    queryMeta = ApplySorting(queryMeta, filter);

                    // 2nd - add this BoosMeta to booksMeta list
                    var bestMeta = await queryMeta.FirstAsync();
                    booksMeta.Add(bestMeta);

                    // 3rd - finde others relevant BooksMeta and add them to booksMeta list
                    var otherMetas = await query.Where(bm => bm.BookSource.BookId == id && bm.BookSourceId != bestMeta.BookSourceId).ToListAsync();
                    booksMeta.AddRange(otherMetas);
                }

                // 3rd part - trasforming into DTO
                var booksDTO = ToBookDTO(booksMeta);
                var authorsDTO = ToAuthorDTO(booksMeta);

                var result = new { Books = booksDTO, Authors = authorsDTO};

                return booksDTO.IsNullOrEmpty() || authorsDTO.IsNullOrEmpty() ? NotFound() : Ok(result);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while browsing books", Error = ex.Message });
            }
        }

        private IQueryable<IGrouping<int, BooksMetum>> ApplyFilters(IQueryable<IGrouping<int, BooksMetum>> query, BrowseFilterDTO filter)
        {
            if (!filter.Title.IsNullOrEmpty())
                query = query.Where(g => g.Any(bm => bm.BookSource.Book.Title.ToLower().Replace(" ", "").Contains(filter.Title!)));

            if (!filter.Tags.IsNullOrEmpty())
            {
                var filterTagsCategorised = litExplorerContext.Tags
                    .Where(t => filter.Tags!.Contains(t.TagId))
                    .GroupBy(t => t.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Tags = g.Select(t => t.TagId).ToList()
                    }).ToList();

                foreach (var category in filterTagsCategorised)
                {
                    if (category.CategoryId < 6)
                        query = query.Where(g => g.Any(bm => category.Tags.All(ft => bm.BookSource.Tags.Any(t => t.CategoryId == category.CategoryId && t.TagId == ft))));
                    else
                        query = query.Where(g => g.Any(bm => category.Tags.Any(ft => bm.BookSource.Tags.Any(t => t.CategoryId == category.CategoryId && t.TagId == ft))));
                }
            }

            if (!filter.Sources.IsNullOrEmpty())
                query = query.Where(g => g.Any(bm => filter.Sources!.Contains(bm.BookSource.SourceId)));

            // AverageRatingRange segment
            if (filter.AverageRatingRange.Key.HasValue) // minRating condition
                query = query.Where(g => g.Any(bm => bm.AverageRating >= filter.AverageRatingRange.Key.Value));

            if (filter.AverageRatingRange.Value.HasValue) // maxRating condition
                query = query.Where(g => g.Any(bm => bm.AverageRating <= filter.AverageRatingRange.Value.Value));

            // ChaptersCountRange segment
            if (filter.ChaptersCountRange.Key.HasValue) // min number of chapters condition
                query = query.Where(g => g.Any(bm => bm.ChaptersCount >= filter.ChaptersCountRange.Key.Value));

            if (filter.ChaptersCountRange.Value.HasValue) // max number of chapters condition
                query = query.Where(g => g.Any(bm => bm.ChaptersCount <= filter.ChaptersCountRange.Value.Value));

            // ActivityYearRange segment
            if (filter.ActivityYearRange.Key.HasValue) // min year condition
                query = query.Where(g => g.Any(bm => bm.LastChapterReleaseDate!.Value.Year >= filter.ActivityYearRange.Key.Value));

            if (filter.ActivityYearRange.Value.HasValue) // max year condition
                query = query.Where(g => g.Any(bm => bm.LastChapterReleaseDate!.Value.Year <= filter.ActivityYearRange.Value.Value));

            return query;
        }

        private IQueryable<BooksMetum> ApplyFilters(IQueryable<BooksMetum> query, BrowseFilterDTO filter)
        {
            if (!filter.Title.IsNullOrEmpty())
                query = query.Where(bm => bm.BookSource.Book.Title.ToLower().Replace(" ", "").Contains(filter.Title!));

            if (!filter.Tags.IsNullOrEmpty())
            {
                var filterTagsCategorised = litExplorerContext.Tags
                    .Where(t => filter.Tags!.Contains(t.TagId))
                    .GroupBy(t => t.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Tags = g.Select(t => t.TagId).ToList()
                    }).ToList();

                foreach (var category in filterTagsCategorised)
                {
                    if (category.CategoryId < 6)
                        query = query.Where(bm => category.Tags.All(ft => bm.BookSource.Tags.Any(t => t.CategoryId == category.CategoryId && t.TagId == ft)));
                    else
                        query = query.Where(bm => category.Tags.Any(ft => bm.BookSource.Tags.Any(t => t.CategoryId == category.CategoryId && t.TagId == ft)));
                }
            }

            if (!filter.Sources.IsNullOrEmpty())
                query = query.Where(bm => filter.Sources!.Contains(bm.BookSource.SourceId));

            // AverageRatingRange segment
            if (filter.AverageRatingRange.Key.HasValue) // minRating condition
                query = query.Where(bm => bm.AverageRating >= filter.AverageRatingRange.Key.Value);

            if (filter.AverageRatingRange.Value.HasValue) // maxRating condition
                query = query.Where(bm => bm.AverageRating <= filter.AverageRatingRange.Value.Value);

            // ChaptersCountRange segment
            if (filter.ChaptersCountRange.Key.HasValue) // min number of chapters condition
                query = query.Where(bm => bm.ChaptersCount >= filter.ChaptersCountRange.Key.Value);

            if (filter.ChaptersCountRange.Value.HasValue) // max number of chapters condition
                query = query.Where(bm => bm.ChaptersCount <= filter.ChaptersCountRange.Value.Value);

            // ActivityYearRange segment
            if (filter.ActivityYearRange.Key.HasValue) // min year condition
                query = query.Where(bm => bm.LastChapterReleaseDate!.Value.Year >= filter.ActivityYearRange.Key.Value);

            if (filter.ActivityYearRange.Value.HasValue) // max year condition
                query = query.Where(bm => bm.LastChapterReleaseDate!.Value.Year <= filter.ActivityYearRange.Value.Value);

            return query;
        }

        private IQueryable<IGrouping<int, BooksMetum>> ApplySorting(IQueryable<IGrouping<int, BooksMetum>> query, BrowseFilterDTO filter)
        {
            switch (filter.SortByOption)
            {
                case SortByOptions.ByPopularity:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g=>g.Max(bm => bm.ReadersCount ?? 0));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.ReadersCount ?? 0));
                    break;

                case SortByOptions.ByRating:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g => g.Max(bm=>bm.AverageRating));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.AverageRating));
                    break;

                case SortByOptions.ByViews:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g => g.Max(bm => bm.TotalViewsCount));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.TotalViewsCount));
                    break;

                case SortByOptions.ByChapters:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g => g.Max(bm => bm.ChaptersCount));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.ChaptersCount));
                    break;

                case SortByOptions.ByReleaseDate:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g => g.Max(bm => bm.FirstChapterReleaseDate));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.FirstChapterReleaseDate));
                    break;

                case SortByOptions.ByUpdateDate:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g => g.Max(bm => bm.LastChapterReleaseDate));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.LastChapterReleaseDate));
                    break;

                case SortByOptions.ByTitle:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(g => g.Max(bm=>bm.BookSource.Book.Title));
                    else
                        query = query.OrderBy(g => g.Max(bm => bm.BookSource.Book.Title));
                    break;

                default: break;
            }

            return query;
        }

        private IQueryable<BooksMetum> ApplySorting(IQueryable<BooksMetum> query, BrowseFilterDTO filter)
        {
            switch (filter.SortByOption)
            {
                case SortByOptions.ByPopularity:
                    query = query.OrderByDescending(bm => bm.ReadersCount ?? 0);
                    break;

                case SortByOptions.ByRating:
                    query = query.OrderByDescending(bm => bm.AverageRating);
                    break;

                case SortByOptions.ByViews:
                    query = query.OrderByDescending(bm => bm.TotalViewsCount);
                    break;

                case SortByOptions.ByChapters:
                    query = query.OrderByDescending(bm => bm.ChaptersCount);
                    break;

                case SortByOptions.ByReleaseDate:
                    query = query.OrderByDescending(bm => bm.FirstChapterReleaseDate);
                    break;

                case SortByOptions.ByUpdateDate:
                    query = query.OrderByDescending(bm => bm.LastChapterReleaseDate);
                    break;

                case SortByOptions.ByTitle:
                    query = query.OrderByDescending(bm => bm.BookSource.Book.Title);
                    break;

                default: break;
            }

            return query;
        }

        private List<BookDTO>? ToBookDTO(List<BooksMetum> booksMeta)
        {
            try
            {
                var grouped = booksMeta.GroupBy(bm => bm.BookSource.Book.BookId);

                var bookDTOs = grouped.Select(g =>
                {
                    var book = g.First().BookSource.Book;

                    return new BookDTO
                    {
                        BookId = book.BookId,
                        Title = book.Title,
                        LibraryStatus = book.Libraries.Select(lib => new LibraryStatusDTO
                        {
                            StatusId = lib.StatusId,
                            StatusName = lib.Status.StatusName
                        }).FirstOrDefault(),
                        BookSources = g.Select(bm =>
                        {
                            var bs = bm.BookSource;
                            return new BookSourceDTO
                            {
                                BookSourceId = bs.BookSourceId,
                                BookId = bs.BookId,
                                SourceId = bs.SourceId,
                                SiteUrl = bs.SiteUrl,
                                LastReadChapter = bs.ReadingHistories.Select(rh => rh.LastReadChapter).FirstOrDefault(),
                                LastReadingUpdateDate = bs.ReadingHistories.Select(rh => rh.LastReadingUpdateDate).FirstOrDefault(),
                                BookMeta = new BookMetaDTO
                                {
                                    BookSourceId = bm.BookSourceId,
                                    AuthorId = bm.AuthorId,
                                    Description = bm.Description,
                                    AverageRating = bm.AverageRating,
                                    RatingsCount = bm.RatingsCount,
                                    TotalViewsCount = bm.TotalViewsCount,
                                    ReadersCount = bm.ReadersCount,
                                    ChaptersCount = bm.ChaptersCount,
                                    FirstChapterReleaseDate = bm.FirstChapterReleaseDate,
                                    LastChapterReleaseDate = bm.LastChapterReleaseDate,
                                    CoverImageUrl = bm.CoverImageUrl
                                },
                                Tags = bs.Tags.Select(t => new TagDTO
                                {
                                    TagId = t.TagId,
                                    CategoryId = t.CategoryId,
                                    TagName = t.TagName
                                }).ToList()
                            };
                        }).ToList()
                    };
                }).ToList();

                return bookDTOs;
            }
            catch
            {
                return null;
            }
        }

        private List<AuthorDTO>? ToAuthorDTO(List<BooksMetum> booksMeta)
        {
            try
            {
                var authorsDTO = booksMeta.Select(bm => new AuthorDTO
                {
                    AuthorId = bm.Author.AuthorId,
                    AuthorName = bm.Author.AuthorName
                }).DistinctBy(a=>a.AuthorId).ToList();

                return authorsDTO;
            }
            catch
            {
                return null;
            }
        }
    }
}
