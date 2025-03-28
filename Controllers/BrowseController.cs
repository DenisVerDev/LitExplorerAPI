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

        [HttpPost]
        public async Task<IActionResult> BrowseBooks([FromBody] BrowseFilterDTO filter, int page, int count)
        {
            try
            {
                if (filter == null)
                    return NotFound();

                var query = litExplorerContext.BooksMeta
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm => bm.Author)
                    .AsQueryable();

                query = ApplyFilters(query, filter);
                query = ApplySorting(query, filter);

                var booksMeta = await query.Skip(page * count).Take(count).ToListAsync();
                
                var booksDTO = ToBookDTO(booksMeta);
                var authorsDTO = ToAuthorDTO(booksMeta);

                var result = new { Books = booksDTO, Authors = authorsDTO};

                return booksDTO.IsNullOrEmpty() ? NotFound() : Ok(result);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while browsing books", Error = ex.Message });
            }
        }

        private IQueryable<BooksMetum> ApplyFilters(IQueryable<BooksMetum> query, BrowseFilterDTO filter)
        {
            if (!filter.Title.IsNullOrEmpty())
                query = query.Where(bm => bm.BookSource.Book.Title.ToLower().Replace(" ", "").Contains(filter.Title!));

            if (!filter.Tags.IsNullOrEmpty())
                query = query.Where(bm => bm.BookSource.Tags.Any(t => filter.Tags!.Contains(t.TagId)));

            if (!filter.Sources.IsNullOrEmpty())
                query = query.Where(bm => filter.Sources!.Contains(bm.BookSource.SourceId));

            if (filter.AverageRatingRange.HasValue)
            {
                double minRating = filter.AverageRatingRange.Value.Key;
                double maxRating = filter.AverageRatingRange.Value.Value;

                query = query.Where(bm => bm.AverageRating >= minRating && bm.AverageRating <= maxRating);
            }

            if (filter.ChaptersCountRange.HasValue)
            {
                int minChapters = filter.ChaptersCountRange.Value.Key;
                int maxChapters = filter.ChaptersCountRange.Value.Value;
                query = query.Where(bm => bm.ChaptersCount >= minChapters && bm.ChaptersCount <= maxChapters);
            }

            if (filter.ReleaseYearRange.HasValue)
            {
                int minYear = filter.ReleaseYearRange.Value.Key;
                int maxYear = filter.ReleaseYearRange.Value.Value;

                query = query.Where(bm => bm.FirstChapterReleaseDate.HasValue &&
                    bm.FirstChapterReleaseDate.Value.Year >= minYear &&
                    bm.FirstChapterReleaseDate.Value.Year <= maxYear);
            }

            return query;
        }

        private IQueryable<BooksMetum> ApplySorting(IQueryable<BooksMetum> query, BrowseFilterDTO filter)
        {
            switch (filter.SortByOption)
            {
                case SortByOptions.ByPopularity:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => (bm.RatingsCount ?? 0) + (bm.ReadersCount ?? 0));
                    else
                        query = query.OrderBy(bm => (bm.RatingsCount ?? 0) + (bm.ReadersCount ?? 0));
                    break;

                case SortByOptions.ByRating:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => bm.AverageRating);
                    else
                        query = query.OrderBy(bm => bm.AverageRating);
                    break;

                case SortByOptions.ByViews:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => bm.TotalViewsCount);
                    else
                        query = query.OrderBy(bm => bm.TotalViewsCount);
                    break;

                case SortByOptions.ByChapters:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => bm.ChaptersCount);
                    else
                        query = query.OrderBy(bm => bm.ChaptersCount);
                    break;

                case SortByOptions.ByReleaseDate:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => bm.FirstChapterReleaseDate);
                    else
                        query = query.OrderBy(bm => bm.FirstChapterReleaseDate);
                    break;

                case SortByOptions.ByUpdateDate:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => bm.LastChapterReleaseDate);
                    else
                        query = query.OrderBy(bm => bm.LastChapterReleaseDate);
                    break;

                case SortByOptions.ByTitle:
                    if (filter.SortByType == SortByType.DESC)
                        query = query.OrderByDescending(bm => bm.BookSource.Book.Title);
                    else
                        query = query.OrderBy(bm => bm.BookSource.Book.Title);
                    break;

                default: break;
            }

            return query;
        }

        private List<BookDTO>? ToBookDTO(List<BooksMetum> booksMeta)
        {
            try
            {
                var bookDTOs = booksMeta.Select(bm => bm.BookSource.Book).Select(b => new BookDTO
                {
                    BookId = b.BookId,
                    Title = b.Title,
                    BookSources = b.BooksSources.Select(bs => new BookSourceDTO
                    {
                        BookSourceId = bs.BookSourceId,
                        BookId = bs.BookId,
                        SourceId = bs.SourceId,
                        SiteUrl = bs.SiteUrl,
                        BookMeta = new BookMetaDTO
                        {
                            BookSourceId = bs.BooksMetum!.BookSourceId,
                            AuthorId = bs.BooksMetum.AuthorId,
                            Description = bs.BooksMetum.Description,
                            AverageRating = bs.BooksMetum.AverageRating,
                            RatingsCount = bs.BooksMetum.RatingsCount,
                            TotalViewsCount = bs.BooksMetum.TotalViewsCount,
                            ReadersCount = bs.BooksMetum.ReadersCount,
                            ChaptersCount = bs.BooksMetum.ChaptersCount,
                            FirstChapterReleaseDate = bs.BooksMetum.FirstChapterReleaseDate,
                            LastChapterReleaseDate = bs.BooksMetum.LastChapterReleaseDate,
                            CoverImageUrl = bs.BooksMetum.CoverImageUrl
                        },
                        Tags = bs.Tags.Select(t => new TagDTO
                        {
                            TagId = t.TagId,
                            CategoryId = t.CategoryId,
                            TagName = t.TagName
                        }).ToList()
                    }).ToList()
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
