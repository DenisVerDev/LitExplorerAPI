using LitExplorerAPI.LitExplorerDTO;
using LitExplorerAPI.LitExplorerModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LitExplorerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private readonly LitExplorerContext litExplorerContext;

        public LibraryController(LitExplorerContext litExplorerContext)
            => this.litExplorerContext = litExplorerContext;

        [HttpGet]
        public async Task<IActionResult> InspectLibrary(int userId, int page, int count)
        {
            try
            {
                var query = litExplorerContext.BooksMeta
                    .Include(bm=>bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                            .ThenInclude(b=>b.Libraries)
                                .ThenInclude(lib=>lib.Status)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm=> bm.BookSource)
                        .ThenInclude(bs => bs.ReadingHistories)
                    .Include(bm => bm.Author)
                    .Where(bm=> litExplorerContext.Libraries.Any(lib=> lib.UserId == userId && lib.BookId == bm.BookSource.BookId))
                    .AsQueryable();

                query = query.OrderByDescending(bm => bm.LastChapterReleaseDate);

                var booksMeta = await query.Skip(page * count).Take(count).ToListAsync();

                HashSet<int> seen = new HashSet<int>();
                booksMeta.RemoveAll(x => !seen.Add(x.BookSource.BookId));

                var booksDTO = ToBookDTO(booksMeta);
                var authorsDTO = ToAuthorDTO(booksMeta);

                var result = new { Books = booksDTO, Authors = authorsDTO };

                return booksDTO.IsNullOrEmpty() || authorsDTO.IsNullOrEmpty() ? NotFound() : Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while inspecting user's library", Error = ex.Message });
            }
        }

        private List<BookDTO>? ToBookDTO(List<BooksMetum> booksMeta)
        {
            try
            {
                var bookDTOs = booksMeta.Select(bm => bm.BookSource.Book).Select(b => new BookDTO
                {
                    BookId = b.BookId,
                    Title = b.Title,
                    LibraryStatus = b.Libraries.Select(lib=> new LibraryStatusDTO 
                    { 
                        StatusId = lib.StatusId,
                        StatusName = lib.Status.StatusName
                    }).FirstOrDefault(),
                    BookSources = b.BooksSources.Select(bs => new BookSourceDTO
                    {
                        BookSourceId = bs.BookSourceId,
                        BookId = bs.BookId,
                        SourceId = bs.SourceId,
                        SiteUrl = bs.SiteUrl,
                        LastReadChapter = bs.ReadingHistories.Select(bs => bs.LastReadChapter).FirstOrDefault(),
                        LastReadingUpdateDate = bs.ReadingHistories.Select(bs => bs.LastReadingUpdateDate).FirstOrDefault(),
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
