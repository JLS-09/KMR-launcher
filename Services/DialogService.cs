using System;
using System.Collections.Generic;
using System.Linq;

namespace KMRLauncherMvvm.Services;

public class DialogService
{
    private readonly List<object> _open = [];

    public IReadOnlyList<object> Open => _open;
    public object? Current => _open.LastOrDefault();
    public bool IsOpen => _open.Count > 0;

    public event Action? Changed;

    public void Show(object dialog)
    {
        if (IsOpen) throw new InvalidOperationException("A dialog is already open.");

        ShowOver(dialog);
    }

    public void ShowOver(object dialog)
    {
        if (_open.Contains(dialog)) return;

        _open.Add(dialog);
        Changed?.Invoke();
    }

    public void Close(object dialog)
    {
        if (!_open.Remove(dialog)) return;

        (dialog as IDisposable)?.Dispose();
        Changed?.Invoke();
    }
}

public interface IDialogSize
{
    double DialogWidth { get; }
    double DialogHeight { get; }
}
