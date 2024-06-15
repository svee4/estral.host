using System.Globalization;
using System.Text.Unicode;
using Amazon.Runtime;
using Amazon.S3;
using Estral.Host.Web.Database;
using Estral.Host.Web.Infra;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
	// environment variables in production 
	builder.Configuration.AddJsonFile("appsettings.secret.json", optional: false);
}

builder.Services.AddNpgsql<AppDbContext>(builder.Configuration.GetRequiredValue("ConnectionStrings:Postgres"));
builder.Services.AddIdentity<User, IdentityRole<int>>(ConfigureIdentity)
	.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddSingleton<StorageHelper>();


var s3Credentials = new BasicAWSCredentials(
	builder.Configuration.GetRequiredValue("S3:ClientId"),
	builder.Configuration.GetRequiredValue("S3:ClientSecret"));

var s3Config = new AmazonS3Config
{
	ServiceURL = "https://" + builder.Configuration.GetRequiredValue("S3:ApiUrl"),
	Profile = null
};

builder.Services.AddScoped<IAmazonS3>(_ => new AmazonS3Client(s3Credentials, s3Config));


builder.Services.AddHttpClient();

builder.Services.AddRazorPages(options =>
{
	options.RootDirectory = "/Features";
});

builder.Services.ConfigureApplicationCookie(options =>
{
	options.Cookie.HttpOnly = true;
	options.Cookie.SameSite = SameSiteMode.Strict;
	options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
	options.Cookie.Path = "/";

	if (!int.TryParse(
		builder.Configuration.GetRequiredValue("Auth:CookieExpirationMinutes"),
		CultureInfo.InvariantCulture,
		out var expMinutes))
	{
		ConfigurationException.Throw("Value for 'Auth:CookieExpirationMinutes' must be parsable into an int");
	}

	options.Cookie.MaxAge = TimeSpan.FromMinutes(expMinutes);


});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
	app.UseForwardedHeaders();
	app.UseExceptionHandler("/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();


await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(dbContext.Database);
}


await app.RunAsync();


static void ConfigureIdentity(IdentityOptions options)
{
	// set allowed username characters
	// WHY ALLOWLIST INSTEAD OF DEFAULT ALLOW ? 
	// because unicode is hard

	const string BasicAscii = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
	const string SomeSpecialStuff = "åäöÅÄÖ";

	// add more stuff here as needed
	List<(int Start, int End)> ranges = [

		// hangul
		// strip off 12 invalid code points from the end
		(
			Start: UnicodeRanges.HangulSyllables.FirstCodePoint,
			End: UnicodeRanges.HangulSyllables.FirstCodePoint + UnicodeRanges.HangulSyllables.Length - 12
		),
	];

	var s = $"{BasicAscii}{SomeSpecialStuff}"
			+ string.Create(ranges.Sum(x => x.End - x.Start), ranges, static (span, ranges) =>
			{
				var i = 0;
				foreach (var (start, end) in ranges)
				{
					for (var j = start; j < end; j++, i++)
					{
						span[i] = (char)(j);
					}
				}
			});

	options.User.AllowedUserNameCharacters = s;
}
