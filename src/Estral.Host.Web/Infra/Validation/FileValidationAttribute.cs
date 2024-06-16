using System.ComponentModel.DataAnnotations;
using Estral.Host.Web.Infra.Extensions;

namespace Estral.Host.Web.Infra.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FileValidationAttribute : ValidationAttribute
{
	public required int MaxInclusiveSizeInBytes { get; set; }
	public string[]? AllowedFileTypes { get; set; }
	public FileTypes.Preset AllowedFileTypesPreset { get; set; }

	private string? _fileSizeValidationFailureMessage;
	public string FileSizeValidationFailureMessage { 
		get => _fileSizeValidationFailureMessage ??= $"File size too large. Maximum size is {MaxInclusiveSizeInBytes} bytes";
		set => _fileSizeValidationFailureMessage = value;
	}

	private string? _allowedFileTypesValidationFailureMessage;
	public string AllowedFileTypesValidationFailureMessage
	{
		get => _allowedFileTypesValidationFailureMessage ??= $"Invalid file types. Allowed types are: {AllowedFileTypes?.StringJoin(", ")}";
		set => _allowedFileTypesValidationFailureMessage = value;
	}

	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value is null)
		{
			return ValidationResult.Success;
		}

		if (value is not IFormFile formFile)
		{
			throw new InvalidOperationException($"{nameof(FileValidationAttribute)} can only validate values of type {nameof(IFormFile)}");
		}

		if (formFile.Length > MaxInclusiveSizeInBytes) 
		{
			return new ValidationResult(FileSizeValidationFailureMessage);
		}

		var allowedFileTypes = AllowedFileTypes;

		if (AllowedFileTypesPreset > 0)
		{
			if (allowedFileTypes is not null)
			{
				throw new InvalidOperationException($"Provide either {nameof(AllowedFileTypesPreset)} or {nameof(AllowedFileTypes)}, but not both");
			}

			allowedFileTypes = FileTypes.GetFileTypesForPreset(AllowedFileTypesPreset);
		}

		if (allowedFileTypes is null)
		{
			return ValidationResult.Success;
		}

		var maxLength = 0;

		foreach (var type in allowedFileTypes)
		{
			var maxLength2 = type switch
			{
				FileTypes.Webp => Signatures.WebpStart.Length + 4 + Signatures.WebpEnd.Length,
				FileTypes.Gif => Math.Max(Signatures.Gif1.Length, Signatures.Gif2.Length),
				FileTypes.Jpg => Signatures.Jpg.Length,
				FileTypes.Png => Signatures.Png.Length,
				_ => throw new InvalidOperationException($"Unhandled file type {type}")
			};
			maxLength = Math.Max(maxLength, maxLength2);
		}

		using var stream = formFile.OpenReadStream();
		var bytes = new byte[maxLength];
		stream.ReadExactly(bytes);

		foreach (var type in allowedFileTypes)
		{
			var matches = type switch
			{
				FileTypes.Webp => MatchSignature(bytes, Signatures.WebpStart)
							&& MatchSignature(bytes, Signatures.WebpEnd, Signatures.WebpStart.Length + 4),
				FileTypes.Gif => MatchSignature(bytes, Signatures.Gif1)
							|| MatchSignature(bytes, Signatures.Gif2),
				FileTypes.Jpg => MatchSignature(bytes, Signatures.Jpg),
				FileTypes.Png => MatchSignature(bytes, Signatures.Png),
				_ => throw new InvalidOperationException($"Unhandled file type {type}")
			};

			if (matches)
			{
				return ValidationResult.Success;
			}
		}

		return new ValidationResult(AllowedFileTypesValidationFailureMessage);

		static bool MatchSignature(byte[] bytes, ReadOnlySpan<byte> signature, int offset = 0) =>
			bytes[offset..signature.Length].AsSpan().SequenceEqual(signature);
	}

	private static class Signatures
	{
		public static ReadOnlySpan<byte> WebpStart => "RIFF"u8;
		public static ReadOnlySpan<byte> WebpEnd => "WEBP"u8;
		public static ReadOnlySpan<byte> Gif1 => "GIF87a"u8;
		public static ReadOnlySpan<byte> Gif2 => "GIF89a"u8;
		public static ReadOnlySpan<byte> Jpg => [0xFF, 0xD8, 0xFF, 0xE0];
		public static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
	}

	public static class FileTypes
	{
		public const string Webp = "webp";
		public const string Gif = "gif";
		public const string Jpg = "jpg";
		public const string Png = "png";

		public enum Preset
		{
			ImageAndGif = 1,
		}

		public static string[] GetFileTypesForPreset(Preset preset) => preset switch
		{
			Preset.ImageAndGif => [Webp, Gif, Jpg, Png],
			_ => throw new InvalidOperationException($"Unknown {nameof(Preset)} {preset}")
		};
	}
}
