using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Core;

public class ConfigManager
{
    private readonly string _configFilePath;
    private JsonObject _configData;

    public ConfigManager()
    {
        var decodDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".decod");
        Directory.CreateDirectory(decodDir);
        _configFilePath = Path.Combine(decodDir, "config.json");

        if (File.Exists(_configFilePath))
        {
            try
            {
                var content = File.ReadAllText(_configFilePath);
                _configData = JsonNode.Parse(content)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                _configData = new JsonObject();
            }
        }
        else
        {
            _configData = new JsonObject();
            Save();
        }
    }

    public string? GetApiKey(string provider)
    {
        var key = provider.ToLowerInvariant();
        var envVal = Environment.GetEnvironmentVariable($"{key.ToUpperInvariant()}_API_KEY");
        if (!string.IsNullOrWhiteSpace(envVal)) return envVal;

        return _configData["api_keys"]?[key]?.ToString();
    }

    public void SetApiKey(string provider, string apiKey)
    {
        var keysObj = _configData["api_keys"]?.AsObject();
        if (keysObj == null)
        {
            keysObj = new JsonObject();
            _configData["api_keys"] = keysObj;
        }
        keysObj[provider.ToLowerInvariant()] = apiKey;
        Save();
    }

    public string? GetSetting(string key)
    {
        return _configData["settings"]?[key]?.ToString();
    }

    public void SetSetting(string key, string value)
    {
        var settingsObj = _configData["settings"]?.AsObject();
        if (settingsObj == null)
        {
            settingsObj = new JsonObject();
            _configData["settings"] = settingsObj;
        }
        settingsObj[key] = value;
        Save();
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_configFilePath, _configData.ToJsonString(options));
    }
}
