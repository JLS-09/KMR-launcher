using Avalonia.Controls;
using KMRLauncherMvvm.ViewModels.InstallMods;

namespace KMRLauncherMvvm.Views.InstallMods;

public partial class InstallProgressWindow : Window
{
    private bool _closeWhenFinished;

    public InstallProgressWindow()
    {
        InitializeComponent();

        Closing += (_, e) =>
        {
            if (DataContext is not InstallProgressViewModel { IsRunning: true } vm) return;
            e.Cancel = true;
            if (vm.CancelCommand.CanExecute(null)) vm.CancelCommand.Execute(null);
            if (_closeWhenFinished) return;

            _closeWhenFinished = true;
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(InstallProgressViewModel.IsFinished) && vm.IsFinished) Close();
            };
        };
    }
}
