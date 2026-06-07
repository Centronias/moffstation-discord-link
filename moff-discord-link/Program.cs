using Serilog;

namespace MoffDiscordLink;

public static class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, builder) =>
        {
            var env = context.HostingEnvironment;
            builder.AddYamlFile("appsettings.yml", false, true);
            builder.AddYamlFile($"appsettings.{env.EnvironmentName}.yml", true, true);
            builder.AddYamlFile("appsettings.Secret.yml", true, true);
        })
        .UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration))
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
        .UseSystemd();
}
