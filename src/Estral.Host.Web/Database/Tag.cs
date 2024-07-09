using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Estral.Host.Web.Database;

public sealed class Tag
{
	public const int NameMaxLength = 32;

	public int Id { get; private set; }
	public string Name { get; set; } = null!;
	public DateTimeOffset Created { get; private set; }

	public IReadOnlyList<Content> Contents { get; private set; }

	private Tag() { }

	public static Tag Create(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return new Tag
		{
			Name = name,
			Created = DateTimeOffset.UtcNow,
		};
	}

	public class Configuration : IEntityTypeConfiguration<Tag>
	{

		[SuppressMessage("", "IDE0058")] // expression value is never used
		public void Configure(EntityTypeBuilder<Tag> builder)
		{
			builder.HasIndex(m => m.Name);

			builder.Property(m => m.Name)
				.IsRequired()
				.HasMaxLength(NameMaxLength);
		}
	}
}
