using System.Globalization;

namespace Rebalancing;

/// <summary>
/// Calculates the whole-share trades needed to bring an account to its target allocation.
///
/// Rules (documented assumptions):
///  - variance% = current% - target%  (negative = BUY, positive = SELL)
///  - shares    = floor(|variance%| / 100 * totalAssets / unitPrice)   (round DOWN, whole shares)
///  - a trade of 0 shares is reported as action NONE
///  - the stated variance, if supplied, is ignored and recalculated
///  - duplicate tickers are rejected
///  - no cash position, no fees, fixed prices
/// </summary>
public sealed class RebalanceService
{
    private sealed record Holding(string Ticker, decimal TargetPct, decimal CurrentPct, decimal UnitPrice);

    public RebalanceResult Rebalance(decimal totalAssets, IReadOnlyList<HoldingInput>? holdings)
    {
        if (totalAssets <= 0)
            throw new RebalanceException("Total assets must be greater than zero");

        if (holdings is null || holdings.Count == 0)
            throw new RebalanceException("At least one security is required");

        var parsed = holdings.Select(Parse).ToList();

        var duplicate = parsed
            .GroupBy(h => h.Ticker, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new RebalanceException($"Duplicate ticker: {duplicate.Key}");

        if (parsed.Sum(h => h.TargetPct) != 100m)
            throw new RebalanceException("Target % must sum to 100");

        if (parsed.Sum(h => h.CurrentPct) != 100m)
            throw new RebalanceException("Current % must sum to 100");

        var trades = parsed.Select(h => ToTrade(h, totalAssets)).ToList();
        return new RebalanceResult(totalAssets, trades);
    }

    private static Trade ToTrade(Holding h, decimal totalAssets)
    {
        var variancePct = h.CurrentPct - h.TargetPct;
        var dollarVariance = Math.Abs(variancePct) / 100m * totalAssets;
        var shares = (int)Math.Floor(dollarVariance / h.UnitPrice);

        var action = shares == 0
            ? TradeAction.None
            : variancePct < 0 ? TradeAction.Buy : TradeAction.Sell;

        return new Trade(
            h.Ticker,
            action,
            shares,
            variancePct,
            h.UnitPrice,
            CurrentValue: h.CurrentPct / 100m * totalAssets,
            TargetValue: h.TargetPct / 100m * totalAssets,
            DollarVariance: dollarVariance);
    }

    private static Holding Parse(HoldingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Ticker))
            throw new RebalanceException("Ticker is required");

        var ticker = input.Ticker.Trim();
        var target = ParseRequired(input.TargetPct, "Target %", ticker);
        var current = ParseRequired(input.CurrentPct, "Current %", ticker);
        var price = ParseRequired(input.UnitPrice, "Unit price", ticker);

        if (target < 0 || target > 100)
            throw new RebalanceException($"Target % must be between 0 and 100 for {ticker}");
        if (current < 0 || current > 100)
            throw new RebalanceException($"Current % must be between 0 and 100 for {ticker}");
        if (price <= 0)
            throw new RebalanceException($"Unit price must be greater than zero for {ticker}");

        return new Holding(ticker, target, current, price);
    }

    private static decimal ParseRequired(string? raw, string field, string ticker)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new RebalanceException($"{field} is required for {ticker}");

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            throw new RebalanceException($"{field} must be numeric for {ticker}");

        return value;
    }
}
