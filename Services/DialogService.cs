using System;

namespace KMRLauncherMvvm.Services;

public class DialogService
{
    public object? Current { get; private set; }

    public event Action<object?>? CurrentChanged;

    public bool IsOpen => Current is not null;

    public void Show(object dialog)
    {
        if (Current is not null) throw new InvalidOperationException("A dialog is already open.");

        Current = dialog;
        CurrentChanged?.Invoke(dialog);
    }

    public void Close(object dialog)
    {
        if (!ReferenceEquals(Current, dialog)) return;

        Current = null;
        CurrentChanged?.Invoke(null);
    }
}

public interface IDialogSize
{
    double DialogWidth { get; }
    double DialogHeight { get; }
}
