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
    public class MetadataController : ControllerBase
    {
        private LitExplorerContext litExplorerContext;

        public MetadataController(LitExplorerContext litExplorerContext)
            => this.litExplorerContext = litExplorerContext;

        [HttpGet("tags")]
        public async Task<IActionResult> GetTags()
        {
            try
            {
                var tags = await litExplorerContext.Tags.ToListAsync();

                var tagsDTO = tags.Select(t => new TagDTO
                {
                    TagId = t.TagId,
                    CategoryId = t.CategoryId,
                    TagName = t.TagName
                }).ToList();

                return tagsDTO.IsNullOrEmpty() ? NotFound() : Ok(tagsDTO);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving all tags", Error = ex.Message });
            }
        }

        [HttpGet("tagsCategories")]
        public async Task<IActionResult> GetTagsCategories(bool tags)
        {
            try
            {
                var query = litExplorerContext.TagsCategories.AsQueryable();
                if (tags) query = query.Include(ts => ts.Tags);

                var tagsCategories = await query.ToListAsync();

                var tagsCategoriesDTO = tagsCategories.Select(tc => new TagsCategoryDTO
                {
                    CategoryId = tc.CategoryId,
                    CategoryName = tc.CategoryName,
                    Tags = tags ? tc.Tags.Select(t => new TagDTO
                    {
                        TagId = t.TagId,
                        CategoryId = t.CategoryId,
                        TagName = t.TagName
                    }).ToList() : null
                }).ToList();

                return tagsCategoriesDTO.IsNullOrEmpty() ? NotFound() : Ok(tagsCategoriesDTO);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving all categories", Error = ex.Message });
            }
        }

        [HttpGet("sources")]
        public async Task<IActionResult> GetSources()
        {
            try
            {
                var sources = await litExplorerContext.Sources.ToListAsync();

                var sourcesDTO = sources.Select(tc => new SourceDTO
                { 
                    SourceId = tc.SourceId,
                    SourceName = tc.SourceName,
                    HomePageUrl = tc.HomePageUrl,
                    IconUrl = tc.IconUrl
                }).ToList();

                return sourcesDTO.IsNullOrEmpty() ? NotFound() : Ok(sourcesDTO);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving all sources", Error = ex.Message });
            }
        }
    }
}
