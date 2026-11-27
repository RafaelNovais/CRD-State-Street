Feature: Portfolio rebalancing for Account ABC
  As a portfolio manager
  I want the application to calculate the shares to buy or sell for each security
  So that the account returns to its target allocation

  # Assumptions
  # - Total asset value is 100000, fully invested, no cash position
  # - Only whole shares can be traded; the app rounds DOWN
  # - Prices are fixed execution prices; no fees, commissions, taxes or slippage
  # - Variance = current% - target%; negative = BUY, positive = SELL
  # - Shares = floor(|variance% x total assets| / unit price)
  # - A trade of 0 shares is reported as NONE
  # - Duplicate tickers are rejected; a stated variance is ignored and recalculated
  # - In the "two-security account" steps, a second security "OTHER" (price 1)
  #   holds the remaining allocation so both percentages still sum to 100

  Background:
    Given account "ABC" has total assets of 100000
    And the account holds the following securities
      | security | target% | current% | unit price |
      | IBM      | 20      | 10       | 150        |
      | MSFT     | 20      | 20       | 90         |
      | ORCL     | 20      | 30       | 220        |
      | AAPL     | 20      | 20       | 450        |
      | HD       | 20      | 20       | 70         |

  # ---------------------------------------------------------------
  # Core calculation
  # ---------------------------------------------------------------

  @AC1 @smoke
  Scenario: AC1 - Rebalance the account to target
    When the rebalance is run
    Then the output is
      | security | action | shares |
      | IBM      | BUY    | 66     |
      | MSFT     | NONE   | 0      |
      | ORCL     | SELL   | 45     |
      | AAPL     | NONE   | 0      |
      | HD       | NONE   | 0      |

  @AC2
  Scenario Outline: AC2 - Shares equal floor(|variance% x total assets| / price)
    Given a two-security account where the subject has target% <target> and current% <current> and unit price <price>
    When the rebalance is run
    Then the shares for the subject are <shares>

    Examples:
      | target | current | price | shares |
      | 20     | 10      | 150   | 66     |
      | 20     | 30      | 220   | 45     |
      | 20     | 10      | 200   | 50     |
      | 20     | 10      | 160   | 62     |
      | 25     | 15      | 100   | 100    |

  @AC3
  Scenario Outline: AC3 - Action follows the sign of the variance
    Given a two-security account where the subject has target% <target> and current% <current>
    When the rebalance is run
    Then the action for the subject is "<action>"

    Examples:
      | target | current | action |
      | 20     | 10      | BUY    |
      | 20     | 30      | SELL   |

  @AC4
  Scenario: AC4 - Zero variance produces no trade
    Given a two-security account where the subject has target% 20 and current% 20
    When the rebalance is run
    Then the action for the subject is "NONE"
    And the shares for the subject are 0

  # ---------------------------------------------------------------
  # Rounding and money
  # ---------------------------------------------------------------

  @AC5
  Scenario: AC5 - Shares are whole non-negative integers
    When the rebalance is run
    Then every share quantity is a whole number
    And every share quantity is greater than or equal to 0

  @AC6
  Scenario Outline: AC6 - The same rounding rule applies to buys and sells
    Given a dollar variance of <variance> and unit price <price>
    When the rebalance is run for a BUY and for a SELL of that variance
    Then both results show <shares> shares

    Examples:
      | variance | price | shares |
      | 10000    | 150   | 66     |
      | 10000    | 220   | 45     |
      | 10000    | 160   | 62     |

  @AC7
  Scenario: AC7 - A trade never exceeds the dollar variance
    When the rebalance is run
    Then for each security the trade value is less than or equal to the dollar variance
    And the IBM buy value is 9900
    And the ORCL sell value is 9900

  @AC8
  Scenario: AC8 - Buys are funded by sells
    When the rebalance is run
    Then the total buy value is less than or equal to the total sell value plus available cash
    And the total buy value is 9900
    And the total sell value is 9900

  @AC9
  Scenario: AC9 - Post-trade holdings are within one share of target
    When the rebalance is run
    Then the post-trade value of each security is within one unit price of its target value
    And the post-trade allocation of IBM is 19.9 percent
    And the post-trade allocation of ORCL is 20.1 percent

  @AC10
  Scenario: AC10 - Total account value is unchanged
    When the rebalance is run
    Then the post-trade securities value plus the cash residual equals 100000

  # ---------------------------------------------------------------
  # Input validation
  # ---------------------------------------------------------------

  @AC11 @negative
  Scenario: AC11 - Target percentages must sum to 100
    Given the target% of IBM is changed to 25
    When the rebalance is run
    Then the request is rejected with the error "Target % must sum to 100"
    And no trade output is produced

  @AC12 @negative
  Scenario: AC12 - Current percentages must sum to 100
    Given the current% of IBM is changed to 15
    When the rebalance is run
    Then the request is rejected with the error "Current % must sum to 100"
    And no trade output is produced

  @AC13 @negative
  Scenario Outline: AC13 - Invalid numeric values are rejected
    Given the <field> of IBM is changed to <value>
    When the rebalance is run
    Then the request is rejected with a validation error
    And no trade output is produced

    Examples:
      | field      | value |
      | target%    | -5    |
      | current%   | -5    |
      | unit price | 0     |
      | unit price | -150  |

  @AC13 @negative
  Scenario Outline: AC13 - Invalid total assets are rejected
    Given the total assets are changed to <value>
    When the rebalance is run
    Then the request is rejected with a validation error
    And no trade output is produced

    Examples:
      | value |
      | 0     |
      | -1    |

  @AC14 @negative
  Scenario Outline: AC14 - Missing or malformed input is rejected
    Given the input has <problem>
    When the rebalance is run
    Then the request is rejected with a validation error
    And no trade output is produced

    Examples:
      | problem                     |
      | a null unit price           |
      | a missing target%           |
      | a non-numeric current%      |
      | an empty list of securities |

  @AC15 @negative
  Scenario: AC15 - Duplicate tickers are rejected
    Given the security "IBM" appears twice in the input
    When the rebalance is run
    Then the request is rejected with the error "Duplicate ticker: IBM"

  @AC16
  Scenario: AC16 - Stated variance is ignored and recalculated
    Given the stated target variance of IBM is 5 instead of -10
    When the rebalance is run
    Then the application recalculates the variance of IBM as -10
    And the IBM action is "BUY" for 66 shares

  # ---------------------------------------------------------------
  # Edge cases
  # ---------------------------------------------------------------

  @AC17 @edge
  Scenario: AC17 - Price higher than the dollar variance gives 0 shares
    Given a two-security account where the subject has target% 20 and current% 20.1 and unit price 450
    When the rebalance is run
    Then the shares for the subject are 0

  @AC18 @edge
  Scenario: AC18 - An already balanced account generates no trades
    Given the current% of every security equals its target%
    When the rebalance is run
    Then every security shows 0 shares
    And no trades are generated

  @AC19 @edge
  Scenario: AC19 - A 0% target sells the full position
    Given a two-security account where the subject has target% 0 and current% 5 and unit price 50
    When the rebalance is run
    Then the action for the subject is "SELL"
    And the shares for the subject are 100

  # ---------------------------------------------------------------
  # Quality
  # ---------------------------------------------------------------

  @AC20 @quality
  Scenario: AC20 - Exact arithmetic and consistent output
    When the rebalance is run twice with the same input
    Then both runs return identical output
    And the IBM share count is 66 and never 65 due to floating point error
    And the output lists every security with ticker, action and quantity
