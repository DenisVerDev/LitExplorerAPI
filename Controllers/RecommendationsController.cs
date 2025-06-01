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
                if (rOptions == RecommendationsOptions.Personal)
                    return await GetPersonalRecommendations(userDTO, count);

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

        private async Task<IActionResult> GetPersonalRecommendations([FromBody] UserDTO? userDTO, int count)
        {
            try
            {
                if (userDTO == null || userDTO.UserId == 0)
                    throw new Exception("Received user's data was null");

                int userId = userDTO.UserId;
                // 1. Отримуємо всі BookId, які користувач читав або додав у бібліотеку
                var userBookIds = await litExplorerContext.Libraries
                                       .Where(l => l.UserId == userId)
                                       .Select(l => l.BookId)
                                       .ToListAsync();
                // Додаємо книги з історії читання (BookSourceId -> BookId)
                var historyBookIds = await litExplorerContext.ReadingHistories
                                       .Where(rh => rh.UserId == userId)
                                       .Select(rh => rh.BookSource.BookId)
                                       .ToListAsync();
                userBookIds.AddRange(historyBookIds);
                userBookIds = userBookIds.Distinct().ToList();

                if (userBookIds.Count == 0)
                    throw new Exception("User doesn't have any previous interactions with books");

                // 2. Вибираємо вектори ознак для книг користувача з кешу BooksFeatures
                var userFeaturesList = await litExplorerContext.BooksFeatures
                                            .Where(bf => userBookIds.Contains(bf.BookId))
                                            .ToListAsync();
                // Будуємо середній профіль користувача (усереднений вектор)
                float[] userProfileVector = new float[0];
                int vectorLength = 0;
                if (userFeaturesList.Count > 0)
                {
                    // Ініціалізуємо профіль нульовим вектором потрібної довжини
                    vectorLength = userFeaturesList[0].VectorBlob.Length / sizeof(float);
                    userProfileVector = new float[vectorLength];
                }
                foreach (var bf in userFeaturesList)
                {
                    // Десеріалізуємо вектор книги з байтів
                    int len = bf.VectorBlob.Length / sizeof(float);
                    float[] bookVector = new float[len];
                    Buffer.BlockCopy(bf.VectorBlob, 0, bookVector, 0, bf.VectorBlob.Length);
                    // Додаємо до профілю (поелементно)
                    for (int i = 0; i < len; i++)
                    {
                        userProfileVector[i] += bookVector[i];
                    }
                }
                // Усереднюємо суму ознак профілю
                for (int i = 0; i < userProfileVector.Length; i++)
                {
                    userProfileVector[i] /= userFeaturesList.Count;
                }

                // 3. Обчислюємо косинусну схожість профілю з усіма іншими книгами:contentReference[oaicite:11]{index=11}
                var allFeatures = await litExplorerContext.BooksFeatures
                                     .Where(bf => !userBookIds.Contains(bf.BookId)).Take(500)
                                     .ToListAsync();
                // Змінні для топ-рекомендацій
                var topRecommendations = new List<(int BookId, double CosineSimilarity)>();

                // Попередньо обчислюємо норму профілю для косинусного подібності
                double profileNorm = 0.0;
                for (int i = 0; i < userProfileVector.Length; i++)
                    profileNorm += userProfileVector[i] * userProfileVector[i];
                profileNorm = Math.Sqrt(profileNorm);

                foreach (var bf in allFeatures)
                {
                    // Десеріалізуємо вектор книги
                    int len = bf.VectorBlob.Length / sizeof(float);
                    float[] bookVector = new float[len];
                    Buffer.BlockCopy(bf.VectorBlob, 0, bookVector, 0, bf.VectorBlob.Length);

                    // Обчислюємо косинус(userProfile, bookVector) = (A·B) / (||A||*||B||)
                    double dot = 0.0;
                    double bookNorm = 0.0;
                    for (int i = 0; i < len; i++)
                    {
                        dot += userProfileVector[i] * bookVector[i];
                        bookNorm += bookVector[i] * bookVector[i];
                    }
                    if (bookNorm == 0 || profileNorm == 0)
                    {
                        continue; // пропускаємо книги без даних або порожній профіль
                    }
                    bookNorm = Math.Sqrt(bookNorm);
                    double cosine = dot / (profileNorm * bookNorm);
                    topRecommendations.Add((bf.BookId, cosine));
                }

                // 4. Сортуємо книги за спаданням схожості і беремо топ-N
                var recommendedIds = topRecommendations
                                        .OrderByDescending(x => x.CosineSimilarity)
                                        .Take(count)    // кількість рекомендацій
                                        .Select(x => x.BookId)
                                        .ToList();

                if (recommendedIds.Count == 0)
                    throw new Exception("Can't find suitable recommendations");

                // 5. Завантажуємо метадані для рекомендованих книг і формуємо відповідь
                var recommendedMeta = await litExplorerContext.BooksMeta
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Book)
                            .ThenInclude(b => b.Libraries)
                                .ThenInclude(lib => lib.Status)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.ReadingHistories)
                    .Include(bm => bm.Author)
                    .Where(bm => recommendedIds.Contains(bm.BookSource.BookId))
                    .ToListAsync();

                // Розміщуємо BooksMeta у порядку рекомендованих BookId
                var sortedBooksMeta = recommendedIds
                    .SelectMany(id => recommendedMeta.Where(bm => bm.BookSource.BookId == id))
                    .ToList();

                // Трансформуємо в DTO для відповіді
                var booksDTO = ToBookDTO(sortedBooksMeta);
                var authorsDTO = ToAuthorDTO(sortedBooksMeta);
                var result = new { Books = booksDTO, Authors = authorsDTO };
                return Ok(result);
            }
            catch (Exception ex)
            {
                //return StatusCode(500, new { Message = "An error occurred while recommending books", Error = ex.Message });
                return await RecommendBooks(userDTO, RecommendationsOptions.BestOfMonth, count);
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
