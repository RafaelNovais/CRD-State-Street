namespace Rebalancing.Specs.Support;

/// <summary>Per-scenario state shared between step definitions (SpecFlow context injection).</summary>
public sealed class ScenarioState
{
    public decimal TotalAssets { get; set; }
    public List<HoldingInput> Holdings { get; } = new();

    public RebalanceResult? Result { get; set; }
    public RebalanceResult? SecondResult { get; set; }
    public RebalanceException? Error { get; set; }

    public decimal DollarVariance { get; set; }
    public decimal Price { get; set; }
}
