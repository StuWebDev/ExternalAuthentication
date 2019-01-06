using Microsoft.AspNetCore.Identity;
using ElCamino.AspNetCore.Identity.AzureTable.Model;

namespace STS.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUserV2
    {
        public string Name { get; set; }
        public string Picture { get; set; }
    }
}
