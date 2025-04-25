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
                var query = litExplorerContext.BooksMeta
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm => bm.Author)
                    .AsQueryable();

                switch(rOptions)
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

                var booksMeta = await query.Take(count).ToListAsync();

                var booksDTO = ToBookDTO(booksMeta);
                var authorsDTO = ToAuthorDTO(booksMeta);

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
