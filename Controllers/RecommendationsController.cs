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
    public class RecommendationsController : ControllerBase
    {
        private readonly LitExplorerContext litExplorerContext;

        public RecommendationsController(LitExplorerContext litExplorerContext)
            => this.litExplorerContext = litExplorerContext;

        [HttpPost]
        public async Task<IActionResult> RecommendBooks([FromBody] UserDTO? userDTO, RecommendationsOptions rOptions, int count)
        {
            try
            {
                var userId = userDTO == null ? 0 : userDTO.UserId;

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

                switch (rOptions)
                {
                    case RecommendationsOptions.BestOfMonth:
                        query = GetBestOfMonth(query);
                        break;

                    case RecommendationsOptions.Hottest:
                        query = GetHottest(query);
                        break;

                    case RecommendationsOptions.Personal:
                        query = GetPersonal(query, userDTO);
                        break;

                    default: throw new Exception("There is no such supported recommendation option!");
                }

                // 2nd part - picking results
                query = query.Take(count * 2); // 2 is number of sources in database. Duplicate metadata of the same books is possible
                var books = await query.Select(bm => bm.BookSource.BookId).Distinct().Take(count).ToListAsync();
                var unsortedBooksMeta = await litExplorerContext.BooksMeta
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                            .ThenInclude(b => b.Libraries.Where(lib => lib.UserId == userId))
                                .ThenInclude(lib => lib.Status)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.ReadingHistories.Where(rh => rh.UserId == userId))
                    .Include(bm => bm.Author)
                    .Where(bm => books.Any(x => x == bm.BookSource.BookId))
                    .ToListAsync();

                var sortedBooksMeta = books.SelectMany(b => unsortedBooksMeta.Where(bm => bm.BookSource.BookId == b)).ToList();

                // 3rd part - trasforming into DTO
                var booksDTO = ToBookDTO(sortedBooksMeta);
                var authorsDTO = ToAuthorDTO(sortedBooksMeta);

                var result = new { Books = booksDTO, Authors = authorsDTO };

                return booksDTO.IsNullOrEmpty() || authorsDTO.IsNullOrEmpty() ? NotFound() : Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while recommending books", Error = ex.Message });
            }
        }

        private IQueryable<BooksMetum> GetBestOfMonth(IQueryable<BooksMetum> query)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            query = query.Where(bm => bm.LastChapterReleaseDate.HasValue && bm.LastChapterReleaseDate.Value >= monthStart)
                         .OrderByDescending(bm => (bm.RatingsCount ?? 0) + (bm.ReadersCount ?? 0));

            return query;
        }

        private IQueryable<BooksMetum> GetHottest(IQueryable<BooksMetum> query)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            query = query.Where(bm => bm.FirstChapterReleaseDate.HasValue && bm.FirstChapterReleaseDate.Value >= monthStart)
                         .OrderByDescending(bm => bm.FirstChapterReleaseDate);

            return query;
        }

        private IQueryable<BooksMetum> GetPersonal(IQueryable<BooksMetum> query, UserDTO? userDTO)
        {
            return query; // stand in code
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
                }).DistinctBy(a => a.AuthorId).ToList();

                return authorsDTO;
            }
            catch
            {
                return null;
            }
        }
    }
}
