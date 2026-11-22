using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.InstallMods;
using KMRLauncherMvvm.ViewModels.Instances;

namespace KMRLauncherMvvm.ViewModels.NewInstance;

public partial class ZipOptionViewModel(KspZip zip) : ObservableObject
{
    public KspZip Zip { get; } = zip;
    public string Version => Zip.Version;
    public string PathText { get; } = InstanceTexts.ShortPath(zip.Path);
    public bool IsMissing { get; } = !File.Exists(zip.Path);
    public string SizeText => IsMissing ? "missing" : InstalledMods.FormatSize(new FileInfo(Zip.Path).Length);

    [ObservableProperty] private bool _isSelected;
}

public enum StepState
{
    Pending,
    Current,
    Done,
    Failed
}

public partial class CreateStepRowViewModel(string title, string detail = "") : ObservableObject
{
    public string Title { get; } = title;

    [ObservableProperty] private string _detail = detail;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _showProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(IsCurrent), nameof(IsDone), nameof(IsFailed))]
    private StepState _state;

    public bool IsPending => State == StepState.Pending;
    public bool IsCurrent => State == StepState.Current;
    public bool IsDone => State == StepState.Done;
    public bool IsFailed => State == StepState.Failed;
}

public enum FoundKind
{
    Ok,
    Warning,
    Error
}

public record FoundRow(string Title, string Hint, FoundKind Kind)
{
    public bool IsOk => Kind == FoundKind.Ok;
    public bool IsWarning => Kind == FoundKind.Warning;
    public bool IsError => Kind == FoundKind.Error;
}

public record SummaryRow(string Label, string Value);

public partial class NewInstanceDialogViewModel : ViewModelBase, IDialogSize
{
    private readonly ZipService _zips;
    private readonly InstanceCreator _creator;
    private readonly Action<Instance?> _finish;
    private readonly Func<IEnumerable<Mod>?> _knownMods;
    private readonly Action _saveSettings;
    private readonly AppSettings _settings;
    private string _parent;
    private bool _nameEdited;
    private bool _importNameEdited;
    private bool _settingName;
    private CancellationTokenSource? _cancel;
    private int _measureRun;
    private int _scanRun;
    private bool _closeAfterCancel;

    [ObservableProperty] private IReadOnlyList<ZipOptionViewModel> _zipOptions = [];
    [ObservableProperty] private ZipOptionViewModel? _selectedZip;
    [ObservableProperty] private string? _addZipError;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _folderText = "";
    [ObservableProperty] private string _folderName = "";
    [ObservableProperty] private bool _isPrimary;
    [ObservableProperty] private CompatibleVersionsViewModel? _versions;
    [ObservableProperty] private IReadOnlyList<string> _versionChips = [];
    [ObservableProperty] private bool _isEditingVersions;
    [ObservableProperty] private string _diskText = "–";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsZipTab), nameof(IsZipForm), nameof(IsFolderForm), nameof(DialogHeight),
        nameof(CreateText))]
    private bool _isFolderTab;

    [ObservableProperty] private string _folderInput = "";
    [ObservableProperty] private KspFolderScan? _scan;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasFoundRows))]
    private IReadOnlyList<FoundRow> _foundRows = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsInPlace))] private bool _isCopy = true;
    [ObservableProperty] private string _importName = "";
    [ObservableProperty] private string? _importNameError;
    [ObservableProperty] private string _importFolderPath = "";
    [ObservableProperty] private string _importFolderText = "";
    [ObservableProperty] private string _copyHint = "";
    [ObservableProperty] private string _inPlaceHint = "";
    [ObservableProperty] private string _copySize = "–";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsForm), nameof(IsZipForm), nameof(IsFolderForm), nameof(DialogHeight))]
    private bool _isCreating;

    [ObservableProperty] private string _creatingTitle = "";
    [ObservableProperty] private string _creatingSubtitle = "";
    [ObservableProperty] private string _writtenText = "–";
    [ObservableProperty] private string? _createError;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private IReadOnlyList<CreateStepRowViewModel> _steps = [];
    [ObservableProperty] private IReadOnlyList<SummaryRow> _summary = [];
    [ObservableProperty] private string _summaryNote = "";

    public string? StartInstanceName { get; }
    public bool IsForm => !IsCreating;
    public bool IsZipTab => !IsFolderTab;
    public bool IsZipForm => IsForm && !IsFolderTab;
    public bool IsFolderForm => IsForm && IsFolderTab;
    public bool IsInPlace => !IsCopy;
    public bool HasFoundRows => FoundRows.Count > 0;
    public bool HasNoZips => ZipOptions.Count == 0;
    public string KspText => SelectedZip?.Version ?? "–";
    public string CreateText => IsFolderTab ? "Add instance" : "Create instance";

    public double DialogWidth => 1000;
    public double DialogHeight => IsCreating || IsFolderTab ? 860 : 1100;

    public NewInstanceDialogViewModel(AppSettings settings, ZipService zips, InstanceCreator creator,
        Action<Instance?> finish, Func<IEnumerable<Mod>?>? knownMods = null, Action? saveSettings = null)
    {
        _settings = settings;
        _saveSettings = saveSettings ?? (() => { });
        _zips = zips;
        _creator = creator;
        _finish = finish;
        _knownMods = knownMods ?? (() => null);
        _parent = InstanceFolders.ParentFor(settings);
        _isPrimary = !settings.UsableInstances.Any();
        StartInstanceName = settings.UsableInstances.FirstOrDefault(i => i.IsPrimary)?.Name ??
                            settings.UsableInstances.FirstOrDefault()?.Name;

        LoadZips(null);
        UpdateSummary();
    }

    partial void OnIsFolderTabChanged(bool value)
    {
        UpdateSummary();
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void LoadZips(KspZip? select)
    {
        foreach (var option in ZipOptions) option.PropertyChanged -= OnZipOptionChanged;

        ZipOptions =
        [
            .. _settings.KspZips
                .OrderByDescending(z => z.Version, Comparer<string>.Create(CkanVersion.Compare))
                .Select(z => new ZipOptionViewModel(z))
        ];
        foreach (var option in ZipOptions) option.PropertyChanged += OnZipOptionChanged;
        OnPropertyChanged(nameof(HasNoZips));

        SelectedZip = ZipOptions.FirstOrDefault(o => o.Zip == select) ??
                      ZipOptions.FirstOrDefault(o => !o.IsMissing) ?? ZipOptions.FirstOrDefault();
    }

    private void OnZipOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ZipOptionViewModel.IsSelected) && sender is ZipOptionViewModel { IsSelected: true } option)
            SelectedZip = option;
    }

    partial void OnSelectedZipChanged(ZipOptionViewModel? value)
    {
        foreach (var option in ZipOptions) option.IsSelected = option == value;
        OnPropertyChanged(nameof(KspText));

        if (Versions is not null) Versions.Changed -= UpdateChips;
        Versions = value is null
            ? null
            : new CompatibleVersionsViewModel(value.Version, KspVersions.DefaultCompatibleVersions(value.Version));
        if (Versions is not null) Versions.Changed += UpdateChips;
        UpdateChips();

        if (!_nameEdited) SetName(() => Name = value is null ? "" : $"KSP {value.Version}");
        _ = MeasureAsync(value);
        UpdateSummary();
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void SetName(Action set)
    {
        _settingName = true;
        set();
        _settingName = false;
    }

    partial void OnNameChanged(string value)
    {
        if (!_settingName) _nameEdited = true;
        NameError = value.Trim().Length == 0 ? "The instance needs a name." : null;
        UpdateFolders();
        CreateCommand.NotifyCanExecuteChanged();
    }

    partial void OnImportNameChanged(string value)
    {
        if (!_settingName) _importNameEdited = true;
        ImportNameError = value.Trim().Length == 0 ? "The instance needs a name." : null;
        UpdateFolders();
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void UpdateFolders()
    {
        FolderPath = InstanceFolders.FreeFolder(_parent, Name, _settings.Instances);
        FolderText = InstanceTexts.ShortPath(FolderPath);
        FolderName = Path.GetFileName(FolderPath);
        ImportFolderPath = InstanceFolders.FreeFolder(_parent, ImportName, _settings.Instances);
        ImportFolderText = InstanceTexts.ShortPath(ImportFolderPath);
        UpdateSummary();
    }

    public void ChangeParent(string parent)
    {
        _parent = parent;
        UpdateFolders();
    }

    private void UpdateChips() =>
        VersionChips =
        [
            .. (Versions?.Entries() ?? [])
                .OrderBy(e => e, Comparer<string>.Create(CkanVersion.Compare))
                .Select(e => KspVersions.IsMinorPrefix(e) ? $"All {e}.x" : e)
        ];

    [RelayCommand]
    private void ToggleEditVersions() => IsEditingVersions = !IsEditingVersions;

    private async Task MeasureAsync(ZipOptionViewModel? option)
    {
        var run = ++_measureRun;
        DiskText = "–";
        UpdateSummary();
        if (option is null || option.IsMissing) return;

        try
        {
            var (_, bytes) = await Task.Run(() => ZipService.Measure(option.Zip));
            if (run != _measureRun) return;
            DiskText = $"about {InstalledMods.FormatSize(bytes)}";
            UpdateSummary();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    public void AddZip(string path)
    {
        var (zip, error) = _zips.AddToSettings(_settings, path);
        AddZipError = error;
        if (zip is null) return;

        _saveSettings();
        LoadZips(zip);
    }

    partial void OnFolderInputChanged(string value) => _ = ScanAsync(value);

    partial void OnIsCopyChanged(bool value)
    {
        UpdateSummary();
        CreateCommand.NotifyCanExecuteChanged();
    }

    public async Task ScanAsync(string input)
    {
        var run = ++_scanRun;
        Scan = null;
        FoundRows = [];
        CopySize = "–";
        UpdateHints();
        if (input.Trim().Length == 0)
        {
            IsScanning = false;
            UpdateSummary();
            CreateCommand.NotifyCanExecuteChanged();
            return;
        }

        IsScanning = true;
        CreateCommand.NotifyCanExecuteChanged();
        var known = _knownMods();
        var instances = _settings.Instances.ToList();
        var scan = await Task.Run(() => KspFolderScanner.Scan(input, instances, known));
        if (run != _scanRun) return;

        Scan = scan;
        IsScanning = false;
        FoundRows = DescribeScan(scan);
        if (!_importNameEdited) SetName(() => ImportName = DefaultImportName(scan));
        UpdateHints();
        UpdateSummary();
        CreateCommand.NotifyCanExecuteChanged();

        if (!scan.CanAdd) return;
        try
        {
            var bytes = await Task.Run(() => Helpers.GetDirectorySize(scan.Folder));
            if (run != _scanRun) return;
            CopySize = InstalledMods.FormatSize(bytes);
            UpdateHints();
            UpdateSummary();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool IsSteam(string folder) => folder.Contains("/steamapps/", StringComparison.OrdinalIgnoreCase);

    private static string DefaultImportName(KspFolderScan scan) =>
        scan.KmrData?.Name ??
        (scan.Version is null ? Path.GetFileName(scan.Folder)
            : IsSteam(scan.Folder) ? $"Steam {scan.Version}" : $"{Path.GetFileName(scan.Folder)} {scan.Version}");

    private void UpdateHints()
    {
        var steam = Scan is { } scan && IsSteam(scan.Folder);
        var original = steam ? "Your Steam install" : "The original folder";
        CopyHint = CopySize == "–" ? $"{original} stays untouched." : $"{original} stays untouched. Uses {CopySize}.";
        InPlaceHint = steam
            ? "KMR manages the Steam folder directly. Steam updates can change it under you."
            : "KMR manages this folder directly. Other tools that change it can get it out of step.";
    }

    public static IReadOnlyList<FoundRow> DescribeScan(KspFolderScan scan)
    {
        if (scan.Version is null)
            return
            [
                new FoundRow(scan.BuildId is null ? "Can't add this folder" : $"Unknown build {scan.BuildId}",
                    scan.Error ?? "", FoundKind.Error)
            ];

        List<FoundRow> rows = [new FoundRow($"KSP {scan.Version}", "Read from the build ID in buildID64.txt", FoundKind.Ok)];
        if (scan.Error is not null)
        {
            rows.Add(new FoundRow("Can't add this folder", scan.Error, FoundKind.Error));
            return rows;
        }

        if (scan.KmrData is { } kmr)
        {
            rows.Add(new FoundRow($"KMR data with {InstallChanges.Plural(kmr.Mods.Count, "mod")}",
                "Its name, mods and settings are read from .kmr, so it works like before. A CKAN registry isn't imported.",
                FoundKind.Ok));
        }
        else if (scan.HasRegistry)
        {
            var hint = $"{scan.Picked} picked, {(scan.Dependencies == 1 ? "1 dependency" : $"{scan.Dependencies} dependencies")}. " +
                       "They become this instance's mods, so updates and removal work like for any other instance.";
            if (scan.UnreadableMods > 0)
            {
                var names = scan.Unreadable.Count <= 5
                    ? JoinAnd(scan.Unreadable)
                    : $"{string.Join(", ", scan.Unreadable.Take(5))} and {scan.Unreadable.Count - 5} more";
                hint += $" {InstallChanges.Plural(scan.UnreadableMods, "mod")} couldn't be read and stay unmanaged: {names}.";
            }
            rows.Add(new FoundRow($"CKAN registry with {InstallChanges.Plural(scan.Mods.Count, "mod")}", hint,
                scan.UnreadableMods > 0 ? FoundKind.Warning : FoundKind.Ok));
        }
        else
        {
            rows.Add(new FoundRow("No CKAN registry",
                "Mods already in GameData stay as they are; KMR doesn't manage them.", FoundKind.Warning));
        }

        if (scan.KmrData is null)
        {
            rows.Add(scan.CkanCompatible is { } compatible
                ? new FoundRow("CKAN compatible versions",
                    compatible.Count == 0
                        ? "None besides the game version itself."
                        : $"{JoinAnd(compatible)} {(compatible.Count == 1 ? "is" : "are")} imported as compatible KSP versions.",
                    FoundKind.Ok)
                : new FoundRow("No CKAN compatible versions",
                    "The launcher's defaults are used. You can change them in the instance's settings.",
                    FoundKind.Warning));
        }

        if (scan.UnownedFolders.Count > 0)
        {
            var names = scan.UnownedFolders.Count <= 3
                ? JoinAnd(scan.UnownedFolders)
                : $"{string.Join(", ", scan.UnownedFolders.Take(2))} and {scan.UnownedFolders.Count - 2} others";
            rows.Add(new FoundRow(
                $"{InstallChanges.Plural(scan.UnownedFolders.Count, "folder")} in GameData no mod owns",
                $"{names}. KMR leaves them alone and never overwrites them.", FoundKind.Warning));
        }

        return rows;
    }

    private static string JoinAnd(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Join("", items) : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    private void UpdateSummary()
    {
        if (!IsFolderTab)
        {
            Summary =
            [
                new("KSP", KspText),
                new("Folder", FolderName),
                IsCreating ? new("Written", WrittenText) : new("Disk", DiskText),
                new("Runner", "Wine"),
                new("Mods", "0")
            ];
            SummaryNote = IsCreating ? "" : "The zip is unpacked into the folder. The zip itself stays where it is.";
            return;
        }

        var scan = Scan is { CanAdd: true } ok ? ok : null;
        var mods = scan is null ? "–"
            : scan.KmrData is { } kmr ? $"{kmr.Mods.Count} from KMR"
            : scan.HasRegistry ? $"{scan.Mods.Count} from CKAN"
            : "0";
        var compatible = scan is null ? "–"
            : scan.KmrData is not null ? "from KMR"
            : string.Join(", ", (scan.CkanCompatible ?? KspVersions.DefaultCompatibleVersions(scan.Version!))
                .OrderByDescending(v => v, Comparer<string>.Create(CkanVersion.Compare)));
        Summary =
        [
            new("KSP", scan?.Version ?? "–"),
            new("Mods", mods),
            new("Compatible", compatible.Length == 0 ? "none" : compatible),
            IsCreating && IsCopy ? new("Written", WrittenText)
                : new("Disk", scan is null ? "–" : IsCopy ? $"{CopySize} copy" : "in place"),
            new("Runner", "Wine")
        ];
        SummaryNote = IsCreating ? ""
            : IsCopy ? "CKAN can keep using the original folder. The copy is managed by KMR only."
            : "KMR keeps its data in a hidden .kmr folder inside it. Nothing else is moved.";
    }

    private bool CanCreate() => !IsCreating && (IsFolderTab
        ? Scan is { CanAdd: true } && !IsScanning && ImportNameError is null && ImportName.Trim().Length > 0
        : SelectedZip is { IsMissing: false } && NameError is null);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task Create()
    {
        Func<IProgress<CreateProgress>, CancellationToken, Task<Instance>> run;
        if (IsFolderTab)
        {
            if (Scan is not { } scan) return;
            var mode = IsCopy ? ImportMode.Copy : ImportMode.InPlace;
            Steps =
            [
                new CreateStepRowViewModel("Read the folder"),
                new CreateStepRowViewModel(IsCopy ? "Copy the files" : "Use the folder in place",
                    IsCopy ? "" : InstanceTexts.ShortPath(scan.Folder)),
                new CreateStepRowViewModel("Set up the Wine prefix", "on first launch"),
                new CreateStepRowViewModel("Save the instance")
            ];
            CreatingTitle = $"Adding {ImportName.Trim()}";
            CreatingSubtitle = $"KSP {scan.Version} from {InstanceTexts.ShortPath(scan.Folder)}";
            var request = new ImportRequest(ImportName.Trim(), scan, mode, ImportFolderPath, IsPrimary);
            run = (progress, cancel) => _creator.ImportAsync(_settings, request, progress, cancel);
        }
        else
        {
            if (SelectedZip is not { } zip || Versions is null) return;
            Steps =
            [
                new CreateStepRowViewModel("Check the zip"),
                new CreateStepRowViewModel("Unpack KSP"),
                new CreateStepRowViewModel("Set up the Wine prefix", "on first launch"),
                new CreateStepRowViewModel("Save the instance")
            ];
            CreatingTitle = $"Creating {Name.Trim()}";
            CreatingSubtitle = $"KSP {zip.Version} from {Path.GetFileName(zip.Zip.Path)}";
            var request = new NewInstanceRequest(Name.Trim(), zip.Zip, FolderPath, Versions.Entries(), IsPrimary);
            run = (progress, cancel) => _creator.CreateFromZipAsync(_settings, request, progress, cancel);
        }

        WrittenText = "–";
        CreateError = null;
        IsCreating = true;
        IsRunning = true;
        UpdateSummary();
        CreateCommand.NotifyCanExecuteChanged();

        _cancel = new CancellationTokenSource();
        try
        {
            var instance = await run(new Progress<CreateProgress>(ShowProgress), _cancel.Token);
            _finish(instance);
        }
        catch (OperationCanceledException)
        {
            IsCreating = false;
            if (_closeAfterCancel) _finish(null);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            foreach (var step in Steps.Where(s => s.IsCurrent)) step.State = StepState.Failed;
            CreateError = e.Message;
        }
        finally
        {
            IsRunning = false;
            _cancel.Dispose();
            _cancel = null;
            UpdateFolders();
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    private void ShowProgress(CreateProgress p)
    {
        var index = (int)p.Step;
        for (var i = 0; i < Steps.Count; i++)
        {
            var state = i < index ? StepState.Done : i == index ? StepState.Current : StepState.Pending;
            if (Steps[i].State != StepState.Failed) Steps[i].State = state;
        }

        if (p.Step == CreateStep.Files && p.Detail is not null) Steps[0].Detail = p.Detail;
        if (p.Unpack is { } unpack)
        {
            var row = Steps[1];
            row.ShowProgress = true;
            row.Progress = unpack.Percentage;
            row.Detail = $"{unpack.FilesDone:N0} / {unpack.FilesTotal:N0} files · {Math.Round(unpack.Percentage * 100)}%";
            WrittenText = $"{InstalledMods.FormatSize(unpack.BytesWritten)} / {InstalledMods.FormatSize(unpack.BytesTotal)}";
            UpdateSummary();
        }

        if (p.Step > CreateStep.Files) Steps[1].ShowProgress = false;
    }

    [RelayCommand]
    private void CancelCreate() => _cancel?.Cancel();

    [RelayCommand]
    private void BackToForm()
    {
        if (IsRunning) return;
        CreateError = null;
        IsCreating = false;
        UpdateSummary();
    }

    [RelayCommand]
    private void Close()
    {
        if (IsRunning)
        {
            _closeAfterCancel = true;
            _cancel?.Cancel();
            return;
        }

        _finish(null);
    }
}
