using System.Globalization;
using Rebalancing.Specs.Support;
using TechTalk.SpecFlow;
using Xunit;

namespace Rebalancing.Specs.StepDefinitions;

[Binding]
public sealed class RebalancingSteps
{
    private const string Subject = "SUBJECT";

    private readonly ScenarioState _state;
    private readonly RebalanceService _service = new();

    public RebalancingSteps(ScenarioState state)
    {
        _state = state;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static decimal D(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string S(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private HoldingInput Holding(string ticker) =>
        _state.Holdings.Single(h => h.Ticker == ticker);

    private RebalanceResult Result =>
        _state.Result ?? throw new InvalidOperationException(
            $"No rebalance result. Error: {_state.Error?.Message ?? "none"}");

    private Trade Find(string ticker) => Result.Trades.Single(t => t.Ticker == ticker);

    private static List<HoldingInput> TwoSecurityAccount(string target, string current, string price)
    {
        var t = D(target);
        var c = D(current);
        return new List<HoldingInput>
        {
            new() { Ticker = Subject, TargetPct = target, CurrentPct = current, UnitPrice = price },
            new() { Ticker = "OTHER", TargetPct = S(100m - t), CurrentPct = S(100m - c), UnitPrice = "1" }
        };
    }

    private void SetTwoSecurityAccount(string target, string current, string price)
    {
        _state.Holdings.Clear();
        _state.Holdings.AddRange(TwoSecurityAccount(target, current, price));
    }

    // ------------------------------------------------------------------
    // Given
    // ------------------------------------------------------------------

    [Given(@"account ""(.*)"" has total assets of (.*)")]
    public void GivenAccountHasTotalAssets(string account, string total)
    {
        _state.TotalAssets = D(total);
    }

    [Given(@"the account holds the following securities")]
    public void GivenTheAccountHolds(Table table)
    {
        foreach (var row in table.Rows)
        {
            _state.Holdings.Add(new HoldingInput
            {
                Ticker = row["security"],
                TargetPct = row["target%"],
                CurrentPct = row["current%"],
                UnitPrice = row["unit price"]
            });
        }
    }

    [Given(@"a two-security account where the subject has target% ([0-9.]+) and current% ([0-9.]+)")]
    public void GivenTwoSecurityAccount(string target, string current) =>
        SetTwoSecurityAccount(target, current, "100");

    [Given(@"a two-security account where the subject has target% ([0-9.]+) and current% ([0-9.]+) and unit price ([0-9.]+)")]
    public void GivenTwoSecurityAccountWithPrice(string target, string current, string price) =>
        SetTwoSecurityAccount(target, current, price);

    [Given(@"a dollar variance of (\d+) and unit price (\d+)")]
    public void GivenDollarVarianceAndPrice(decimal variance, decimal price)
    {
        _state.DollarVariance = variance;
        _state.Price = price;
    }

    [Given(@"the (target%|current%|unit price) of (\w+) is changed to (.*)")]
    public void GivenFieldChanged(string field, string ticker, string value)
    {
        var holding = Holding(ticker);
        switch (field)
        {
            case "target%": holding.TargetPct = value; break;
            case "current%": holding.CurrentPct = value; break;
            case "unit price": holding.UnitPrice = value; break;
        }
    }

    [Given(@"the total assets are changed to (.*)")]
    public void GivenTotalAssetsChanged(string value)
    {
        _state.TotalAssets = D(value);
    }

    [Given(@"the input has (.*)")]
    public void GivenTheInputHas(string problem)
    {
        switch (problem)
        {
            case "a null unit price": Holding("IBM").UnitPrice = null; break;
            case "a missing target%": Holding("IBM").TargetPct = null; break;
            case "a non-numeric current%": Holding("IBM").CurrentPct = "abc"; break;
            case "an empty list of securities": _state.Holdings.Clear(); break;
            default: throw new ArgumentException($"Unknown input problem: '{problem}'");
        }
    }

    [Given(@"the security ""(.*)"" appears twice in the input")]
    public void GivenSecurityAppearsTwice(string ticker)
    {
        _state.Holdings.Add(Holding(ticker).Clone());
    }

    [Given(@"the stated target variance of (\w+) is (-?\d+) instead of (-?\d+)")]
    public void GivenStatedVariance(string ticker, string stated, string actual)
    {
        Holding(ticker).StatedVariancePct = stated;
    }

    [Given(@"the current% of every security equals its target%")]
    public void GivenCurrentEqualsTarget()
    {
        foreach (var holding in _state.Holdings)
            holding.CurrentPct = holding.TargetPct;
    }

    // ------------------------------------------------------------------
    // When
    // ------------------------------------------------------------------

    [When(@"the rebalance is run")]
    public void WhenTheRebalanceIsRun()
    {
        try
        {
            _state.Result = _service.Rebalance(_state.TotalAssets, _state.Holdings);
        }
        catch (RebalanceException ex)
        {
            _state.Error = ex;
        }
    }

    [When(@"the rebalance is run twice with the same input")]
    public void WhenTheRebalanceIsRunTwice()
    {
        _state.Result = _service.Rebalance(_state.TotalAssets, _state.Holdings);
        _state.SecondResult = _service.Rebalance(_state.TotalAssets, _state.Holdings);
    }

    [When(@"the rebalance is run for a BUY and for a SELL of that variance")]
    public void WhenRunForBuyAndSell()
    {
        var pct = _state.DollarVariance / _state.TotalAssets * 100m;
        var price = S(_state.Price);

        var buy = TwoSecurityAccount("30", S(30m - pct), price);
        var sell = TwoSecurityAccount("30", S(30m + pct), price);

        _state.Result = _service.Rebalance(_state.TotalAssets, buy);
        _state.SecondResult = _service.Rebalance(_state.TotalAssets, sell);
    }

    // ------------------------------------------------------------------
    // Then: results
    // ------------------------------------------------------------------

    [Then(@"the output is")]
    public void ThenTheOutputIs(Table table)
    {
        Assert.Equal(table.RowCount, Result.Trades.Count);

        foreach (var row in table.Rows)
        {
            var trade = Find(row["security"]);
            Assert.Equal(row["action"], trade.Action.ToString().ToUpperInvariant());
            Assert.Equal(int.Parse(row["shares"], CultureInfo.InvariantCulture), trade.Shares);
        }
    }

    [Then(@"the shares for the subject are (\d+)")]
    public void ThenSharesForSubject(int shares) =>
        Assert.Equal(shares, Find(Subject).Shares);

    [Then(@"the action for the subject is ""(.*)""")]
    public void ThenActionForSubject(string action) =>
        Assert.Equal(action, Find(Subject).Action.ToString().ToUpperInvariant());

    [Then(@"every share quantity is a whole number")]
    public void ThenEveryShareIsWhole()
    {
        foreach (var trade in Result.Trades)
            Assert.Equal(0m, (decimal)trade.Shares % 1m);
    }

    [Then(@"every share quantity is greater than or equal to 0")]
    public void ThenEveryShareNonNegative()
    {
        foreach (var trade in Result.Trades)
            Assert.True(trade.Shares >= 0, $"{trade.Ticker} has negative shares");
    }

    [Then(@"both results show (\d+) shares")]
    public void ThenBothResultsShow(int shares)
    {
        var buy = _state.Result!.Trades.Single(t => t.Ticker == Subject);
        var sell = _state.SecondResult!.Trades.Single(t => t.Ticker == Subject);

        Assert.Equal(TradeAction.Buy, buy.Action);
        Assert.Equal(TradeAction.Sell, sell.Action);
        Assert.Equal(shares, buy.Shares);
        Assert.Equal(shares, sell.Shares);
    }

    // ------------------------------------------------------------------
    // Then: money
    // ------------------------------------------------------------------

    [Then(@"for each security the trade value is less than or equal to the dollar variance")]
    public void ThenTradeValueWithinVariance()
    {
        foreach (var trade in Result.Trades)
            Assert.True(trade.TradeValue <= trade.DollarVariance,
                $"{trade.Ticker}: trade value {trade.TradeValue} exceeds variance {trade.DollarVariance}");
    }

    [Then(@"the (IBM|MSFT|ORCL|AAPL|HD) (buy|sell) value is (\d+)")]
    public void ThenSecurityTradeValue(string ticker, string side, decimal value)
    {
        var trade = Find(ticker);
        var expected = side == "buy" ? TradeAction.Buy : TradeAction.Sell;

        Assert.Equal(expected, trade.Action);
        Assert.Equal(value, trade.TradeValue);
    }

    [Then(@"the total buy value is less than or equal to the total sell value plus available cash")]
    public void ThenBuysFundedBySells()
    {
        const decimal availableCash = 0m;
        Assert.True(TotalValue(TradeAction.Buy) <= TotalValue(TradeAction.Sell) + availableCash);
    }

    [Then(@"the total (buy|sell) value is (\d+)")]
    public void ThenTotalValue(string side, decimal value)
    {
        var action = side == "buy" ? TradeAction.Buy : TradeAction.Sell;
        Assert.Equal(value, TotalValue(action));
    }

    [Then(@"the post-trade value of each security is within one unit price of its target value")]
    public void ThenPostTradeWithinOneShare()
    {
        foreach (var trade in Result.Trades)
        {
            var gap = Math.Abs(trade.PostTradeValue - trade.TargetValue);
            Assert.True(gap <= trade.UnitPrice,
                $"{trade.Ticker}: gap {gap} is larger than one share ({trade.UnitPrice})");
        }
    }

    [Then(@"the post-trade allocation of (IBM|MSFT|ORCL|AAPL|HD) is ([0-9.]+) percent")]
    public void ThenPostTradeAllocation(string ticker, decimal percent)
    {
        var allocation = Find(ticker).PostTradeValue / Result.TotalAssets * 100m;
        Assert.Equal(percent, allocation);
    }

    [Then(@"the post-trade securities value plus the cash residual equals (\d+)")]
    public void ThenTotalValueUnchanged(decimal total)
    {
        var securities = Result.Trades.Sum(t => t.PostTradeValue);
        var cashResidual = TotalValue(TradeAction.Sell) - TotalValue(TradeAction.Buy);

        Assert.True(cashResidual >= 0, "Buys must not exceed sell proceeds");
        Assert.Equal(total, securities + cashResidual);
    }

    // ------------------------------------------------------------------
    // Then: validation
    // ------------------------------------------------------------------

    [Then(@"the request is rejected with the error ""(.*)""")]
    public void ThenRejectedWithError(string message)
    {
        Assert.NotNull(_state.Error);
        Assert.Equal(message, _state.Error!.Message);
    }

    [Then(@"the request is rejected with a validation error")]
    public void ThenRejectedWithValidationError() =>
        Assert.NotNull(_state.Error);

    [Then(@"no trade output is produced")]
    public void ThenNoOutput() =>
        Assert.Null(_state.Result);

    [Then(@"the application recalculates the variance of (\w+) as (-?[0-9.]+)")]
    public void ThenVarianceRecalculated(string ticker, decimal variance) =>
        Assert.Equal(variance, Find(ticker).VariancePct);

    [Then(@"the (\w+) action is ""(.*)"" for (\d+) shares")]
    public void ThenActionAndShares(string ticker, string action, int shares)
    {
        var trade = Find(ticker);
        Assert.Equal(action, trade.Action.ToString().ToUpperInvariant());
        Assert.Equal(shares, trade.Shares);
    }

    // ------------------------------------------------------------------
    // Then: edge cases and quality
    // ------------------------------------------------------------------

    [Then(@"every security shows 0 shares")]
    public void ThenEverySecurityZero()
    {
        foreach (var trade in Result.Trades)
            Assert.Equal(0, trade.Shares);
    }

    [Then(@"no trades are generated")]
    public void ThenNoTrades() =>
        Assert.All(Result.Trades, t => Assert.Equal(TradeAction.None, t.Action));

    [Then(@"both runs return identical output")]
    public void ThenIdenticalOutput() =>
        Assert.Equal(_state.Result!.Trades.ToList(), _state.SecondResult!.Trades.ToList());

    [Then(@"the IBM share count is (\d+) and never (\d+) due to floating point error")]
    public void ThenIbmExact(int expected, int wrong)
    {
        var shares = Find("IBM").Shares;
        Assert.Equal(expected, shares);
        Assert.NotEqual(wrong, shares);
    }

    [Then(@"the output lists every security with ticker, action and quantity")]
    public void ThenOutputListsEverySecurity()
    {
        Assert.Equal(_state.Holdings.Count, Result.Trades.Count);
        Assert.All(Result.Trades, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Ticker));
            Assert.True(Enum.IsDefined(t.Action));
            Assert.True(t.Shares >= 0);
        });
    }

    // ------------------------------------------------------------------

    private decimal TotalValue(TradeAction action) =>
        Result.Trades.Where(t => t.Action == action).Sum(t => t.TradeValue);
}
