using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskFlow.Identity.Api.Tests;

public class StartupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("too-short-for-hs256")]
    public void Api_refuses_to_start_without_a_strong_jwt_key(string key)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mongo:ConnectionString", "mongodb://localhost:1");
            builder.UseSetting("Mongo:Database", "unused");
            builder.UseSetting("Jwt:Key", key);
        });

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:Key", error.ToString());
    }
}
