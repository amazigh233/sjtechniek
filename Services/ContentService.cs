using System.Text.Json;

namespace SjTechniek.Services;

public class ContentService
{
    private Dictionary<string, string> _content = new();
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ContentService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "content.json");
        Load();
    }

    public string Get(string key, string defaultValue = "")
    {
        return _content.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            _content[key] = value;
            await SaveAsync();
        }
        finally { _lock.Release(); }
    }

    public Dictionary<string, string> GetAll() => new(_content);

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            _content = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch { _content = new(); }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_content, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
