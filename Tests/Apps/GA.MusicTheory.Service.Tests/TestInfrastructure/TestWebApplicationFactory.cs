namespace GA.MusicTheory.Service.Tests;

using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IEnumerable<Assembly> GetTestAssemblies() =>
        [typeof(TestWebApplicationFactory).Assembly];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(TestPaths.RepositoryPath("Apps", "ga-server", "GA.MusicTheory.Service"));
        base.ConfigureWebHost(builder);
    }
}
