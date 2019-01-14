using IdentityModel;
using IdentityServer4.Events;
using IdentityServer4.Quickstart.UI;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using STS.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Host.Quickstart.Account
{
    [SecurityHeaders]
    [AllowAnonymous]
    public class ExternalController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clientStore;
        private readonly IEventService _events;

        public ExternalController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IEventService events)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _interaction = interaction;
            _clientStore = clientStore;
            _events = events;
        }

        // initiate roundtrip to external authentication provider
        [HttpGet]
        public async Task<IActionResult> Challenge(string provider, string returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl)) returnUrl = "~/";

            // validate returnUrl - either it is a valid OIDC URL or back to a local page
            if (Url.IsLocalUrl(returnUrl) == false && _interaction.IsValidReturnUrl(returnUrl) == false)
            {
                // user might have clicked on a malicious link - should be logged
                throw new Exception("invalid return URL");
            }

            if (AccountOptions.WindowsAuthenticationSchemeName == provider)
            {
                // windows authentication needs special handling
                return await ProcessWindowsLoginAsync(returnUrl);
            }
            else
            {
                // start challenge and roundtrip the return URL and scheme 
                var props = new AuthenticationProperties
                {
                    RedirectUri = Url.Action(nameof(Callback)),
                    Items =
                    {
                        { "returnUrl", returnUrl },
                        { "scheme", provider },
                    }
                };

                return Challenge(props, provider);
            }
        }

        // Post processing of external authentication
        [HttpGet]
        public async Task<IActionResult> Callback()
        {
            // OK - This runs through the authentication method in Startup.cs
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (result?.Succeeded != true)
            {
                throw new Exception("External authentication error");
            }

            // Uncomment to see a log of the Claims
            //ClaimsPrincipal principalResult = result.Principal as ClaimsPrincipal;
            //if (null != principalResult)
            //{
            //    foreach (Claim claim in principalResult.Claims)
            //    {
            //        Console.WriteLine("CLAIM TYPE: " + claim.Type + "; CLAIM VALUE: " + claim.Value);
            //    }
            //}

            // OK - lookup our user and external provider info
            var (user, provider, providerUserId, claims) = await FindUserFromExternalProviderAsync(result);

            // OK - Auto-register "New" External-Provider Authenticated "User"
            if (user == null)
            {
                // User registration for external log in
                user = await AutoRegisterExternalUser(provider, providerUserId, claims);
            }
            else
            // Check if picture or username has changed
            {
                user = await UpdateExternalUser(user, claims);
            }

            // OK - Used to store data needed for signout from those protocols.
            var additionalLocalClaims = new List<Claim>();

            // OK - Add state to cookie I create
            var localSignInProps = new AuthenticationProperties();

            var principal = await _signInManager.CreateUserPrincipalAsync(user);

            additionalLocalClaims.AddRange(principal.Claims);
            var name = principal.FindFirst(JwtClaimTypes.Name)?.Value ?? user.Id;

            await _events.RaiseAsync(new UserLoginSuccessEvent(provider, providerUserId, user.Id, name));

            // OK - This is our Cookie
            await HttpContext.SignInAsync(user.Id, name, provider, localSignInProps, additionalLocalClaims.ToArray());

            // OK - Delete temporary cookie used during external authentication
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // OK - validate return URL and redirect back to authorization endpoint or a local page
            var returnUrl = result.Properties.Items["returnUrl"];
            if (_interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect("~/");
        }

        private async Task<IActionResult> ProcessWindowsLoginAsync(string returnUrl)
        {
            // see if windows auth has already been requested and succeeded
            var result = await HttpContext.AuthenticateAsync(AccountOptions.WindowsAuthenticationSchemeName);
            if (result?.Principal is WindowsPrincipal wp)
            {
                // we will issue the external cookie and then redirect the
                // user back to the external callback, in essence, treating windows
                // auth the same as any other external authentication mechanism
                var props = new AuthenticationProperties()
                {
                    RedirectUri = Url.Action("Callback"),
                    Items =
                    {
                        { "returnUrl", returnUrl },
                        { "scheme", AccountOptions.WindowsAuthenticationSchemeName },
                    }
                };

                var id = new ClaimsIdentity(AccountOptions.WindowsAuthenticationSchemeName);
                id.AddClaim(new Claim(JwtClaimTypes.Subject, wp.Identity.Name));
                id.AddClaim(new Claim(JwtClaimTypes.Name, wp.Identity.Name));

                // add the groups as claims -- be careful if the number of groups is too large
                if (AccountOptions.IncludeWindowsGroups)
                {
                    var wi = wp.Identity as WindowsIdentity;
                    var groups = wi.Groups.Translate(typeof(NTAccount));
                    var roles = groups.Select(x => new Claim(JwtClaimTypes.Role, x.Value));
                    id.AddClaims(roles);
                }

                await HttpContext.SignInAsync(
                    IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme,
                    new ClaimsPrincipal(id),
                    props);
                return Redirect(props.RedirectUri);
            }
            else
            {
                // trigger windows auth
                // since windows auth don't support the redirect uri,
                // this URL is re-triggered when we call challenge
                return Challenge(AccountOptions.WindowsAuthenticationSchemeName);
            }
        }

        // This function is returning a providerId, provider, and checking if they already exist in the database
        private async Task<(ApplicationUser user, string provider, string providerUserId, IEnumerable<Claim> claims)>
        FindUserFromExternalProviderAsync(AuthenticateResult result)
        {
            // access to all the Profile data?? the "Principal"
            var externalUser = result.Principal;

            // SUB - try to determine the unique id of the external user (issued by the provider)
            var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject) ??
                              externalUser.FindFirst(ClaimTypes.NameIdentifier) ??
                              throw new Exception("Unknown userid");

            // remove the user id claim so we don't include it as an extra claim if/when we provision the user
            var claims = externalUser.Claims.ToList();
            claims.Remove(userIdClaim);

            // Which External provider is authenticating the user
            var provider = result.Properties.Items["scheme"];
            var providerUserId = userIdClaim.Value;

            // Find if user is in my database
            var user = await _userManager.FindByLoginAsync(provider, providerUserId);

            return (user, provider, providerUserId, claims);
        }

        // Making Claims about the User & Registration
        private async Task<ApplicationUser> AutoRegisterExternalUser(string provider, string providerUserId, IEnumerable<Claim> claims)
        {
            // create a list of claims that we want to transfer into our store
            var filtered = new List<Claim>();

            // Name of the user
            var name = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Name)?.Value ??
                claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            if (name != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, name));
            }
            else
            {
                var first = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.GivenName)?.Value ??
                    claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName)?.Value;
                var last = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.FamilyName)?.Value ??
                    claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value;
                if (first != null && last != null)
                {
                    filtered.Add(new Claim(JwtClaimTypes.Name, first + " " + last));
                }
                else if (first != null)
                {
                    filtered.Add(new Claim(JwtClaimTypes.Name, first));
                }
                else if (last != null)
                {
                    filtered.Add(new Claim(JwtClaimTypes.Name, last));
                }
            }

            // Email of the user
            var email = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Email)?.Value ??
               claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
            if (email != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Email, email));
            }

            // Picture
            var picture = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Picture)?.Value;
            if (picture != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Picture, picture));
            }

            // Make a simple user with UserName and E-mail
            // add a picture if there is one
            var user = new ApplicationUser
            {
                UserName = Guid.NewGuid().ToString(),
                Email = email,
                Name = name,
                Picture = picture
            };

            // CreateAsync is using name and e-mail
            var identityResult = await _userManager.CreateAsync(user);
            if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);

            if (filtered.Any())
            {
                identityResult = await _userManager.AddClaimsAsync(user, filtered);
                if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);
            }

            // Log in as new user
            identityResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerUserId, provider));
            if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);

            // a newly registered and logged in user
            return user;
        }
        private async Task<ApplicationUser> UpdateExternalUser(ApplicationUser user, IEnumerable<Claim> claims)
        {
            var oldName = user.Name;
            var oldPicture = user.Picture;

            // latest name of the user
            var name = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Name)?.Value ??
                claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            if (name != null && name != oldName)
            {
                // Write to database
                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded) throw new Exception(update.Errors.First().Description);

                // Change the strings back to claims and replace
                var claimReplacement = await _userManager.ReplaceClaimAsync(user, new Claim(JwtClaimTypes.Name, oldName), new Claim(JwtClaimTypes.Name, name));
                if (!claimReplacement.Succeeded) throw new Exception(claimReplacement.Errors.First().Description);
            }

            // latest picture
            var picture = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Picture)?.Value;
            if (picture != null && picture != oldPicture)
            {
                // Write to database
                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded) throw new Exception(update.Errors.First().Description);

                // Change the strings back to claims and replace
                var claimReplacement = await _userManager.ReplaceClaimAsync(user, new Claim(JwtClaimTypes.Picture, oldPicture), new Claim(JwtClaimTypes.Picture, picture));
                if (!claimReplacement.Succeeded) throw new Exception(claimReplacement.Errors.First().Description);
            }
            return user;
        }
    }
}