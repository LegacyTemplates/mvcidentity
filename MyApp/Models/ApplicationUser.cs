#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace MyApp.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayName { get; set; }

        public string? TwitterUserId { get; set; }
        public string? TwitterScreenName { get; set; }

        public string? FacebookUserId { get; set; }

        public string? GoogleUserId { get; set; }

        public string? GoogleProfilePageUrl { get; set; }

        public string? MicrosoftUserId { get; set; }
        
        public string? ProfileUrl { get; set; }
    }
}
