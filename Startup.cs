// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
using STS.Data;
using STS.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using ElCamino.AspNetCore.Identity.AzureTable.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using IdentityModel;

namespace STS
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // MICROSOFT ASP.NET IDENTITY
            // Good video to get started : https://www.youtube.com/watch?v=ipAwOGzpxpQ
            services
                .AddIdentity<ApplicationUser, ElCamino.AspNetCore.Identity.AzureTable.Model.IdentityRole>()


              // ElCamino.Azure.Table package from Dave Melendez -  https://github.com/dlmelendez/identityazuretable
                .AddAzureTableStoresV2<ApplicationDbContext>(new Func<IdentityConfiguration>(() =>
                {
                    // you can configure this for AzureStorageEmulator.exe - see documentation below
                    // https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
                    // with the default connection string as below
                    // DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;

                    // or create new Azure blob storage resource and use that connection string

                    IdentityConfiguration idconfig = new IdentityConfiguration();
                    //idconfig.TablePrefix = Configuration["AzureTable:Prefix"];
                    idconfig.StorageConnectionString = Configuration["AzureTable:ConnectionString"];
                    //idconfig.LocationMode = Configuration["AzureTable:LocationMode"];
                    return idconfig;

                }))
                .AddDefaultTokenProviders()

             // Run this the first time you Publish the app to create the required databases in your Cloud Blob Storage account
             // The next time you pubilsh/update/stage/release your app you can remove this line
               .CreateAzureTablesIfNotExists<ApplicationDbContext>();


            // IDENTITY SERVER
            // running InMemory as this is a demo of just setting up Authentication with External Providers
            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })
                .AddInMemoryIdentityResources(Config.GetIdentityResources())
                .AddInMemoryClients(Config.GetClients())
                .AddAspNetIdentity<ApplicationUser>()
                .AddDeveloperSigningCredential();

            // Shows the Issuer in debugging "SecurityTokenInvalidIssuerException: IDX10205" error,
            // this error came up while configuring Microsoft Account, due to the tenantid being a mismatch.
            // uncomment below to show the issuer
            //IdentityModelEventSource.ShowPII = true;

            services.AddAuthentication()
                // using default "IdentityConstants.External" cookie
                //
                // OPENID CONNECT - IMPLICIT FLOW - RESPONSE = "id_token", SCOPE = "openid profile"

                .AddOpenIdConnect("Google", "Google", o => // "Scheme", "DisplayName", Options =>
                {
                    // Discovery Document
                    // https://accounts.google.com/.well-known/openid-configuration

                    o.Authority = "https://accounts.google.com";

                    // Callback endpoint - you can make them up as you go along... no other coding required
                    o.CallbackPath = "/signin-google";
                    // Must be different endpoint than /signin-oidc if you have multiple OpenID Connect authority log ins
                    // To avoid "Correlation Error"

                    // Client_Id for demo purposes only ( no APIs enabled )
                    o.ClientId = Configuration["Google:id"];
                })

                .AddOpenIdConnect("Microsoft", "Microsoft", o => // "Scheme", "DisplayName", Options =>
                {
                    // Discovery Document
                    // https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration

                    // Single-Tenant Authority endpoint
                    //o.Authority = "https://login.microsoftonline.com/{replaceThisWithYourTenantId}/v2.0/";

                    // Multi-Tenant Authority endpoint
                    o.Authority = "https://login.microsoftonline.com/common/v2.0/";

                    // now you need to configure the accepted tenants -or- go back and just use a single-tenant

                    // Add each Azure AD tenantid that you want to include/approve
                    // eg. for personal accounts, the issuer is = "9188040d-6c67-4c5b-b112-36a304b66dad"
                    o.TokenValidationParameters.ValidIssuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";
                    //
                    // OR for development purposes
                    //
                    // switch off the requirement for the tenant issuer to be validated
                    //o.TokenValidationParameters.ValidateIssuer = false;

                    // see below for a more comprehensive way to set up dynamic multi-tenant environments
                    // https://github.com/Azure-Samples/active-directory-dotnet-webapp-multitenant-openidconnect/blob/master/TodoListWebApp/App_Start/Startup.Auth.cs

                    // Callback endpoint - you can make them up as you go along... no other coding required
                    o.CallbackPath = "/signin-microsoft";
                    // Must be different endpoint than /signin-oidc if you have multiple OpenID Connect log ins
                    // To avoid "Correlation Error"

                    // Id for development purposes only
                    o.ClientId = Configuration["Microsoft:id"];
                })
                // end of OpenID Connect Providers

                // You can add OAuth providers with another package
                // "AspNet.Security.OAuth.{ProviderName}" - https://github.com/TerribleDev/OwinOAuthProviders
                // The ones that currently come with Microsoft Authentication are;
                //.AddFacebook().AddTwitter().AddGoogle().AddMicrosoftAccount()
                .AddFacebook(o => {
                    // My app settings are set to upgrade all API calls to v3.2 anyway
                    // but here is a manual adjustment if needed
                    //o.AuthorizationEndpoint = "https://www.facebook.com/v3.2/dialog/oauth";

                    o.ClientId = Configuration["Facebook:id"];
                    o.ClientSecret = Configuration["Facebook:secret"];

                    // I want to get the user profile picture also
                    o.Fields.Add("picture");

                    o.Events = new OAuthEvents
                    {
                        OnCreatingTicket = context =>
                        {
                            // see the JSON data returned
                            Console.WriteLine("User" + context.User);

                            // Principal
                            var identity = (ClaimsIdentity)context.Principal.Identity;

                            // get the json data of the users profile image
                            var profileImg = context.User["picture"]["data"]["url"].ToString();

                            // add JWT Claim of Picture to the Principal
                            identity.AddClaim(new Claim(JwtClaimTypes.Picture, profileImg));

                            return Task.CompletedTask;
                        }
                    };
                })

                // OR add your own... OAUTH - SERVER FLOW - RESPONSE = "code"
                //
                // LinkedIn
                .AddOAuth("LinkedIn", "LinkedIn", o => // "Scheme", "DisplayName", Options =>
                {
                    // Documentation
                    // https://docs.microsoft.com/en-us/linkedin/shared/authentication/authentication?context=linkedin/consumer/context

                    o.AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
                    o.TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";

                    o.ClientId = Configuration["LinkedIn:id"];
                    o.ClientSecret = Configuration["LinkedIn:secret"];

                    // Callback endpoint - you can make them up as you go along... no other coding required
                    o.CallbackPath = "/signin-linkedin";

                    // These are the only scopes my application has access to
                    o.Scope.Add("r_basicprofile r_emailaddress");

                    // V2 endpoint... you may see a Forbidden error
                    // V1 endpoints will be deprecated by 1st march 2019
                    // https://engineering.linkedin.com/blog/2018/12/developer-program-updates
                    o.UserInformationEndpoint =
                        "https://api.linkedin.com/v1/people/~:(id,first-name,last-name,email-address)";

                    // "All developer applications created on the LinkedIn Developer Portal... 
                    // ...after January 14, 2019 have access to the LinkedIn v2 API by default"
                    //o.UserInformationEndpoint = "https://api.linkedin.com/v2/me";

                    // add headers to following backchannel request to show you want to use v2
                    //request.Headers.Add("X-RestLi-Protocol-Version", "2.0.0");

                    // Build up the claims required from the response to the userinfo endpoint
                    o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    o.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "firstName");
                    o.ClaimActions.MapJsonKey(ClaimTypes.Surname, "lastName");
                    o.ClaimActions.MapJsonKey(ClaimTypes.Email, "emailAddress");

                    // wait for v2 update to get picture as well
                    // https://developer.linkedin.com/docs/ref/v2/profile/profile-picture
                    //o.ClaimActions.MapJsonKey(JwtClaimTypes.Picture, "");

                    o.Events = new OAuthEvents
                    {
                        // the code has been exchanged for an access token
                        OnCreatingTicket = async context =>
                        {
                            // request info about the user
                            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);

                            // make sure the LinkedIn format is JSON
                            request.Headers.Add("x-li-format", "json");

                            // Usual OAuth Access Token Headers
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                            // Get the response from the server-server "back-channel" request
                            var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                            response.EnsureSuccessStatusCode();

                            // Create the user object
                            var user = JObject.Parse(await response.Content.ReadAsStringAsync());

                            // See the user object
                            //Console.Write("user... " + user);

                            // Use the user object to create claims for the Principal
                            context.RunClaimActions(user);

                        }
                    };

                 });

            //
            // END OF AUTHENTICATION SECTION
            //

            // MVC for the minimal QuickStart UI
            services.AddMvc();

        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }
    }
}