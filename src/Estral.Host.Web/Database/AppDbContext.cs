using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Database;

public sealed class AppDbContext(DbContextOptions options) : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
	public DbSet<Content> Contents {  get; private set; }
	public DbSet<AuditEvent> AuditEvents { get; private set; }
	public DbSet<Tag> Tags { get; private set; }

	protected override void OnModelCreating(ModelBuilder builder) 
	{
		base.OnModelCreating(builder);
		builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
	}
}
