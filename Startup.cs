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
            // ASP.NET IDENTITY
            // Good video to get started : https://www.youtube.com/watch?v=ipAwOGzpxpQ
            services
                .AddIdentity<ApplicationUser, ElCamino.AspNetCore.Identity.AzureTable.Model.IdentityRole>()

            // Using the ElCamino.Azure.Table package from Dave Melendez -  https://github.com/dlmelendez/identityazuretable
                .AddAzureTableStoresV2<ApplicationDbContext>(new Func<IdentityConfiguration>(() =>
                {
                    // you can configure this for AzureStorageEmulator.exe - see documentation below
                    // https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator#storage-emulator-command-line-tool-reference
                    // Or start the emulator and add connection string, as below, to your appsettings
                    // DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;
                    // or create new Blob Storage resource

                    IdentityConfiguration idconfig = new IdentityConfiguration();
                    //idconfig.TablePrefix = Configuration["AzureTable:Prefix"];
                    idconfig.StorageConnectionString = Configuration["AzureTable:ConnectionString"];
                    //idconfig.LocationMode = Configuration["AzureTable:LocationMode"];
                    return idconfig;

                }))
                .AddDefaultTokenProviders();
                //
                // // Run this the first time you Publish the app to create the required databases in your Cloud Blob Storage account
                //
                //.CreateAzureTablesIfNotExists<ApplicationDbContext>();

            // IDENTITY SERVER
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


            // OPENID CONNECT EXTERNAL PROVIDERS - Client_Id for demo purposes only ( no APIs enabled )
            // RESPONSE = "id_token", SCOPE = "openid profile", using default "IdentityConstants.External" cookie
            services.AddAuthentication()
                .AddOpenIdConnect("Google", "Google", o => // "Scheme", "DisplayName", Options =>
                {
                    // https://accounts.google.com/.well-known/openid-configuration
                    o.Authority = "https://accounts.google.com";
                    // Client_Id for demo purposes only ( no APIs enabled )
                    o.ClientId = "842409198808-ul3vj7qra1nrr4fq6qm6rkjss6hckqun.apps.googleusercontent.com";
                });
            
            
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