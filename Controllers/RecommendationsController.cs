using LitExplorerAPI.LitExplorerDTO;
using LitExplorerAPI.LitExplorerModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LitExplorerAPI.Services;

namespace LitExplorerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecommendationsController : ControllerBase
    {
        private readonly LitExplorerContext litExplorerContext;
        private readonly BooksFeaturesStorage booksFeaturesStorage;

        public RecommendationsController(LitExplorerContext litExplorerContext, BooksFeaturesStorage booksFeaturesStorage)
        {
            this.litExplorerContext = litExplorerContext;
            this.booksFeaturesStorage = booksFeaturesStorage;
        }

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

                var queryFilter = rOptions == RecommendationsOptions.Hottest ? GetHottest(query) : GetBestOfMonth(query);
                
                var queryGroup = queryFilter.GroupBy(bm => bm.BookSource.BookId);
                queryGroup = rOptions switch
                {
                    RecommendationsOptions.Hottest => queryGroup.OrderByDescending(g => g.Max(bm => bm.FirstChapterReleaseDate)),
                    _ => queryGroup.OrderByDescending(g => g.Max(bm => bm.ReadersCount ?? 0))
                };

                // 2nd part
                var bookIds = await queryGroup.Select(g => g.First().BookSource.BookId).Take(count).ToListAsync();
                var booksMeta = new List<BooksMetum>();
                foreach (var id in bookIds)
                {
                    var queryMeta = queryFilter.Where(bm => bm.BookSource.BookId == id);
                    queryMeta = rOptions switch
                    {
                        RecommendationsOptions.Hottest => queryMeta.OrderByDescending(bm=>bm.FirstChapterReleaseDate),
                        _ => queryMeta.OrderByDescending(bm => bm.ReadersCount ?? 0)
                    };

                    var bestMeta = await queryMeta.FirstAsync();
                    booksMeta.Add(bestMeta);

                    var otherMetas = await query.Where(bm => bm.BookSource.BookId == id && bm.BookSourceId != bestMeta.BookSourceId).ToListAsync();
                    booksMeta.AddRange(otherMetas);
                }

                // 3rd part - trasforming into DTO
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
                var userFeaturesList = booksFeaturesStorage.BooksFeatures
                                            .Where(bf => userBookIds.Contains(bf.BookId))
                                            .ToList();
                // Будуємо середній профіль користувача (усереднений вектор)
                float[] userProfileVector = null;
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
                Random random = new Random();
                int skipValue = random.Next(0, booksFeaturesStorage.BooksFeatures.Count - 500 - userBookIds.Count);

                var allFeatures = booksFeaturesStorage.BooksFeatures
                                     .Where(bf => !userBookIds.Contains(bf.BookId)).Skip(skipValue).Take(500)
                                     .ToList();
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
                            .ThenInclude(b => b.Libraries.Where(lib => lib.UserId == userId))
                                .ThenInclude(lib => lib.Status)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.Tags)
                    .Include(bm => bm.BookSource)
                        .ThenInclude(bs => bs.ReadingHistories.Where(rh => rh.UserId == userId))
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

            return query.Where(bm => bm.LastChapterReleaseDate.HasValue && bm.LastChapterReleaseDate.Value >= monthStart);
        }

        private IQueryable<BooksMetum> GetHottest(IQueryable<BooksMetum> query)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            return query.Where(bm => bm.FirstChapterReleaseDate.HasValue && bm.FirstChapterReleaseDate.Value >= monthStart);
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
