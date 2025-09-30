using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.Cognito;

namespace ThirdOpinion.Common.UnitTests;

public class Initialize : IDisposable
{
    static Initialize()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var collection = new ServiceCollection();
        collection.Configure<GlobalAppSettingsOptions>(Configuration);

        ServiceProvider = collection.BuildServiceProvider();
    }

    public static IConfiguration Configuration { get; }

    public static IServiceProvider ServiceProvider { get; }

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}