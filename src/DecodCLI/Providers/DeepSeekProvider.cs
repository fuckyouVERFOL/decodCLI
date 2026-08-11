namespace DecodCLI.Providers;

public class DeepSeekProvider : OpenAIProvider
{
    public new string Name => "DeepSeek";
    public new string DefaultModel => "deepseek-chat";
    public new IReadOnlyList<string> SupportedModels => new[] { "deepseek-chat", "deepseek-reasoner" };
    public new bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));

    public DeepSeekProvider(string? apiKey = null) 
        : base(apiKey ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"), "https://api.deepseek.com")
    {
    }
}
