using DomainLayer;
using System.Text.Json;

namespace face_detector;

public interface ISourceRepository
{
    List<SourceDto> Load();
    void Save(IEnumerable<SourceDto> sources);
}

public sealed class JsonSourceRepository : ISourceRepository
{
    private readonly string filePath;

    public JsonSourceRepository(string filePath)
    {
        this.filePath = filePath;
    }

    public List<SourceDto> Load()
    {
        if (!File.Exists(filePath))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SourceDto>>(File.ReadAllText(filePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<SourceDto> sources)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(filePath, JsonSerializer.Serialize(sources, options));
    }
}
