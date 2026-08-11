namespace DecodCLI.Core;

public class MetricsCollector
{
    public int TotalInputTokens { get; private set; }
    public int TotalOutputTokens { get; private set; }
    public int TotalToolCalls { get; private set; }
    public int SuccessfulToolCalls { get; private set; }

    public void RecordUsage(int inputTokens, int outputTokens)
    {
        TotalInputTokens += inputTokens;
        TotalOutputTokens += outputTokens;
    }

    public void RecordToolExecution(bool success)
    {
        TotalToolCalls++;
        if (success) SuccessfulToolCalls++;
    }

    public string FormatSummary()
    {
        return $"Tokens: {TotalInputTokens} in / {TotalOutputTokens} out | Tools: {SuccessfulToolCalls}/{TotalToolCalls} ok";
    }
}
