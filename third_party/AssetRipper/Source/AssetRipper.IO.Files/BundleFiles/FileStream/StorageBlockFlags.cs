namespace AssetRipper.IO.Files.BundleFiles.FileStream;

[Flags]
public enum StorageBlockFlags
{
	CompressionTypeMask = 0x3F,

	Streamed = 0x40,
}

public static class StorageBlockFlagsExtensions
{
	public static CompressionType GetCompressionType(this StorageBlockFlags flags)
	{
		return (CompressionType)(flags & StorageBlockFlags.CompressionTypeMask);
	}

	public static bool IsStreamed(this StorageBlockFlags flags)
	{
		return (flags & StorageBlockFlags.Streamed) != 0;
	}

	public static StorageBlockFlags WithCompressionType(this StorageBlockFlags flags, CompressionType compressionType)
	{
		return (flags & ~StorageBlockFlags.CompressionTypeMask) | (StorageBlockFlags)compressionType;
	}
}
