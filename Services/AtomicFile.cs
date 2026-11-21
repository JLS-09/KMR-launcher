using System.IO;
using System.Text;

namespace KMRLauncherMvvm.Services;

public static class AtomicFile
{
    public static void WriteAllText(string path, string text)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes);
            stream.Flush(true);
        }

        File.Move(temp, path, true);
    }
}
