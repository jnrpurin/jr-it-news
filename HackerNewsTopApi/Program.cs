using HackerNewsTopApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// HttpClient User-Agent avoid server bloq
builder.Services.AddHttpClient<IHackerNewsService, HackerNewsService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HackerNewsTopApi/1.0"); // (contact@yourdomain)
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "HackerNews Top Stories API",
        Version = "v1",
        Description = "Retrieves top N stories from the Hacker News API"
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
