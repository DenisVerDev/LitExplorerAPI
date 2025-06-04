using LitExplorerAPI.LitExplorerModels;

namespace LitExplorerAPI.Services
{
    public class BooksFeaturesStorage
    {
        public List<BooksFeature> BooksFeatures { get; init; }

        public BooksFeaturesStorage(LitExplorerContext litExplorerContext)
        {
            BooksFeatures = litExplorerContext.BooksFeatures.ToList();
            Console.WriteLine("Books features are loaded!");
        }

    }
}
