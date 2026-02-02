using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the Code tab: browse vanilla decompiled code (read-only)
/// and edit per-modpack source files.
/// </summary>
public sealed class CodeEditorViewModel : ViewModelBase
{
    private readonly ModpackManager _modpackManager;
    private readonly VanillaCodeService _vanillaCodeService;
    private readonly CompilationService _compilationService;

    public CodeEditorViewModel()
    {
        _modpackManager = new ModpackManager();
        _vanillaCodeService = new VanillaCodeService();
        _compilationService = new CompilationService();
        VanillaCodeTree = new ObservableCollection<CodeTreeNode>();
        ModSourceTree = new ObservableCollection<CodeTreeNode>();
        AvailableModpacks = new ObservableCollection<string>();

        LoadModpacks();
        LoadVanillaTree();
    }

    internal ModpackManager ModpackManager => _modpackManager;

    public ObservableCollection<CodeTreeNode> VanillaCodeTree { get; }
    public ObservableCollection<CodeTreeNode> ModSourceTree { get; }
    public ObservableCollection<string> AvailableModpacks { get; }

    // ---------------------------------------------------------------
    // Selected modpack
    // ---------------------------------------------------------------

    private string? _selectedModpack;
    public string? SelectedModpack
    {
        get => _selectedModpack;
        set
        {
            if (_selectedModpack != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedModpack, value);
                LoadModSourceTree();
            }
        }
    }

    // ---------------------------------------------------------------
    // Selected file and content
    // ---------------------------------------------------------------

    private CodeTreeNode? _selectedFile;
    public CodeTreeNode? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (_selectedFile != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedFile, value);
                LoadFileContent();
            }
        }
    }

    private string _fileContent = string.Empty;
    public string FileContent
    {
        get => _fileContent;
        set => this.RaiseAndSetIfChanged(ref _fileContent, value);
    }

    private bool _isReadOnly = true;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    private string _currentFilePath = string.Empty;
    public string CurrentFilePath
    {
        get => _currentFilePath;
        set => this.RaiseAndSetIfChanged(ref _currentFilePath, value);
    }

    // ---------------------------------------------------------------
    // Build output (populated by Phase 4 compilation)
    // ---------------------------------------------------------------

    private string _buildOutput = string.Empty;
    public string BuildOutput
    {
        get => _buildOutput;
        set => this.RaiseAndSetIfChanged(ref _buildOutput, value);
    }

    private string _buildStatus = string.Empty;
    public string BuildStatus
    {
        get => _buildStatus;
        set => this.RaiseAndSetIfChanged(ref _buildStatus, value);
    }

    // ---------------------------------------------------------------
    // Data loading
    // ---------------------------------------------------------------

    private void LoadModpacks()
    {
        AvailableModpacks.Clear();
        foreach (var mp in _modpackManager.GetStagingModpacks())
            AvailableModpacks.Add(mp.Name);

        if (AvailableModpacks.Count > 0 && _selectedModpack == null)
            SelectedModpack = AvailableModpacks[0];
    }

    private void LoadVanillaTree()
    {
        VanillaCodeTree.Clear();
        var tree = _vanillaCodeService.BuildVanillaCodeTree();
        if (tree != null)
            VanillaCodeTree.Add(tree);
    }

    private void LoadModSourceTree()
    {
        ModSourceTree.Clear();

        if (string.IsNullOrEmpty(_selectedModpack))
            return;

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null) return;

        var tree = VanillaCodeService.BuildModSourceTree(modpack.Path, modpack.Name);
        ModSourceTree.Add(tree);
    }

    private void LoadFileContent()
    {
        if (_selectedFile == null || !_selectedFile.IsFile)
        {
            FileContent = string.Empty;
            IsReadOnly = true;
            CurrentFilePath = string.Empty;
            return;
        }

        CurrentFilePath = _selectedFile.FullPath;
        IsReadOnly = _selectedFile.IsReadOnly;

        try
        {
            FileContent = File.ReadAllText(_selectedFile.FullPath);
        }
        catch (Exception ex)
        {
            FileContent = $"// Error loading file: {ex.Message}";
            IsReadOnly = true;
        }
    }

    // ---------------------------------------------------------------
    // File operations
    // ---------------------------------------------------------------

    public void SaveFile()
    {
        if (_selectedFile == null || _selectedFile.IsReadOnly || string.IsNullOrEmpty(_selectedModpack))
            return;

        try
        {
            File.WriteAllText(_selectedFile.FullPath, FileContent);

            // Also sync the manifest's source list
            var relativePath = GetModRelativePath(_selectedFile.FullPath);
            if (relativePath != null)
                _modpackManager.SaveStagingSource(_selectedModpack, relativePath, FileContent);

            BuildStatus = $"Saved: {_selectedFile.Name}";
        }
        catch (Exception ex)
        {
            BuildStatus = $"Save failed: {ex.Message}";
        }
    }

    public void AddFile(string fileName)
    {
        if (string.IsNullOrEmpty(_selectedModpack) || string.IsNullOrEmpty(fileName))
            return;

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        var relativePath = Path.Combine("src", fileName);
        _modpackManager.AddStagingSource(_selectedModpack, relativePath);
        LoadModSourceTree();
        BuildStatus = $"Added: {fileName}";
    }

    public void RemoveFile()
    {
        if (_selectedFile == null || _selectedFile.IsReadOnly || string.IsNullOrEmpty(_selectedModpack))
            return;

        var relativePath = GetModRelativePath(_selectedFile.FullPath);
        if (relativePath == null) return;

        _modpackManager.RemoveStagingSource(_selectedModpack, relativePath);
        FileContent = string.Empty;
        SelectedFile = null;
        LoadModSourceTree();
        BuildStatus = "File removed";
    }

    /// <summary>
    /// Compile the selected modpack's source code using Roslyn.
    /// </summary>
    public async Task BuildModpackAsync()
    {
        if (string.IsNullOrEmpty(_selectedModpack))
        {
            BuildStatus = "No modpack selected";
            return;
        }

        var modpacks = _modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (manifest == null)
        {
            BuildStatus = "Modpack not found";
            return;
        }

        if (!manifest.Code.HasAnySources)
        {
            BuildStatus = "No source files to compile";
            return;
        }

        BuildStatus = "Compiling...";
        BuildOutput = string.Empty;

        var result = await _compilationService.CompileModpackAsync(manifest);

        // Format output
        var sb = new StringBuilder();

        if (result.SecurityWarnings.Count > 0)
        {
            sb.AppendLine("=== Security Scan ===");
            foreach (var warning in result.SecurityWarnings)
                sb.AppendLine(warning.ToString());
            sb.AppendLine();
        }

        if (result.Diagnostics.Count > 0)
        {
            sb.AppendLine("=== Compilation ===");
            foreach (var diag in result.Diagnostics)
                sb.AppendLine(diag.ToString());
            sb.AppendLine();
        }

        if (result.Success)
        {
            sb.AppendLine($"Build succeeded: {result.OutputDllPath}");

            // Update security status based on scan results
            if (result.SecurityWarnings.Count == 0)
            {
                manifest.SecurityStatus = SecurityStatus.SourceVerified;
                BuildStatus = "Build succeeded - Source Verified";
            }
            else
            {
                manifest.SecurityStatus = SecurityStatus.SourceWithWarnings;
                BuildStatus = $"Build succeeded - {result.SecurityWarnings.Count} security warning(s)";
            }
            manifest.SaveToFile();
        }
        else
        {
            var errorCount = result.Diagnostics.Count(d => d.Severity == Models.DiagnosticSeverity.Error);
            BuildStatus = $"Build failed - {errorCount} error(s)";
        }

        BuildOutput = sb.ToString();
    }

    public void RefreshAll()
    {
        LoadModpacks();
        LoadVanillaTree();
        LoadModSourceTree();
    }

    private string? GetModRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(_selectedModpack)) return null;
        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null) return null;

        try
        {
            return Path.GetRelativePath(modpack.Path, fullPath);
        }
        catch
        {
            return null;
        }
    }
}
