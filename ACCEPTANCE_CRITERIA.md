# Acceptance Criteria: Portfolio Rebalancing (Account ABC)

## Assumptions
1. Total asset value is $100,000, fully invested, no cash position.
2. Only whole shares can be traded; the app rounds **down** (truncates toward zero).
3. Prices are fixed execution prices; no fees, commissions, taxes or slippage.
4. Variance = current% - target%. Negative = BUY, positive = SELL.
5. Shares = floor(|variance% x total assets| / unit price).
6. Target percentages sum to 100%, and so do current percentages.
7. Current holdings may be fractional; the app only needs the dollar value of each holding.

## Expected result for the given data

| Security | Current $ | Target $ | Difference | Price | Exact shares | Action (whole shares) |
|----------|-----------|----------|------------|-------|--------------|-----------------------|
| IBM      | 10,000    | 20,000   | -10,000    | 150   | 66.67        | BUY 66                |
| MSFT     | 20,000    | 20,000   | 0          | 90    | 0            | No trade              |
| ORCL     | 30,000    | 20,000   | +10,000    | 220   | 45.45        | SELL 45               |
| AAPL     | 20,000    | 20,000   | 0          | 450   | 0            | No trade              |
| HD       | 20,000    | 20,000   | 0          | 70    | 0            | No trade              |

Buy value (66 x 150) = $9,900 = sell value (45 x 220). The rebalance is cash-neutral.

## Acceptance Criteria

### Core calculation
1. **Given** the Account ABC data, **when** rebalancing runs, **then** the output is IBM BUY 66, ORCL SELL 45, and MSFT, AAPL and HD 0.
2. Shares for each security equal `floor(|variance% x total assets| / unit price)`. Example: IBM = 10% x $100,000 / $150 = 66.67, so 66.
3. A negative variance outputs BUY and a positive variance outputs SELL.
4. A zero variance outputs no trade (0 shares, action NONE).

### Rounding and money
5. Share quantities are always whole, non-negative integers. Fractional shares are never output.
6. The same rounding rule (round down) applies to buys and sells.
7. The value of a trade never exceeds the dollar variance for that security.
8. Total buy value <= total sell value + available cash. For this dataset both are $9,900.
9. After the trades, each security's value is within one share's price of its target (IBM 19.9%, ORCL 20.1%).
10. Total account value is unchanged after rebalancing, apart from the rounding residual.

### Input validation
11. If target % values do not sum to 100, the request is rejected with a clear error and no partial output.
12. If current % values do not sum to 100, the request is rejected with a clear error.
13. Negative percentages, zero or negative prices, and zero or negative total assets are rejected.
14. Missing, null or non-numeric fields and an empty security list are rejected.
15. A duplicate ticker is rejected or consolidated, and the behavior is documented.
16. If the stated target variance does not equal current% - target%, the app either recalculates it or rejects the input, and the behavior is documented.

### Edge cases
17. If a unit price is higher than the dollar variance (e.g. AAPL at $450 with a $100 variance), the output is 0 shares.
18. If the account is already balanced, every security shows 0 and no trades are generated.
19. If a security has a 0% target, the full position is sold.

### Quality
20. Calculations use exact decimal arithmetic, so floating-point error cannot turn 66 into 65. The output is deterministic and lists every security with ticker, action and quantity in a consistent format.
