namespace Rebalancing;

public enum TradeAction
{
    None,
    Buy,
    Sell
}

/// <summary>
/// Raw (string) input for one security, as it would arrive in a request.
/// Strings let the service validate missing and non-numeric values.
/// </summary>
public sealed class HoldingInput
{
    public string? Ticker { get; set; }
    public string? TargetPct { get; set; }
    public string? CurrentPct { get; set; }
    public string? UnitPrice { get; set; }

    /// <summary>Optional. Ignored: the service always recalculates variance (current% - target%).</summary>
    public string? StatedVariancePct { get; set; }

    public HoldingInput Clone() => (HoldingInput)MemberwiseClone();
}

public sealed record Trade(
    string Ticker,
    TradeAction Action,
    int Shares,
    decimal VariancePct,
    decimal UnitPrice,
    decimal CurrentValue,
    decimal TargetValue,
    decimal DollarVariance)
{
    public decimal TradeValue => Shares * UnitPrice;

    public decimal PostTradeValue => Action switch
    {
        TradeAction.Buy => CurrentValue + TradeValue,
        TradeAction.Sell => CurrentValue - TradeValue,
        _ => CurrentValue
    };
}

public sealed record RebalanceResult(decimal TotalAssets, IReadOnlyList<Trade> Trades);

public sealed class RebalanceException : Exception
{
    public RebalanceException(string message) : base(message)
    {
    }
}
