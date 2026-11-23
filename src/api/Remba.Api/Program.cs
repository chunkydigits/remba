using System.Text.Json;
using System.Text.Json.Serialization;
using Remba.Api.Configuration;
using Remba.Model.Configuration;
using Remba.Repository.Cosmos;
using Remba.Service.System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddRembaAuthentication(builder.Configuration);
builder.Services.AddRembaRepositories(builder.Configuration);
builder.Services.AddRembaServices(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("RembaUi", policy =>
    {
        // Embed buttons call the API from arbitrary host pages using bearer tokens.
        // Keep this open; authorization still requires a valid Auth0 access token.
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

var auth0 = app.Configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>() ?? new Auth0Options();
var cosmos = app.Configuration.GetSection(CosmosDbOptions.SectionName).Get<CosmosDbOptions>() ?? new CosmosDbOptions();

if (!auth0.IsConfigured)
{
    app.Logger.LogWarning("Auth0 is not configured. Protected endpoints will reject unauthenticated requests until Domain and Audience are set.");
}

using (var scope = app.Services.CreateScope())
{
    if (!cosmos.IsConfigured)
    {
        app.Logger.LogWarning("Cosmos DB is not configured. Using in-memory storage for local development.");
    }
    else
    {
        var initializer = scope.ServiceProvider.GetRequiredService<CosmosDbInitializer>();
        await initializer.EnsureCreatedAsync();
    }

    var suggestedLists = scope.ServiceProvider.GetRequiredService<ISuggestedListService>();
    await suggestedLists.EnsureDefaultsAsync();

    var entityTypes = scope.ServiceProvider.GetRequiredService<IEntityTypeService>();
    await entityTypes.EnsureDefaultsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("RembaUi");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
