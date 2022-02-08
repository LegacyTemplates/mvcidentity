using Funq;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;
using MyApp.ServiceInterface;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

[assembly: HostingStartup(typeof(MyApp.AppHost))]

namespace MyApp;

public class AppHost : AppHostBase, IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) => {
            // Configure ASP.NET Core IOC Dependencies
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

        
        AddSeedUsers(base.App).Wait();
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
