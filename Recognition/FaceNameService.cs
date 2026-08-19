using System.Text.Json;

namespace Recognition;

public interface IFaceNameService
{
    string Get(string id);
    void Set(string id, string fullname);
}

public sealed class FileFaceNameService : IFaceNameService
{
    private readonly string filePath;
    private readonly Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
    private readonly object sync = new();

    public FileFaceNameService(string baseDirectory)
    {
        filePath = Path.Combine(baseDirectory, "face-names.txt");
        Load();
    }

    public string Get(string id)
    {
        lock (sync)
            return names.TryGetValue(id, out var fullname) ? fullname : string.Empty;
    }

    public void Set(string id, string fullname)
    {
        lock (sync)
        {
            if (string.IsNullOrWhiteSpace(fullname))
                names.Remove(id);
            else
                names[id] = fullname.Trim();

            File.WriteAllText(filePath, JsonSerializer.Serialize(names, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }

    private void Load()
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath));
            if (loaded is null)
                return;

            foreach (var item in loaded)
                names[item.Key] = item.Value;
        }
        catch
        {
        }
    }
}
