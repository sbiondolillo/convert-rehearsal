Feature: Conversion

  Background:
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"

  Scenario: The program asks the service for the rate of the pair
    Given the service answers with the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sends the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario Outline: The program prints the amount in the target currency
    Given the service answers with the rate <rate>
    When a person runs the program with "<arguments>"
    Then standard output is "<output>"
    And the exit code is 0

    Examples:
      | arguments    | rate     | output     |
      | 10 USD EUR   | 0.8888   | 8.89 EUR   |
      | 2.50 USD JPY | 157.8014 | 394.50 JPY |

  Scenario: The program takes the codes in lower case
    Given the service answers with the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: The program rejects an amount that is not a number
    When a person runs the program with "abc USD EUR"
    Then the program sends no request
    And standard error holds text
    And the exit code is not 0
