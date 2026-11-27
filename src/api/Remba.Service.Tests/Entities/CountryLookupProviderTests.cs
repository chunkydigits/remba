using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Entities;
using Remba.Service.Entities;

namespace Remba.Service.Tests.Entities;

public class CountryLookupProviderTests
{
    [Fact]
    public async Task SearchAsync_WhenNameMatches_ThenShouldReturnFlagUrlAndIso2()
    {
        var provider = CreateProvider(
            """
            [
              {"name":"Japan","iso2":"JP","iso3":"JPN","capital":"Tokyo","region":"Asia","flag":"/flags/jp.svg"},
              {"name":"Jamaica","iso2":"JM","iso3":"JAM","capital":"Kingston","region":"Americas","flag":"/flags/jm.svg"}
            ]
            """);

        var results = await provider.SearchAsync(EntityTypes.Country, "jap");

        Assert.Single(results);
        Assert.Equal("Japan", results[0].Name);
        Assert.Equal(EntityIdentifierTypes.Iso2, results[0].IdentifierType);
        Assert.Equal("JP", results[0].IdentifierValue);
        Assert.Equal("https://flagdex.net/flags/jp.svg", results[0].ImageUrl);
        Assert.Equal(EntityLookupProviders.Flagdex, results[0].Provider);
    }

    [Fact]
    public async Task GetByIdentifierAsync_WhenIso2_ThenShouldResolveCountry()
    {
        var provider = CreateProvider(
            """
            [
              {"name":"France","iso2":"FR","iso3":"FRA","capital":"Paris","region":"Europe","flag":"/flags/fr.svg"}
            ]
            """);

        var result = await provider.GetByIdentifierAsync(
            EntityTypes.Country,
            EntityIdentifierTypes.Iso2,
            "fr");

        Assert.NotNull(result);
        Assert.Equal("France", result!.Name);
        Assert.Equal("FR", result.IdentifierValue);
        Assert.Equal("https://flagdex.net/flags/fr.svg", result.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_WhenFlagIsFileUri_ThenShouldReturnHttpsFlagUrl()
    {
        var provider = CreateProvider(
            """
            [
              {"name":"New Zealand","iso2":"NZ","iso3":"NZL","capital":"Wellington","region":"Oceania","flag":"file:///flags/nz.svg"}
            ]
            """);

        var results = await provider.SearchAsync(EntityTypes.Country, "new zealand");

        Assert.Single(results);
        Assert.Equal("https://flagdex.net/flags/nz.svg", results[0].ImageUrl);
    }

    private static CountryLookupProvider CreateProvider(string json)
    {
        var handler = new StubHandler(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://flagdex.net/")
        };

        return new CountryLookupProvider(
            httpClient,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new FlagdexOptions()),
            NullLogger<CountryLookupProvider>.Instance);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
