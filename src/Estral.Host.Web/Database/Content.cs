using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estral.Host.Web.Database;

public sealed class Content
{
	public const int TitleMaxLength = 50;
	public const int DescriptionMaxLength = 250;

	public int Id { get; private set; }
	public string Title { get; set; } = null!;
	public string? Description { get; set; }
	public DateTimeOffset Created { get; private set; }

	public int OwnerId { get; private set; }
	public User Owner { get; set; } = null!;

	public IList<Tag> Tags { get; private set; } = null!;

	private Content() { }

	public static Content Create(string title, string? description, User owner)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentNullException.ThrowIfNull(owner);

		return new Content()
		{
			Title = title,
			Description = description,
			Created = DateTimeOffset.UtcNow,
			OwnerId = owner.Id,
			Owner = owner,
			Tags = []
		};
	}

	public class Configuration : IEntityTypeConfiguration<Content>
	{
		[SuppressMessage("", "IDE0058")] // expression value is never used
		public void Configure(EntityTypeBuilder<Content> builder)
		{
			builder.HasIndex(m => m.Title);

			builder.Property(m => m.Title)
				.IsRequired()
				.HasMaxLength(TitleMaxLength);

			builder.HasIndex(m => m.OwnerId);

			builder.HasIndex(m => new { m.OwnerId, m.Title })
				.IsUnique();
		}
	}
}
