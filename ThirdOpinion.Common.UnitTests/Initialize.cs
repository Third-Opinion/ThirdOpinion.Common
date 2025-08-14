using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.Cognito;

namespace ThirdOpinion.Common.UnitTests;

public class Initialize : IDisposable
{
    public static IConfiguration Configuration { get; private set; }

    public static IServiceProvider ServiceProvider { get; private set; }

    static Initialize()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var collection = new ServiceCollection();
        collection.Configure<GlobalAppSettingsOptions>(Configuration);

        ServiceProvider = collection.BuildServiceProvider();
    }

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}