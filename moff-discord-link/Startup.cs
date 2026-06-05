using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Content.Server.Database;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using IPNetwork = System.Net.IPNetwork;

namespace MoffDiscordLink;

public class Startup(IConfiguration configuration)
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LoginHandler>();
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        var connStr = configuration.GetConnectionString("DefaultConnection");
        if (connStr == null)
            throw new InvalidOperationException("Need to specify DefaultConnection connection string");

        services.AddDbContext<PostgresServerDbContext>(options => options.UseNpgsql(connStr));

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthConsts.AdminPolicy, p => p.RequireRole(Constants.AdminRole))
            .AddPolicy(AuthConsts.PiiPolicy, p => p.RequireRole(Constants.PIIRole));

        services.AddControllers();
        services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizeFolder("/Players", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizeFolder("/Connections", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizeFolder("/Bans", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizeFolder("/RoleBans", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizeFolder("/Logs", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizeFolder("/Characters", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizeFolder("/Whitelist", AuthConsts.AdminPolicy);
            options.Conventions.AuthorizePage("/Discord/Manage", AuthConsts.PiiPolicy);
            options.Conventions.AuthorizePage("/Discord/Edit", AuthConsts.PiiPolicy);
        });

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthConsts.Ss14Cookie;
                options.DefaultChallengeScheme = AuthConsts.Ss14AuthScheme;
            })
            .AddCookie(AuthConsts.Ss14Cookie, options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromHours(1);
                options.AccessDeniedPath = "/AccessDenied";
            })
            .AddCookie(AuthConsts.DiscordCookie, options => { options.ExpireTimeSpan = TimeSpan.FromMinutes(10); })
            .AddOAuth(AuthConsts.DiscordAuthScheme, options =>
            {
                options.SignInScheme = AuthConsts.DiscordCookie;
                options.ClientId = configuration["Discord:ClientId"]
                    ?? throw new InvalidOperationException("Discord:ClientId must be configured");
                options.ClientSecret = configuration["Discord:ClientSecret"]
                    ?? throw new InvalidOperationException("Discord:ClientSecret must be configured");
                options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                options.UserInformationEndpoint = "https://discord.com/api/users/@me";
                options.CallbackPath = "/discord-callback";
                options.Scope.Add("identify");
                options.Events.OnCreatingTicket = async ctx =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                    var response = await ctx.Backchannel.SendAsync(request, ctx.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ctx.HttpContext.RequestAborted));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var id))
                        ctx.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.GetString()!));
                    if (root.TryGetProperty("username", out var username))
                        ctx.Identity!.AddClaim(new Claim(ClaimTypes.Name, username.GetString()!));
                };
            })
            .AddOpenIdConnect(AuthConsts.Ss14AuthScheme, options =>
            {
                options.SignInScheme = AuthConsts.Ss14Cookie;

                options.Authority = configuration["Auth:Authority"];
                options.ClientId = configuration["Auth:ClientId"];
                options.ClientSecret = configuration["Auth:ClientSecret"];
                options.SaveTokens = true;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.GetClaimsFromUserInfoEndpoint = true;

                options.Events.OnTokenValidated = async ctx =>
                {
                    var handler = ctx.HttpContext.RequestServices.GetRequiredService<LoginHandler>();
                    await handler.HandleTokenValidated(ctx);
                };
            });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSerilogRequestLogging();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };

        foreach (var entry in configuration.GetSection("ForwardProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(entry, out var ip))
            {
                forwardedHeadersOptions.KnownProxies.Add(ip);
            }
            else if (IPNetwork.TryParse(entry, out var network))
            {
                forwardedHeadersOptions.KnownIPNetworks.Add(network);
            }
            else
            {
                throw new InvalidOperationException($"Invalid IP address or CIDR notation in ForwardProxies: {entry}");
            }
        }

        app.UseForwardedHeaders(forwardedHeadersOptions);

        var pathBase = configuration.GetValue<string>("PathBase");
        if (!string.IsNullOrEmpty(pathBase))
        {
            app.UsePathBase(pathBase);
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
        });
    }
}
