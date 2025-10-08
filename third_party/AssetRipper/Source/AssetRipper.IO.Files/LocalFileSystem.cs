namespace AssetRipper.IO.Files;

public partial class LocalFileSystem : FileSystem
{
	public static LocalFileSystem Instance { get; } = new();

	public partial class LocalFileImplementation
	{
	}

	public partial class LocalDirectoryImplementation
	{
		public override void Create(string path) => System.IO.Directory.CreateDirectory(path);

		public override void Delete(string path) => System.IO.Directory.Delete(path, true);
	}

	private static string ExecutingDirectory => AppContext.BaseDirectory;

	public string LocalTemporaryDirectory => Path.Join(ExecutingDirectory, "temp", GetRandomString()[0..4]);

	private string? _temporaryDirectory;
	public override string TemporaryDirectory
	{
		get => _temporaryDirectory ??= LocalTemporaryDirectory;
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				_temporaryDirectory = Path.GetFullPath(value);
			}
		}
	}
}
