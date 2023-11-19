# mvcidentity

.NET 6.0 MVC Website integrated with ServiceStack using ASP.NET Identity Auth

![](https://raw.githubusercontent.com/ServiceStack/Assets/master/csharp-templates/mvcidentity.png)

> Login

![](https://raw.githubusercontent.com/ServiceStack/Assets/master/csharp-templates/mvcidentity-login.png)

> Browse [source code](https://github.com/NetCoreTemplates/mvcidentity) and install with the `web` dotnet tool:

    $ dotnet tool install -g x

    $ x new LegacyTemplates/mvcidentity ProjectName

Alternatively write new project files directly into an empty repository, using the Directory Name as the ProjectName:

    $ git clone https://github.com/<User>/<ProjectName>.git
    $ cd <ProjectName>
    $ x new mvcidentity

### Database Setup

To create the User and Auth tables you will need access to SQL Server instance then change the `"DefaultConnection"` connection string in `appsettings.json` then you can run the ef .NET Core tool:

    $ dotnet ef migrations add Create{ProjectName}IdentitySchema
    $ dotnet ef database update

If needed, you can re-create your DB schema after modifying your custom `ApplicationUser` class with additional info you want to store on each user, by deleting the `__EFMigrationHistory` and all `AspNet*` tables, deleting the `Migrations` folder in your host projects and re-running the above commands.

### OAuth Setup

Replace the `oauth.*` App settings with your own in `appsettings.Development.json` for local development and `appsettings.json` for production deployments.

 - Twitter - [Create Twitter App](https://dev.twitter.com/apps) with `{BaseUrl}/signin-twitter` referrer and follow [Twitter walk through](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/twitter-logins?view=aspnetcore-2.2)
 - Facebook - [Create Facebook App](https://developers.facebook.com/apps) with `{BaseUrl}/signin-facebook` referrer and follow [Facebook walk through](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/facebook-logins?view=aspnetcore-2.2)
 - Google - [Create Google App](https://console.developers.google.com/apis/credentials) with `{BaseUrl}/signin-google` referrer and follow [Google walk through](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-2.2)
 - Microsoft - [Create Microsoft App](https://apps.dev.microsoft.com) with `{BaseUrl}/signin-microsoft` referrer and follow [Microsoft walk through](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-2.2)
