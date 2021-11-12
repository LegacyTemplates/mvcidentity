using Funq;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.EntityFrameworkCore;
using MyApp.ServiceInterface;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Configuration;

[assembly: HostingStartup(typeof(MyApp.AppHost))]

namespace MyApp;

/// <summary>
/// To create Identity SQL Server database, change "ConnectionStrings" in appsettings.json
///   $ dotnet ef migrations add CreateMyAppIdentitySchema
///   $ dotnet ef database update
/// </summary>
public class AppHost : AppHostBase, IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) => {
            var config = context.Configuration;
#if DEBUG
        services.AddMvc(options => options.EnableEndpointRouting = false).AddRazorRuntimeCompilation();
#else
            services.AddMvc(options => options.EnableEndpointRouting = false);
#endif

        services.Configure<CookiePolicyOptions>(options =>
        {
            // This lambda determines whether user consent for non-essential cookies is needed for a given request.
            options.CheckConsentNeeded = context => true;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        services.AddIdentity<ApplicationUser, IdentityRole>(options => {
                options.User.AllowedUserNameCharacters = null;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
            
        services.AddAuthentication(IISDefaults.AuthenticationScheme)
            .AddTwitter(options => { /* Create Twitter App at: https://dev.twitter.com/apps */
                options.ConsumerKey = config["oauth.twitter.ConsumerKey"];
                options.ConsumerSecret = config["oauth.twitter.ConsumerSecret"];
                options.SaveTokens = true;
                options.RetrieveUserDetails = true;
            })
            .AddFacebook(options => { /* Create App https://developers.facebook.com/apps */
                options.AppId = config["oauth.facebook.AppId"];
                options.AppSecret = config["oauth.facebook.AppSecret"];
                options.SaveTokens = true;
                options.Scope.Clear();
                config.GetSection("oauth.facebook.Permissions").GetChildren()
                    .Each(x => options.Scope.Add(x.Value));
            })
            .AddGoogle(options => { /* Create App https://console.developers.google.com/apis/credentials */
                options.ClientId = config["oauth.google.ConsumerKey"];
                options.ClientSecret = config["oauth.google.ConsumerSecret"];
                options.SaveTokens = true;
            })
            .AddMicrosoftAccount(options => { /* Create App https://apps.dev.microsoft.com */
                options.ClientId = config["oauth.microsoftgraph.AppId"];
                options.ClientSecret = config["oauth.microsoftgraph.AppSecret"];
                options.SaveTokens = true;
            });
            
        services.Configure<ForwardedHeadersOptions>(options => {
            //https://github.com/aspnet/IISIntegration/issues/140#issuecomment-215135928
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        });
            
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = false;
            options.Password.RequiredUniqueChars = 6;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
            options.Lockout.MaxFailedAccessAttempts = 10;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
        });

        services.ConfigureApplicationCookie(options =>
        {
            // Cookie settings
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(150);
            // If the LoginPath isn't set, ASP.NET Core defaults 
            // the path to /Account/Login.
            options.LoginPath = "/Account/Login";
            // If the AccessDeniedPath isn't set, ASP.NET Core defaults 
            // the path to /Account/AccessDenied.
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
        });

        // Add application services.
        services.AddTransient<IEmailSender, EmailSender>();

        // Populate ApplicationUser with Auth Info
        services.AddTransient<IExternalLoginAuthInfoProvider>(c => 
            new ExternalLoginAuthInfoProvider(config));
    })
    .Configure(app => {
        app.UseStaticFiles();
        app.UseCookiePolicy();
        app.UseAuthentication();
        
        app.UseServiceStack(new AppHost());

        app.UseMvc(routes =>
        {
            routes.MapRoute(
                name: "default",
                template: "{controller=Home}/{action=Index}/{id?}");
        });
        
        AddSeedUsers(app).Wait();
    });

    public AppHost() : base("MyApp", typeof(MyServices).Assembly) { }

    // Configure your AppHost with the necessary configuration and dependencies your App needs
    public override void Configure(Container container)
    {
        SetConfig(new HostConfig {
            AdminAuthSecret = "adm1nSecret",
        });

        // TODO: Replace OAuth App settings in: appsettings.Development.json
        Plugins.Add(new AuthFeature(() => new CustomUserSession(), 
            new IAuthProvider[] {
                new NetCoreIdentityAuthProvider(AppSettings) // Adapter to enable ASP.NET Core Identity Auth in ServiceStack
                {
                    AdminRoles = { "Manager" }, // Automatically Assign additional roles to Admin Users
                    PopulateSessionFilter = (session, principal, req) => 
                    {
                        //Example of populating ServiceStack Session Roles + Custom Info from EF Identity DB
                        var user = req.GetMemoryCacheClient().GetOrCreate(
                            IdUtils.CreateUrn(nameof(ApplicationUser), session.Id),
                            TimeSpan.FromMinutes(5), // return cached results before refreshing cache from db every 5 mins
                            () => ApplicationServices.DbExec(db => db.GetIdentityUserById<ApplicationUser>(session.Id)));

                        session.Email ??= user.Email;
                        session.FirstName ??= user.FirstName;
                        session.LastName ??= user.LastName;
                        session.DisplayName ??= user.DisplayName;
                        session.ProfileUrl = user.ProfileUrl ?? Svg.GetDataUri(Svg.Icons.DefaultProfile);

                        session.Roles = req.GetMemoryCacheClient().GetOrCreate(
                            IdUtils.CreateUrn(nameof(session.Roles), session.Id),
                            TimeSpan.FromMinutes(5), // return cached results before refreshing cache from db every 5 mins
                            () => ApplicationServices.DbExec(db => db.GetIdentityUserRolesById(session.Id)));
                    }
                }, 
            }));
    }
        
    private async Task AddSeedUsers(IApplicationBuilder app)
    {
        var scopeFactory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();

        using var scope = scopeFactory.CreateScope();
        //initializing custom roles 
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        string[] roleNames = { "Admin", "Manager" };

        void assertResult(IdentityResult result)
        {
            if (!result.Succeeded)
                throw new Exception(result.Errors.First().Description);
        }

        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                //create the roles and seed them to the database: Question 1
                assertResult(await roleManager.CreateAsync(new IdentityRole(roleName)));
            }
        }
        
        var testUser = await userManager.FindByEmailAsync("user@gmail.com");
        if (testUser == null)
        {
            assertResult(await userManager.CreateAsync(new ApplicationUser {
                DisplayName = "Test User",
                Email = "user@gmail.com",
                UserName = "user@gmail.com",
                FirstName = "Test",
                LastName = "User",
            }, "p@55wOrd"));
        }

        var managerUser = await userManager.FindByEmailAsync("manager@gmail.com");
        if (managerUser == null)
        {
            assertResult(await userManager.CreateAsync(new ApplicationUser {
                DisplayName = "Test Manager",
                Email = "manager@gmail.com",
                UserName = "manager@gmail.com",
                FirstName = "Test",
                LastName = "Manager",
            }, "p@55wOrd"));
                
            managerUser = await userManager.FindByEmailAsync("manager@gmail.com");
            assertResult(await userManager.AddToRoleAsync(managerUser, "Manager"));
        }

        var adminUser = await userManager.FindByEmailAsync("admin@gmail.com");
        if (adminUser == null)
        {
            assertResult(await userManager.CreateAsync(new ApplicationUser {
                DisplayName = "Admin User",
                Email = "admin@gmail.com",
                UserName = "admin@gmail.com",
                FirstName = "Admin",
                LastName = "User",
            }, "p@55wOrd"));
                
            adminUser = await userManager.FindByEmailAsync("admin@gmail.com");
            assertResult(await userManager.AddToRoleAsync(adminUser, RoleNames.Admin));
        }
    }
}

public static class AppExtensions
{
    public static T DbExec<T>(this IServiceProvider services, Func<System.Data.IDbConnection, T> fn) => 
        services.DbContextExec<ApplicationDbContext,T>(ctx => {
            ctx.Database.OpenConnection(); return ctx.Database.GetDbConnection(); }, fn);
}
    
// Add any additional metadata properties you want to store in the Users Typed Session
public class CustomUserSession : AuthUserSession
{
}
