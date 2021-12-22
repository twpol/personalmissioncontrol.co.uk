using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace app
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.Cookie.Name = "SessionId";
                options.Cookie.IsEssential = true;
            });

            services
                .AddAuthentication(MultipleAuthenticationDefaults.AuthenticationScheme)
                .AddMultiple()
                .AddMicrosoftAccount("Microsoft", options =>
                {
                    options.ClientId = Configuration["Authentication:Microsoft:ClientId"];
                    options.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];
                    options.Scope.Add("mail.read");
                    options.Scope.Add("offline_access");
                    options.SaveTokens = true;
                    options.ForwardAuthenticate = MultipleAuthenticationDefaults.AuthenticationScheme;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Microsoft", policy => policy.AddAuthenticationSchemes("Microsoft").AddRequirements(new MultipleAuthenticationRequirement("Microsoft")));
            });

            services.AddSingleton<IAuthorizationMiddlewareResultHandler, MultipleAuthenticationAuthorizationMiddlewareResultHandler>();

            services.AddScoped<MicrosoftGraphProvider>();

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

            app.UseResponseHeader("Cache-Control", "public, max-age=604800, immutable");
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseResponseHeader("Cache-Control", "private, max-age=1");
            app.UseRouting();

            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }
    }
}
