using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Estral.Host.Web.Infra.Extensions;

public static class ConfigurationExtensions
{
	public static string GetRequiredValue(this IConfiguration configuration, string key)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		return StringExtensions.NormalizeToNull(configuration[key]) is { } value
			? value
			: ConfigurationException.Throw<string>($"Configuration did not contain required value for key '{key}'");
	}
}

public sealed class ConfigurationException : Exception
{
	public ConfigurationException() { }
	public ConfigurationException(string? message) : base(message) { }
	public ConfigurationException(string? message, Exception? innerException) : base(message, innerException) { }

	[DoesNotReturn, StackTraceHidden]
	public static void Throw(string message) => throw new ConfigurationException(message);

	[DoesNotReturn, StackTraceHidden]
	public static T Throw<T>(string message) => throw new ConfigurationException(message);

}
