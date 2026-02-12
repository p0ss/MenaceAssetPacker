using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using ReactiveUI;

namespace Menace.Modkit.App.ViewModels;

public sealed class SaveEditorViewModel : ViewModelBase
{
    private readonly SaveFileService _saveFileService;
    private readonly List<SaveFileHeader> _allSaveFiles = new();

    private SaveFileHeader? _selectedSave;
    private string _statusMessage = string.Empty;
    private bool _showNoSavesWarning;
    private bool _hasUnsavedChanges;
    private string _searchText = string.Empty;

    // Editable header fields
    private string _editableSaveGameName = string.Empty;
    private string _editablePlanetName = string.Empty;
    private string _editableOperationName = string.Empty;
    private string _editableDifficulty = string.Empty;
    private string _editableStrategyConfigName = string.Empty;
    private int _editableCompletedMissions;
    private int _editableOperationLength;
    private double _editablePlayTimeSeconds;
    private DateTime _editableSaveTime;
    private SaveStateType _editableSaveStateType;

    // Editable body fields
    private bool _editableIronman;
    private int _editableSeed;
    private int _editableCredits;
    private int _editableIntelligence;
    private int _editableAuthority;
    private int _editablePromotionPoints;

    public SaveEditorViewModel()
    {
        _saveFileService = new SaveFileService();
        SaveFiles = new ObservableCollection<SaveFileHeader>();

        RefreshCommand = ReactiveCommand.Create(LoadSaveFiles);
        SaveChangesCommand = ReactiveCommand.Create(SaveChanges);
        DeleteSelectedSaveCommand = ReactiveCommand.Create(DeleteSelectedSave);
        DuplicateSelectedSaveCommand = ReactiveCommand.Create(DuplicateSelectedSave);
        OpenSaveFolderCommand = ReactiveCommand.Create(OpenSaveFolder);
    }

    /// <summary>
    /// Available save state types for the dropdown.
    /// </summary>
    public SaveStateType[] SaveStateTypes => new[]
    {
        SaveStateType.Manual,
        SaveStateType.Auto,
        SaveStateType.Quick,
        SaveStateType.Ironman
    };

    /// <summary>
    /// Collection of discovered save files (filtered by search).
    /// </summary>
    public ObservableCollection<SaveFileHeader> SaveFiles { get; }

    /// <summary>
    /// Search text for filtering saves.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Currently selected save file.
    /// </summary>
    public SaveFileHeader? SelectedSave
    {
        get => _selectedSave;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSave, value);
            if (value != null)
            {
                // Populate all editable fields from the selected save
                _editableSaveGameName = value.SaveGameName;
                _editablePlanetName = value.PlanetName;
                _editableOperationName = value.OperationName;
                _editableDifficulty = value.Difficulty;
                _editableStrategyConfigName = value.StrategyConfigName;
                _editableCompletedMissions = value.CompletedMissions;
                _editableOperationLength = value.OperationLength;
                _editablePlayTimeSeconds = value.PlayTimeSeconds;
                _editableSaveTime = value.SaveTime;
                _editableSaveStateType = value.SaveStateType;

                // Notify all editable properties changed
                this.RaisePropertyChanged(nameof(EditableSaveGameName));
                this.RaisePropertyChanged(nameof(EditablePlanetName));
                this.RaisePropertyChanged(nameof(EditableOperationName));
                this.RaisePropertyChanged(nameof(EditableDifficulty));
                this.RaisePropertyChanged(nameof(EditableStrategyConfigName));
                this.RaisePropertyChanged(nameof(EditableCompletedMissions));
                this.RaisePropertyChanged(nameof(EditableOperationLength));
                this.RaisePropertyChanged(nameof(EditablePlayTimeSeconds));
                this.RaisePropertyChanged(nameof(EditablePlayTimeFormatted));
                this.RaisePropertyChanged(nameof(EditableSaveTime));
                this.RaisePropertyChanged(nameof(EditableSaveStateType));

                HasUnsavedChanges = false;

                // Parse body data on selection
                if (value.BodyData == null && value.IsValid)
                {
                    value.BodyData = _saveFileService.ParseBody(value);
                }

                // Populate editable body fields
                if (value.BodyData != null)
                {
                    _editableIronman = value.BodyData.Ironman;
                    _editableSeed = value.BodyData.Seed;
                    _editableCredits = value.BodyData.Credits;
                    _editableIntelligence = value.BodyData.Intelligence;
                    _editableAuthority = value.BodyData.Authority;
                    _editablePromotionPoints = value.BodyData.PromotionPoints;

                    this.RaisePropertyChanged(nameof(EditableIronman));
                    this.RaisePropertyChanged(nameof(EditableSeed));
                    this.RaisePropertyChanged(nameof(EditableCredits));
                    this.RaisePropertyChanged(nameof(EditableIntelligence));
                    this.RaisePropertyChanged(nameof(EditableAuthority));
                    this.RaisePropertyChanged(nameof(EditablePromotionPoints));
                }
            }
            this.RaisePropertyChanged(nameof(HasSelectedSave));
            this.RaisePropertyChanged(nameof(ScreenshotPath));
            this.RaisePropertyChanged(nameof(BodyData));
            this.RaisePropertyChanged(nameof(HasBodyData));
            this.RaisePropertyChanged(nameof(IsModded));
            this.RaisePropertyChanged(nameof(ModMeta));
        }
    }

    /// <summary>
    /// Whether a save is currently selected.
    /// </summary>
    public bool HasSelectedSave => SelectedSave != null;

    /// <summary>
    /// Path to the current save's screenshot, or null if none.
    /// </summary>
    public string? ScreenshotPath => SelectedSave?.ScreenshotPath;

    /// <summary>
    /// Parsed body data for the selected save.
    /// </summary>
    public SaveBodyData? BodyData => SelectedSave?.BodyData;

    /// <summary>
    /// Whether body data is available and valid.
    /// </summary>
    public bool HasBodyData => BodyData?.IsValid == true;

    /// <summary>
    /// Whether the selected save is modded.
    /// </summary>
    public bool IsModded => SelectedSave?.IsModded == true;

    /// <summary>
    /// Mod metadata for the selected save.
    /// </summary>
    public ModMetaData? ModMeta => SelectedSave?.ModMeta;

    // Editable properties for all header fields

    public string EditableSaveGameName
    {
        get => _editableSaveGameName;
        set { if (SetField(ref _editableSaveGameName, value)) MarkChanged(); }
    }

    public string EditablePlanetName
    {
        get => _editablePlanetName;
        set { if (SetField(ref _editablePlanetName, value)) MarkChanged(); }
    }

    public string EditableOperationName
    {
        get => _editableOperationName;
        set { if (SetField(ref _editableOperationName, value)) MarkChanged(); }
    }

    public string EditableDifficulty
    {
        get => _editableDifficulty;
        set { if (SetField(ref _editableDifficulty, value)) MarkChanged(); }
    }

    public string EditableStrategyConfigName
    {
        get => _editableStrategyConfigName;
        set { if (SetField(ref _editableStrategyConfigName, value)) MarkChanged(); }
    }

    public int EditableCompletedMissions
    {
        get => _editableCompletedMissions;
        set { if (SetField(ref _editableCompletedMissions, value)) MarkChanged(); }
    }

    public int EditableOperationLength
    {
        get => _editableOperationLength;
        set { if (SetField(ref _editableOperationLength, value)) MarkChanged(); }
    }

    public double EditablePlayTimeSeconds
    {
        get => _editablePlayTimeSeconds;
        set
        {
            if (SetField(ref _editablePlayTimeSeconds, value))
            {
                MarkChanged();
                this.RaisePropertyChanged(nameof(EditablePlayTimeFormatted));
            }
        }
    }

    /// <summary>
    /// Formatted play time for display/edit (e.g., "2h 30m").
    /// </summary>
    public string EditablePlayTimeFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(_editablePlayTimeSeconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }

    public DateTime EditableSaveTime
    {
        get => _editableSaveTime;
        set { if (SetField(ref _editableSaveTime, value)) MarkChanged(); }
    }

    public SaveStateType EditableSaveStateType
    {
        get => _editableSaveStateType;
        set { if (SetField(ref _editableSaveStateType, value)) MarkChanged(); }
    }

    // --- Editable body properties ---

    public bool EditableIronman
    {
        get => _editableIronman;
        set { if (SetField(ref _editableIronman, value)) MarkChanged(); }
    }

    public int EditableSeed
    {
        get => _editableSeed;
        set { if (SetField(ref _editableSeed, value)) MarkChanged(); }
    }

    public int EditableCredits
    {
        get => _editableCredits;
        set { if (SetField(ref _editableCredits, value)) MarkChanged(); }
    }

    public int EditableIntelligence
    {
        get => _editableIntelligence;
        set { if (SetField(ref _editableIntelligence, value)) MarkChanged(); }
    }

    public int EditableAuthority
    {
        get => _editableAuthority;
        set { if (SetField(ref _editableAuthority, value)) MarkChanged(); }
    }

    public int EditablePromotionPoints
    {
        get => _editablePromotionPoints;
        set { if (SetField(ref _editablePromotionPoints, value)) MarkChanged(); }
    }

    private bool SetField<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        this.RaiseAndSetIfChanged(ref field, value);
        return true;
    }

    private void MarkChanged()
    {
        if (SelectedSave != null)
            HasUnsavedChanges = true;
    }

    /// <summary>
    /// Status message to display to the user.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>
    /// Whether to show the warning when saves folder is not found.
    /// </summary>
    public bool ShowNoSavesWarning
    {
        get => _showNoSavesWarning;
        private set => this.RaiseAndSetIfChanged(ref _showNoSavesWarning, value);
    }

    /// <summary>
    /// Whether there are unsaved changes to the current save.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveChangesCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectedSaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DuplicateSelectedSaveCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSaveFolderCommand { get; }

    /// <summary>
    /// Loads save files from the saves folder.
    /// </summary>
    public void LoadSaveFiles()
    {
        try
        {
            SelectedSave = null;
            _allSaveFiles.Clear();
            SaveFiles.Clear();
            StatusMessage = string.Empty;

            // Get diagnostic info
            var (saveFolderPath, reason) = _saveFileService.GetSaveFolderPathWithReason();

            if (saveFolderPath == null || !System.IO.Directory.Exists(saveFolderPath))
            {
                ShowNoSavesWarning = true;
                StatusMessage = reason;
                Services.ModkitLog.Warn($"[SaveEditor] Cannot load saves: {reason}");
                return;
            }

            ShowNoSavesWarning = false;

            var saves = _saveFileService.DiscoverSaveFiles();
            _allSaveFiles.AddRange(saves);
            ApplyFilter();

            if (_allSaveFiles.Count == 0)
            {
                StatusMessage = $"No save files found in {saveFolderPath}";
            }
            else
            {
                StatusMessage = $"Found {_allSaveFiles.Count} save file(s)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading saves: {ex.Message}";
            Services.ModkitLog.Error($"[SaveEditor] LoadSaveFiles failed: {ex}");
        }
    }

    private void ApplyFilter()
    {
        SaveFiles.Clear();

        var query = SearchText?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(query)
            ? _allSaveFiles
            : _allSaveFiles.Where(s =>
                s.SaveGameName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.PlanetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.OperationName.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var save in filtered)
        {
            SaveFiles.Add(save);
        }
    }

    /// <summary>
    /// Saves changes to the selected save file.
    /// </summary>
    public void SaveChanges()
    {
        try
        {
            if (SelectedSave == null)
            {
                StatusMessage = "No save selected";
                return;
            }

            if (!SelectedSave.IsValid)
            {
                StatusMessage = "Cannot modify invalid save file";
                return;
            }

            // Check if this was an ironman save
            var wasIronman = SelectedSave.BodyData?.Ironman == true;

            // Copy all editable values back to the header
            SelectedSave.SaveGameName = EditableSaveGameName;
            SelectedSave.PlanetName = EditablePlanetName;
            SelectedSave.OperationName = EditableOperationName;
            SelectedSave.Difficulty = EditableDifficulty;
            SelectedSave.StrategyConfigName = EditableStrategyConfigName;
            SelectedSave.CompletedMissions = EditableCompletedMissions;
            SelectedSave.OperationLength = EditableOperationLength;
            SelectedSave.PlayTimeSeconds = EditablePlayTimeSeconds;
            SelectedSave.SaveTime = EditableSaveTime;
            SelectedSave.SaveStateType = EditableSaveStateType;

            // Copy editable body values back
            if (SelectedSave.BodyData != null && SelectedSave.BodyData.IsValid)
            {
                // Always disable ironman when editing - can't claim ironman on modified saves
                SelectedSave.BodyData.Ironman = false;
                SelectedSave.BodyData.Seed = EditableSeed;
                SelectedSave.BodyData.Credits = EditableCredits;
                SelectedSave.BodyData.Intelligence = EditableIntelligence;
                SelectedSave.BodyData.Authority = EditableAuthority;
                SelectedSave.BodyData.PromotionPoints = EditablePromotionPoints;
            }

            // Write header changes
            bool headerSuccess = _saveFileService.WriteHeader(SelectedSave);
            bool bodySuccess = true;

            // Write body changes if we have valid body data
            if (SelectedSave.BodyData != null && SelectedSave.BodyData.IsValid)
            {
                bodySuccess = _saveFileService.WriteBodyChanges(SelectedSave);
            }

            if (headerSuccess && bodySuccess)
            {
                var messages = new List<string> { "Save updated" };

                if (wasIronman)
                {
                    messages.Add("ironman disabled");
                    _editableIronman = false;
                    this.RaisePropertyChanged(nameof(EditableIronman));
                }
                if (SelectedSave.BodyData != null)
                {
                    messages.Add("resources saved");
                }
                messages.Add("(backup created)");

                StatusMessage = string.Join(", ", messages);
                this.RaisePropertyChanged(nameof(BodyData));
                HasUnsavedChanges = false;
                ApplyFilter();
            }
            else if (headerSuccess)
            {
                StatusMessage = "Header saved, but body changes failed";
                HasUnsavedChanges = false;
                ApplyFilter();
            }
            else
            {
                StatusMessage = "Failed to save changes";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving changes: {ex.Message}";
            Services.ModkitLog.Error($"[SaveEditor] SaveChanges failed: {ex}");
        }
    }

    /// <summary>
    /// Deletes the currently selected save file.
    /// </summary>
    public void DeleteSelectedSave()
    {
        try
        {
            if (SelectedSave == null)
            {
                StatusMessage = "No save selected";
                return;
            }

            var saveToDelete = SelectedSave;
            var filePath = saveToDelete.FilePath;
            var fileName = saveToDelete.FileName;

            // Clear selection first to avoid UI issues
            SelectedSave = null;

            if (_saveFileService.DeleteSaveFile(filePath))
            {
                _allSaveFiles.Remove(saveToDelete);
                SaveFiles.Remove(saveToDelete);
                StatusMessage = $"Deleted: {fileName}";
            }
            else
            {
                StatusMessage = $"Failed to delete: {fileName}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting save: {ex.Message}";
            Services.ModkitLog.Error($"[SaveEditor] DeleteSelectedSave failed: {ex}");
        }
    }

    /// <summary>
    /// Duplicates the currently selected save file.
    /// </summary>
    public void DuplicateSelectedSave()
    {
        if (SelectedSave == null)
        {
            StatusMessage = "No save selected";
            return;
        }

        // This is called from the View after getting the new name via dialog
        StatusMessage = "Use the Duplicate button to create a copy";
    }

    /// <summary>
    /// Duplicates the selected save with the given new name.
    /// </summary>
    public bool DuplicateSaveWithName(string newName)
    {
        if (SelectedSave == null)
        {
            StatusMessage = "No save selected";
            return false;
        }

        var newSave = _saveFileService.DuplicateSaveFile(SelectedSave, newName);
        if (newSave != null)
        {
            _allSaveFiles.Insert(0, newSave);
            SaveFiles.Insert(0, newSave);
            SelectedSave = newSave;
            StatusMessage = $"Created: {newSave.FileName}";
            return true;
        }

        StatusMessage = "Failed to duplicate save";
        return false;
    }

    /// <summary>
    /// Opens the saves folder in the file explorer.
    /// </summary>
    public void OpenSaveFolder()
    {
        _saveFileService.OpenSaveFolder();
    }
}
