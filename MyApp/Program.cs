using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.EntityFrameworkCore;
using ServiceStack;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
var services = builder.Services;
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

// To create Identity SQL Server database, change "ConnectionStrings" in appsettings.json
//   $ dotnet ef migrations add CreateMyAppIdentitySchema
//   $ dotnet ef database update
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseDeveloperExceptionPage();
}
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

app.Run();
