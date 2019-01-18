using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MyApp.Models;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Text;

namespace MyApp
{
    public interface IExternalLoginAuthInfoProvider
    {
        void PopulateUser(ExternalLoginInfo info, ApplicationUser user);
    }

    public class ExternalLoginAuthInfoProvider : IExternalLoginAuthInfoProvider
    {
        private readonly IConfiguration configuration;
        private readonly IAuthHttpGateway authGateway;

        public ExternalLoginAuthInfoProvider(IConfiguration configuration, IAuthHttpGateway authHttpGateway = null)
        {
            this.configuration = configuration;
            this.authGateway = authHttpGateway ?? new AuthHttpGateway();
        }

        public void PopulateUser(ExternalLoginInfo info, ApplicationUser user)
        {
            var accessToken = info.AuthenticationTokens.FirstOrDefault(x => x.Name == "access_token");
            var accessTokenSecret = info.AuthenticationTokens.FirstOrDefault(x => x.Name == "access_token_secret");
            
            if (info.LoginProvider == "Twitter" && accessToken != null && accessTokenSecret != null)
            {
                var twitterUserId = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

                var twitterInfo = JsonObject.ParseArray(authGateway.DownloadTwitterUserInfo(
                        consumerKey: configuration["oauth.twitter.ConsumerKey"],
                        consumerSecret: configuration["oauth.twitter.ConsumerSecret"],
                        accessToken: accessToken.Value,
                        accessTokenSecret: accessTokenSecret.Value,
                        twitterUserId: twitterUserId))
                    .FirstOrDefault();

                user.TwitterUserId = twitterUserId;

                if (twitterInfo != null)
                {
                    user.DisplayName = twitterInfo.Get("name");
                    user.TwitterScreenName = twitterInfo.Get("screen_name");

                    if (twitterInfo.TryGetValue("profile_image_url", out var profileUrl))
                    {
                        user.ProfileUrl = profileUrl.SanitizeOAuthUrl();
                    }
                }
            }
            else if (info.LoginProvider == "Facebook")
            {
                user.FacebookUserId = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
                user.DisplayName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
                user.FirstName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName)?.Value;
                user.LastName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value;

                if (accessToken != null)
                {
                    var facebookInfo = JsonObject.Parse(authGateway.DownloadFacebookUserInfo(accessToken.Value, "picture"));
                    var picture = facebookInfo.Object("picture");
                    var data = picture?.Object("data");
                    if (data != null)
                    {
                        if (data.TryGetValue("url", out var profileUrl))
                        {
                            user.ProfileUrl = profileUrl.SanitizeOAuthUrl();
                        }
                    }                
                }
            }
            else if (info.LoginProvider == "Google")
            {
                user.GoogleUserId = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
                user.DisplayName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
                user.FirstName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName)?.Value;
                user.LastName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value;
                user.GoogleProfilePageUrl = info.Principal?.Claims.FirstOrDefault(x => x.Type == "urn:google:profile")?.Value;

                if (accessToken != null)
                {
                    var googleInfo = JsonObject.Parse(authGateway.DownloadGoogleUserInfo(accessToken.Value));
                    user.ProfileUrl = googleInfo.Get("picture").SanitizeOAuthUrl();
                }
            }
            else if (info.LoginProvider == "Microsoft")
            {
                user.MicrosoftUserId = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
                user.DisplayName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
                user.FirstName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName)?.Value;
                user.LastName = info.Principal?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value;

                if (accessToken != null)
                {
                    if (configuration["oauth.microsoftgraph.SavePhoto"] == "true")
                    {
                        user.ProfileUrl = authGateway.CreateMicrosoftPhotoUrl(accessToken.Value, configuration["oauth.microsoftgraph.SavePhotoSize"]);
                    }
                }
            }
        }
    }
}