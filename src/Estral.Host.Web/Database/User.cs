using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estral.Host.Web.Database;

public sealed class User : IdentityUser<int>
{
	public const int UsernameMaxLength = 32;
	public const int ProfileDescriptionMaxLength = 400;

	public ulong DiscordId { get; private set; }

	public string? ProfileDescription { get; set; }

	public DateTimeOffset Created {  get; private set; }

	// make non-nullable because its required
	public new string UserName
	{
		get => base.UserName!;
		set => base.UserName = value;
	}

	private User() { }

	public static User Create(ulong discordId, string username)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(discordId);
		ArgumentException.ThrowIfNullOrWhiteSpace(username);

		return new User()
		{
			DiscordId = discordId,
			UserName = username,
			Created = DateTimeOffset.UtcNow
		};
	}

	public class Configuration : IEntityTypeConfiguration<User>
	{

		[SuppressMessage("", "IDE0058")] // expression value is never used
		public void Configure(EntityTypeBuilder<User> builder)
		{
			builder.Property(m => m.UserName)
				.IsRequired()
				.HasMaxLength(UsernameMaxLength);

			builder.Property(m => m.ProfileDescription)
				.HasMaxLength(ProfileDescriptionMaxLength);

			builder.Property(m => m.DiscordId)
				.IsRequired();
		}
	}
}
