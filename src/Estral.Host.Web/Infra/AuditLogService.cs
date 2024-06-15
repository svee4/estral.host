using Estral.Host.Web.Database;

namespace Estral.Host.Web.Infra;

public sealed class AuditLogService(AppDbContext dbContext, ILogger<AuditLogService> logger)
{
	private readonly AppDbContext _dbContext = dbContext;
	private readonly ILogger _logger = logger;

	public async Task Add(
		string category,
		string message,
		string? requestIp = null,
		string? username = null,
		int? userId = null,
		string? traceId = null,
		Dictionary<string, string>? data = null,
		CancellationToken token = default
		)
	{
		var ev = AuditEvent.Create(category, message, requestIp, username, userId, traceId, data);
		_logger.LogInformation("Audit event: {AuditEvent}", ev);
		await _dbContext.AuditEvents.AddAsync(ev, token);
		await _dbContext.SaveChangesAsync(token);
	}


	public static class Categories
	{
		public const string Authorization = "Authorization";
		public const string ContentDelete = "ContentDelete";
	}
}
