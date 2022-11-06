using System;
using System.Diagnostics;
using System.Security.Claims;
using app.Auth;
using app.Services;
using app.Services.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace app
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static readonly ActivitySource ActivitySource = new("pmc");

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddOpenTelemetryTracing(builder => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ActivitySource.Name))
                .AddSource(ActivitySource.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri("https://api.honeycomb.io");
                    options.Headers = $"x-honeycomb-team={Configuration["Instrumentation:Honeycomb:ApiKey"]}";
                })
            );

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ShouldListenTo = _ => true,
                ActivityStopped = activity =>
                {
                    foreach (var (key, value) in activity.Baggage) activity.AddTag(key, value);
                }
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddSingleton(typeof(IModelCache<>), typeof(ModelMemoryCache<>));

            services.AddCosmosStorage(options =>
            {
                options.StorageEndpoint = Configuration["Storage:Cosmos:Endpoint"];
                options.StorageKey = Configuration["Storage:Cosmos:Key"];
            });
            services.AddDistributedCosmosCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromDays(7);
                options.Cookie.Name = "SessionId";
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.MaxAge = options.IdleTimeout;
            });

            services
                .AddAuthentication(MultipleAuthenticationDefaults.AuthenticationScheme)
                .AddMultiple()
                .AddOAuthWithUser("Exist", options =>
                {
                    options.ClientId = Configuration["Authentication:Exist:ClientId"];
                    options.ClientSecret = Configuration["Authentication:Exist:ClientSecret"];
                    options.AuthorizationEndpoint = "https://exist.io/oauth2/authorize";
                    options.TokenEndpoint = "https://exist.io/oauth2/access_token";
                    options.UserInformationEndpoint = "https://exist.io/api/2/accounts/profile/";
                    options.Scope.Add("custom_read");
                    options.CallbackPath = "/signin-exist";
                    options.SaveTokens = true;
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "username");
                    options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "last_name");
                    options.ForwardAuthenticate = MultipleAuthenticationDefaults.AuthenticationScheme;
                })
                .AddMicrosoftAccount("Microsoft", options =>
                {
                    options.ClientId = Configuration["Authentication:Microsoft:ClientId"];
                    options.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];
                    options.Scope.Add("Mail.ReadWrite");
                    options.Scope.Add("offline_access");
                    options.Scope.Add("Tasks.Read");
                    options.SaveTokens = true;
                    options.ForwardAuthenticate = MultipleAuthenticationDefaults.AuthenticationScheme;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Exist", policy => policy.AddAuthenticationSchemes("Exist").AddRequirements(new MultipleAuthenticationRequirement("Exist")));
                options.AddPolicy("Microsoft", policy => policy.AddAuthenticationSchemes("Microsoft").AddRequirements(new MultipleAuthenticationRequirement("Microsoft")));
            });

            services.AddSingleton<IAuthorizationMiddlewareResultHandler, MultipleAuthenticationAuthorizationMiddlewareResultHandler>();
            services.AddSingleton<MultipleAuthenticationContext<OAuthOptions>>();
            services.AddSingleton<MultipleAuthenticationContext<MicrosoftAccountOptions>>();

            services.AddScoped<OAuthProvider>();
            services.AddScoped<MicrosoftGraphProvider>();

            services.AddScoped<ExistData>();
            services.AddScoped<MicrosoftData>();

            services.AddControllers();

            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

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

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = ctx =>
                {
                    var versioned = !string.IsNullOrEmpty(ctx.Context.Request.Query["v"]);
                    ctx.Context.Response.Headers["Cache-Control"] = versioned ? "public, max-age=604800, immutable" : "public, no-cache";
                }
            });

            app.UseResponseHeader("Cache-Control", "private, no-cache");
            app.UseRouting();

            app.UseSession();
            app.UseSessionSlidingExpiration();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });
        }
    }
}
