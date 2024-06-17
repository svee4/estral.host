using Estral.Host.Web.Database;

namespace Estral.Host.Web.Infra;

public sealed class AuditLogService(AppDbContext dbContext, ILogger<AuditLogService> logger)
{
	private readonly AppDbContext _dbContext = dbContext;
	private readonly ILogger _logger = logger;

	public async Task Add(Entry entry, CancellationToken token = default)
	{
		var ev = AuditEvent.Create(
			category: entry.Category,
			message: entry.Message,
			requestIp: entry.RequestIp,
			username: entry.Username,
			userId: entry.UserId,
			traceId: entry.TraceId,
			data: entry.Data);

		_logger.LogInformation("Audit event: {AuditEvent}", ev);
		await _dbContext.AuditEvents.AddAsync(ev, token);
		await _dbContext.SaveChangesAsync(token);
	}

	public sealed class Entry
	{
		public required string Category { get; set; }
		public string? Message { get; set; }
		public string? RequestIp { get; set; }
		public string? Username { get; set; }
		public int? UserId { get; set; }
		public string? TraceId { get; set; }
		public Dictionary<string, string?>? Data { get; set; }
	}

	public static class Categories
	{
		public const string Authorization = "Authorization";
		public const string ContentDelete = "ContentDelete";
		public const string ContentUpload = "ContentUpload";
		public const string SettingsUpdate = "SettingsUpdate";
	}
}
