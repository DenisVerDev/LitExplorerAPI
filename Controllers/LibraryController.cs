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

        [HttpPost("updateLibraryStatus")]
        public async Task<IActionResult> UpdateLibraryStatus([FromBody]UserDTO user, int bookId, int? libraryStatus)
        {
            try
            {
                // 1. Verify user exists
                var userExists = await litExplorerContext.Users
                    .AnyAsync(u => u.UserId == user.UserId);
                if (!userExists)
                    return NotFound(new { Message = $"User with ID {user.UserId} not found." });

                // 2. Verify book exists
                var bookExists = await litExplorerContext.Books
                    .AnyAsync(b => b.BookId == bookId);
                if (!bookExists)
                    return NotFound(new { Message = $"Book with ID {bookId} not found." });

                // 3. Try to load existing library entry
                var entry = await litExplorerContext.Libraries
                    .FindAsync(user.UserId, bookId);

                // 4. If status is null => delete entry
                if (libraryStatus == null)
                {
                    if (entry != null)
                    {
                        litExplorerContext.Libraries.Remove(entry);
                        await litExplorerContext.SaveChangesAsync();
                    }
                    return Ok(new { Message = "Removed from library." });
                }

                // 5. Validate the provided status ID
                var statusId = libraryStatus.Value;
                var validStatus = await litExplorerContext.LibraryStatuses
                    .AnyAsync(s => s.StatusId == statusId);
                if (!validStatus)
                    return BadRequest(new { Message = $"Library status ID {statusId} is invalid." });

                // 6. Create or update
                if (entry == null)
                {
                    // new entry; AddedDate will default via SQL GETDATE()
                    entry = new Library
                    {
                        UserId = user.UserId,
                        BookId = bookId,
                        StatusId = statusId,
                        LastStatusUpdateDate = DateTime.UtcNow
                    };
                    litExplorerContext.Libraries.Add(entry);
                }
                else
                {
                    entry.StatusId = statusId;
                    entry.LastStatusUpdateDate = DateTime.UtcNow;
                    litExplorerContext.Libraries.Update(entry);
                }

                await litExplorerContext.SaveChangesAsync();
                return Ok(new { Message = "Library status updated." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while changing book's library status", Error = ex.Message });
            }
        }

        [HttpPost("updateReadingHistory")]
        public async Task<IActionResult> UpdateReadingHistory([FromBody] UserDTO user, int bookSourceId, int? lastReadChapter)
        {
            try
            {
                // 1. Verify the user exists
                var userExists = await litExplorerContext.Users
                    .AnyAsync(u => u.UserId == user.UserId);
                if (!userExists)
                    return NotFound(new { Message = $"User with ID {user.UserId} not found." });

                // 2. Verify the book-source exists
                var source = await litExplorerContext.BooksSources
                    .Where(bs => bs.BookSourceId == bookSourceId).FirstOrDefaultAsync();
                if (source == null)
                    return NotFound(new { Message = $"BookSource with ID {bookSourceId} not found." });

                // 3. Verify if books is in personal library
                var library = await litExplorerContext.Libraries.FindAsync(user.UserId, source.BookId);
                if (library == null)
                    return NotFound(new { Message = $"Book with ID {source.BookId} is not part of user's library." });

                // 4. Try to load an existing history entry (PK is (UserId, BookSourceId))
                var history = await litExplorerContext.ReadingHistories
                    .FindAsync(user.UserId, bookSourceId);

                if (history == null)
                {
                    // 4a. No previous entry: create it
                    history = new ReadingHistory
                    {
                        UserId = user.UserId,
                        BookSourceId = bookSourceId,
                        LastReadChapter = lastReadChapter,
                        LastReadingUpdateDate = DateTime.UtcNow
                    };
                    litExplorerContext.ReadingHistories.Add(history);
                }
                else
                {
                    // 4b. Existing entry: update it
                    history.LastReadChapter = lastReadChapter;
                    history.LastReadingUpdateDate = DateTime.UtcNow;
                    litExplorerContext.ReadingHistories.Update(history);
                }

                // 5. Persist
                await litExplorerContext.SaveChangesAsync();
                return Ok(new { Message = "Reading history updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while updating book's reading history", Error = ex.Message });
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
