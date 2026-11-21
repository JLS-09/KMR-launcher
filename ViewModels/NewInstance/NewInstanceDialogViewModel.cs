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

public partial class NewInstanceDialogViewModel : ViewModelBase, IDialogSize
{
    private readonly ZipService _zips;
    private readonly InstanceCreator _creator;
    private readonly Action<Instance?> _finish;
    private readonly AppSettings _settings;
    private string _parent;
    private bool _nameEdited;
    private bool _settingName;
    private CancellationTokenSource? _cancel;
    private int _measureRun;
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
    [NotifyPropertyChangedFor(nameof(IsForm), nameof(DialogHeight))]
    private bool _isCreating;

    [ObservableProperty] private string _creatingTitle = "";
    [ObservableProperty] private string _creatingSubtitle = "";
    [ObservableProperty] private string _writtenText = "–";
    [ObservableProperty] private string? _createError;
    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private IReadOnlyList<CreateStepRowViewModel> _steps = [];

    public string? StartInstanceName { get; }
    public bool IsForm => !IsCreating;
    public bool HasNoZips => ZipOptions.Count == 0;
    public string KspText => SelectedZip?.Version ?? "–";
    public string EditVersionsText => IsEditingVersions ? "Done" : "Edit";

    public double DialogWidth => 1000;
    public double DialogHeight => IsCreating ? 860 : 1100;

    public NewInstanceDialogViewModel(AppSettings settings, ZipService zips, InstanceCreator creator,
        Action<Instance?> finish)
    {
        _settings = settings;
        _zips = zips;
        _creator = creator;
        _finish = finish;
        _parent = InstanceFolders.ParentFor(settings);
        _isPrimary = !settings.UsableInstances.Any();
        StartInstanceName = settings.UsableInstances.FirstOrDefault(i => i.IsPrimary)?.Name ??
                            settings.UsableInstances.FirstOrDefault()?.Name;

        LoadZips(null);
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

        if (!_nameEdited) SetName(value is null ? "" : $"KSP {value.Version}");
        _ = MeasureAsync(value);
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void SetName(string name)
    {
        _settingName = true;
        Name = name;
        _settingName = false;
    }

    partial void OnNameChanged(string value)
    {
        if (!_settingName) _nameEdited = true;
        NameError = value.Trim().Length == 0 ? "The instance needs a name." : null;
        UpdateFolder();
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void UpdateFolder()
    {
        FolderPath = InstanceFolders.FreeFolder(_parent, Name, _settings.Instances);
        FolderText = InstanceTexts.ShortPath(FolderPath);
        FolderName = Path.GetFileName(FolderPath);
    }

    public void ChangeParent(string parent)
    {
        _parent = parent;
        UpdateFolder();
    }

    private void UpdateChips() =>
        VersionChips =
        [
            .. (Versions?.Entries() ?? [])
                .OrderBy(e => e, Comparer<string>.Create(CkanVersion.Compare))
                .Select(e => KspVersions.IsMinorPrefix(e) ? $"All {e}.x" : e)
        ];

    partial void OnIsEditingVersionsChanged(bool value) => OnPropertyChanged(nameof(EditVersionsText));

    [RelayCommand]
    private void ToggleEditVersions() => IsEditingVersions = !IsEditingVersions;

    private async Task MeasureAsync(ZipOptionViewModel? option)
    {
        var run = ++_measureRun;
        DiskText = "–";
        if (option is null || option.IsMissing) return;

        try
        {
            var (_, bytes) = await Task.Run(() => ZipService.Measure(option.Zip));
            if (run == _measureRun) DiskText = $"about {InstalledMods.FormatSize(bytes)}";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    public void AddZip(string path)
    {
        var (zip, error) = _zips.AddToSettings(_settings, path);
        AddZipError = error;
        if (zip is not null) LoadZips(zip);
    }

    private bool CanCreate() => SelectedZip is { IsMissing: false } && NameError is null && !IsCreating;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task Create()
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
        WrittenText = "–";
        CreateError = null;
        IsCreating = true;
        IsRunning = true;
        CreateCommand.NotifyCanExecuteChanged();

        _cancel = new CancellationTokenSource();
        var request = new NewInstanceRequest(Name.Trim(), zip.Zip, FolderPath, Versions.Entries(), IsPrimary);
        var progress = new Progress<CreateProgress>(ShowProgress);
        try
        {
            var instance = await _creator.CreateFromZipAsync(_settings, request, progress, _cancel.Token);
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
            UpdateFolder();
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

        if (p.Step == CreateStep.Unpack && p.Detail is not null) Steps[0].Detail = p.Detail;
        if (p.Unpack is { } unpack)
        {
            var unpackRow = Steps[1];
            unpackRow.ShowProgress = true;
            unpackRow.Progress = unpack.Percentage;
            unpackRow.Detail = $"{unpack.FilesDone:N0} / {unpack.FilesTotal:N0} files · {Math.Round(unpack.Percentage * 100)}%";
            WrittenText = $"{InstalledMods.FormatSize(unpack.BytesWritten)} / {InstalledMods.FormatSize(unpack.BytesTotal)}";
        }

        if (p.Step > CreateStep.Unpack) Steps[1].ShowProgress = false;
    }

    [RelayCommand]
    private void CancelCreate() => _cancel?.Cancel();

    [RelayCommand]
    private void BackToForm()
    {
        if (IsRunning) return;
        CreateError = null;
        IsCreating = false;
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
