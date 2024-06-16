using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estral.Host.Web.Database;

public sealed class AuditEvent
{
	public int Id { get; private set; }
	public string Category { get; private set; }
	public string? Message { get; private set; }
	public string? RequestIp { get; private set; }
	public string? Username { get; private set; }
	public int? UserId { get; private set; }
	public string? TraceId { get; private set; }
	// stored as json
	public IReadOnlyDictionary<string, string?>? Data { get; private set; }
	public DateTimeOffset Created { get; private set; }

	private AuditEvent() { }

	public static AuditEvent Create(
			string category,
			string? message,
			string? requestIp,
			string? username,
			int? userId,
			string? traceId,
			IReadOnlyDictionary<string, string?>? data) =>
		new AuditEvent
		{
			Category = category,
			Message = message,
			RequestIp = requestIp,
			Username = username,
			UserId = userId,
			TraceId = traceId,
			Data = data,
			Created = DateTimeOffset.UtcNow
		};

	public class Configuration : IEntityTypeConfiguration<AuditEvent>
	{

		[SuppressMessage("", "IDE0058")] // expression value is never used
		public void Configure(EntityTypeBuilder<AuditEvent> builder)
		{
			builder.Property(m => m.Data)
				.HasConversion(
					to => JsonSerializer.Serialize(to, default(JsonSerializerOptions)),
					from => JsonSerializer.Deserialize<Dictionary<string, string?>?>(from, default(JsonSerializerOptions)));
		}
	}

}
