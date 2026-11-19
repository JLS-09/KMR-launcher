namespace KMRLauncherMvvm.ViewModels;

// A page that the search box in the top bar can search. For now the box only searches the current page;
// searching across mods, instances and actions comes later (Phase 9, "Later").
public interface ISearchablePage
{
    void ApplySearch(string text);
}
