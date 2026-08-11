namespace DecodCLI.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    object ParametersSchema { get; }
    Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default);
}
