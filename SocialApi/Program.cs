using Microsoft.AspNetCore.Mvc;
using SocialApi.Caching;

var builder = WebApplication.CreateBuilder(args);

// Register Singleton Services
builder.Services.AddSingleton<ICache<string, List<string>>>(new LRUCache<string, List<string>>(1000));
builder.Services.AddSingleton<RequestCoalescer<string, List<string>>>();

var app = builder.Build();

// Mock DB Methods
async Task<List<string>> DbGetFeedAsync(string userId)
{
    await Task.Delay(500); // Simulate expensive 500ms DB call
    return new List<string> { $"Post 1 by {userId}", $"Post 2 by {userId}" };
}

List<string> DbGetFollowers(string userId) => new() { "user_2", "user_3" }; // Mock followers

// GET /feed (Cache-Aside + Coalescing)
app.MapGet("/feed/{userId}", async (string userId, 
    [FromServices] ICache<string, List<string>> cache, 
    [FromServices] RequestCoalescer<string, List<string>> coalescer) =>
{
    // 1. Try Cache
    var cachedFeed = cache.Get(userId);
    if (cachedFeed != null)
    {
        return Results.Ok(new { Source = "Cache", Data = cachedFeed });
    }

    // 2. Cache Miss -> Coalesce concurrent requests -> Hit DB
    var data = await coalescer.GetOrComputeAsync(userId, async () =>
    {
        var dbData = await DbGetFeedAsync(userId);
        cache.Put(userId, dbData); // Populate cache for future requests
        return dbData;
    });

    return Results.Ok(new { Source = "DB", Data = data });
});

// POST /post (Invalidation)
app.MapPost("/post", (string authorId, string content, 
    [FromServices] ICache<string, List<string>> cache) =>
{
    // 1. Save post to DB (Mocked)
    
    // 2. Fan-out Cache Invalidation
    var followers = DbGetFollowers(authorId);
    foreach (var follower in followers)
    {
        cache.Invalidate(follower);
    }

    return Results.Ok(new { Status = "Success", InvalidatedFeeds = followers.Count });
});

app.Run();