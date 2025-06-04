using LitExplorerAPI.LitExplorerModels;
using LitExplorerAPI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<LitExplorerContext>
(
    options => options.UseSqlServer
    (
        builder.Configuration.GetConnectionString("DebugConnection"),
        o=>o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
    )
);
builder.Services.AddSingleton<BooksFeaturesStorage>(serviceProvider =>
{
    using var scope = serviceProvider.CreateScope();
    var scopedContext = scope.ServiceProvider.GetRequiredService<LitExplorerContext>();

    return new BooksFeaturesStorage(scopedContext);
});

var app = builder.Build();

var featuresStorage = app.Services.GetRequiredService<BooksFeaturesStorage>(); // initialize constructor

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
