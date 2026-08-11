using System.Text.Json;

namespace DecodCLI.Core;

public class MemoryManager
{
    private readonly string _memoryPath;
    public List<string> Memories { get; private set; } = new();

    public MemoryManager(string workspaceRoot)
    {
        var decodDir = Path.Combine(workspaceRoot, ".decod");
        Directory.CreateDirectory(decodDir);
        _memoryPath = Path.Combine(decodDir, "memory.json");
        Load();
    }

    public void AddMemory(string memoryItem)
    {
        if (!Memories.Contains(memoryItem))
        {
            Memories.Add(memoryItem);
            Save();
        }
    }

    public void RemoveMemory(int index)
    {
        if (index >= 0 && index < Memories.Count)
        {
            Memories.RemoveAt(index);
            Save();
        }
    }

    private void Load()
    {
        if (File.Exists(_memoryPath))
        {
            try
            {
                var content = File.ReadAllText(_memoryPath);
                Memories = JsonSerializer.Deserialize<List<string>>(content) ?? new();
            }
            catch
            {
                Memories = new();
            }
        }
    }

    private void Save()
    {
        File.WriteAllText(_memoryPath, JsonSerializer.Serialize(Memories, new JsonSerializerOptions { WriteIndented = true }));
    }
}
