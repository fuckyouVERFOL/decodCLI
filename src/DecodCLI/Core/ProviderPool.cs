using DecodCLI.Providers;

namespace DecodCLI.Core;

public class ProviderPool
{
    private readonly List<IModelProvider> _providers = new();
    public IModelProvider ActiveProvider { get; private set; }
    public string ActiveModel { get; private set; }

    public IReadOnlyList<IModelProvider> Providers => _providers.AsReadOnly();

    public ProviderPool(ConfigManager config)
    {
        _providers.Add(new OpenAIProvider(config.GetApiKey("openai")));
        _providers.Add(new AnthropicProvider(config.GetApiKey("anthropic")));
        _providers.Add(new GeminiProvider(config.GetApiKey("gemini")));
        _providers.Add(new DeepSeekProvider(config.GetApiKey("deepseek")));
        _providers.Add(new OllamaProvider(config.GetSetting("ollama_url") ?? "http://localhost:11434"));

        var preferredProviderName = config.GetSetting("default_provider") ?? "OpenAI";
        ActiveProvider = _providers.FirstOrDefault(p => p.Name.Equals(preferredProviderName, StringComparison.OrdinalIgnoreCase) && p.IsConfigured)
                         ?? _providers.FirstOrDefault(p => p.IsConfigured)
                         ?? _providers.First(p => p.Name == "Ollama");

        ActiveModel = config.GetSetting("default_model") ?? ActiveProvider.DefaultModel;
    }

    public bool SetActiveProvider(string providerName, string? modelName = null)
    {
        var provider = _providers.FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider != null)
        {
            ActiveProvider = provider;
            ActiveModel = modelName ?? provider.DefaultModel;
            return true;
        }
        return false;
    }

    public void SetActiveModel(string modelName)
    {
        ActiveModel = modelName;
    }

    public async Task<ChatResponse> ExecuteWithFallbackAsync(List<ChatMessage> messages, List<ToolDefinition> tools, CancellationToken ct = default)
    {
        var candidateProviders = _providers.Where(p => p.IsConfigured).ToList();
        if (candidateProviders.Count == 0)
        {
            candidateProviders.Add(_providers.First(p => p.Name == "Ollama"));
        }

        candidateProviders.Remove(ActiveProvider);
        candidateProviders.Insert(0, ActiveProvider);

        List<Exception> errors = new();

        foreach (var provider in candidateProviders)
        {
            try
            {
                var modelToUse = provider == ActiveProvider ? ActiveModel : provider.DefaultModel;
                var response = await provider.GenerateAsync(messages, tools, modelToUse, ct);
                ActiveProvider = provider;
                return response;
            }
            catch (Exception ex)
            {
                errors.Add(new Exception($"Provider [{provider.Name}] failed: {ex.Message}", ex));
            }
        }

        throw new AggregateException("All AI Providers in the pool failed to respond.", errors);
    }
}
