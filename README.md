# External Authentication only STS with IdentityServer4, ASP.NET Identity, and ElCamino for Azure Table Storage.

My aim is to show External Provider log in only for a STS user authentication flow.
	A simple authentication system, to get users signed up quickly and securely.
		I do want to get their image/picture/avatar as well though, to pass on to the clients, if available.


I've enabled the Quickstart UI Home View to show the claims without needing to add a Client.
- Got to the [Home Controller](./Quickstart/Home/HomeController.cs) & uncomment the code to make it as it should be for production purposes.


But you can add your own Client to see how it works as a STS, or go to [Client Demo](https://devops-client.firebaseapp.com/) to see a live Angular ( Firebase ) Client demo.


I'm persisting data to Azure Storage Table, through ElCamino package by Dave Melendez.
- https://github.com/dlmelendez/identityazuretable
- https://www.nuget.org/packages/ElCamino.AspNet.Identity.AzureTable/


In my [Startup](./Startup.cs) file;
	- ElCamino configuration -or- local Azure Storage Emulator
	- OpenID Connect Implicit Flow for Google and Microsoft - .AddOpenIdConnect()
	- ASP.NET Identity default provided server flow with - .AddFacebook()
	- OAuth example also using the server flow - .AddOAuth()

In my [External Controller](./Quickstart/Account/ExternalController.cs);
	- AutoRegister function to provision and persist a new user provided by the external log in.
	- UpdateClaims to check if they've updated either their name or picture since their last log in


### How to use

Copy the repo, restore Nuget packages and run off https://localhost:5001 in your launchSettings.json for development.
	- Use https as some providers will only accept SSL
Configure an app in each of the external providers you want to add, and make sure you include the correct callback uri.
	- eg. "https://localhost:5001/signin-{nameOfExternalProvider}"
	- And, add API for LogIn or OpenID and Profile scopes... whatever your provider calls those resources.
Then, add application id and secret to your application's settings or secret manager.


OR - try the STS now

Visit live [demo website](https://demo-sts-authentication-only.azurewebsites.net/) and log in with a provider you have an account with to see the claims that will be passed on to the Client


OR - try a Client

Visit live [demo Client](https://devops-client.firebaseapp.com/)


OR

Connect using your own Client... at localhost:4200 with these settings
	- You can copy this [Angular Starter Client](https://github.com/HennesseyWilkes/AngularClient) to run a basic app using this Security Token Service with AuthService and AuthGuard.

	CONNECTION SETTINGS
    authority: 'https://demo-sts-authentication-only.azurewebsites.net', or 'https://localhost:5001' if you're running it locally,
    client_id: 'Client',
    response_type: 'id_token',
    redirect_uri: 'http://localhost:4200/auth-callback', or whatever you choose to use to handle the OIDC callback
    post_logout_redirect_uri: 'http://localhost:4200',
    scope: 'openid profile'


OR... none of the above.


#### My journey through the first steps in identity;

The desire to make users information secure and managed over any applications I develop in the future, brought me to the identity space.

I first came to this [video tutorial](https://mva.microsoft.com/en-us/training-courses/introduction-to-identityserver-for-aspnet-core-17945) with Brock Allen and Scott Hanselman in the Microsoft Virtual Academy.
They really cover a lot in only a couple of hours. Great for starting out, like I was.

The on to watching Nate Barbetini for Okta and for Microsoft, explaing about [OpenID, OAuth](https://www.youtube.com/watch?v=996OiexHze0&index=2&t=0s&list=PLpV5iycDoGxmheirI659J7E22DvS5AwVk) and [ASP.NET Identity](https://www.youtube.com/watch?v=996OiexHze0&index=2&t=0s&list=PLpV5iycDoGxmheirI659J7E22DvS5AwVk).
He speaks clearly about the identity space and the two protocols, OpenID and OAuth.

I found a more thorough explanation of ASP.NET identity with [this video](https://www.youtube.com/watch?v=ipAwOGzpxpQ) by Adam Tuliper and Jeremy Foster.
This seems like a much deeper dive than Nate's videos, although I recommend watching both. Repeatedly.

Along the way, with these great introductions I spent a lot of time in Visual Studio, quickly discovering bugs in my code and patiently finding the answers;
through lots of stackoverflows from people like myself, pouring over the documentation, dicovering well-known endpoints, and protocol specifications.

I chose to persist the user data to Azure Storage, mostly because i didn't want to pay any extra costs associated with a SQL Server, but after
a while because I just wanted to prove it wasn't complicated.

I chose to run with my decision of using Azure Storage, although creating my own ( of any great quailty for production ) would take longer than I wanted.

That lead me to install Dave Melendez's package which provides an excellent and professional solution.

Although I will come back to this and create something more streamlined for my specific use case.

So here we are with my demo for the first step in Identity and creating a STS for Authentication, with various examples of how to include
external providers.


The next step will be adding APIs protected behind TFA.

..and moving away from InMemory sources into a better interface for registering Clients and the like.



### References:

#### OpenID Connect & OAuth
	DOCS
		- [OpenID Connect](https://openid.net/connect/)
		- [OAuth](https://oauth.net/2/)
	VIDEO
		- [Nate Barbettini for Okta - 1 hour](https://www.youtube.com/watch?v=996OiexHze0&index=2&t=0s&list=PLpV5iycDoGxmheirI659J7E22DvS5AwVk)
	EXAMPLES
		- [Jerrie Pelser](https://www.jerriepelser.com/blog/authenticate-oauth-aspnet-core-2/)

#### IdentityServer 4
	DOCS
		- [Official Documentaion](https://identityserver4.readthedocs.io/en/release/)
	NEWS
		- [Dom Baier](https://leastprivilege.com/)
	VIDEO
		- [Brock Allen & Scott Hansleman - 2 hours](https://mva.microsoft.com/en-us/training-courses/introduction-to-identityserver-for-aspnet-core-17945)
	EXAMPLES
		- [Startup.cs](https://github.com/IdentityServer/IdentityServer4/blob/master/host/Startup.cs)

#### ASP.NET Identity
	DOCS
		- [Introduction to Identity](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
	VIDEO
		- [Jeremy Foster & Adam Tuliper - 4:30 hours](https://www.youtube.com/watch?v=ipAwOGzpxpQ)
		- [Identity with Nate - 1 hour talk @Microsoft](https://www.youtube.com/watch?v=z2iCddrJRY8)
	EXAMPLES
		- [Introduction to Identity](https://digitalmccullough.com/posts/aspnetcore-auth-system-demystified.html)
	EXTERNAL PROVIDERS
		- Google = [Register your app](https://console.cloud.google.com) & [See the docs](https://myaccount.google.com/.well-known/openid-configuration)
		- Microsoft = [Register your app](https://apps.dev.microsoft.com) & [See the docs](https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration)
		- LinkedIn = [Register your app](https://www.linkedin.com/developer/apps) & [See the docs](https://docs.microsoft.com/en-us/linkedin/shared/authentication/authentication?context=linkedin/consumer/context)
		- facebook = [Register your app](https://developers.facebook.com/) & [See the docs](https://developers.facebook.com/docs/graph-api/reference/)
		- [Google Authentication for .NET](https://cloud.google.com/dotnet/docs/getting-started/authenticate-users)
		- [Google OpenID Connect, how to...](https://developers.google.com/identity/protocols/OpenIDConnect)
		- [Microsoft OpenID Connect, how to...](https://docs.microsoft.com/en-us/azure/active-directory/develop/v1-protocols-openid-connect-code)
	USEFUL DOCS
		- [User Manager, and methods.](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1)
		- [UserStore, and methods.](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.iuserstore-1)
		- [UserClaims, and methods.](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.iuserclaimstore-1)
		- [UserLogin, and methods.](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.iuserloginstore-1)
		- [Persist additional claims](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/additional-claims)

#### Azure Table Storage
	DOCS
		- [Quickstarts](https://docs.microsoft.com/en-us/azure/storage/)
		- [Table Service REST APIs](https://docs.microsoft.com/en-us/rest/api/storageservices/table-service-rest-api)
		- [Design Guide](https://docs.microsoft.com/en-us/azure/cosmos-db/table-storage-design-guide)
		- [Concurrency in Azure Blob Storage](https://azure.microsoft.com/es-es/blog/managing-concurrency-in-microsoft-azure-storage-2/)
		- [Overview for .NET](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity-custom-storage-providers)
		- [Getting Started with .NET](https://docs.microsoft.com/en-gb/azure/cosmos-db/table-storage-how-to-use-dotnet)
	EXAMPLES
		- [Tahir Naushad tutorial](https://www.c-sharpcorner.com/article/azure-table-storage-in-asp-net-core-2-0/)
		- [Tahir Naushad on GitHub](https://github.com/TahirNaushad/Fiver.Azure.Table)
		- [RavenDb example on GitHub, Startup.cs configuration.](https://github.com/JudahGabriel/RavenDB.Identity/blob/master/RavenDB.Identity/UserStore.cs)
		- [Dave Melendez on GitHub](https://github.com/dlmelendez/identityazuretable/)
	PACKAGES
		- [El Camino by Dave Melendez](https://www.nuget.org/packages/ElCamino.AspNet.Identity.AzureTable/)
	DEVELOPMENT
		- [Storage Emulator, on SSMS on (local)\MSSQLLocalDb engine](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator)
