using AssetRipper.IO.Endian;

namespace AssetRipper.IO.Files.BundleFiles;

public abstract record class Node : IEndianWritable
{
	public override string ToString() => PathFixed;

	public abstract void Write(EndianWriter writer);

	public string PathFixed { get; private set; } = string.Empty;

	private string _path = string.Empty;
	public string Path
	{
		get => _path;
		set
		{
			_path = value;
			PathFixed = SpecialFileNames.FixFileIdentifier(value);
		}
	}
	public long Offset { get; set; }
	public long Size { get; set; }
}
