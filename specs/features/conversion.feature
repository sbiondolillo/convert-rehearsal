Feature: Currency conversion

  A person converts an amount of money to another currency at the rate of the day.

  Scenario: The program asks the service for the rate of the pair
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sends the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario Outline: The program prints the amount in the target currency
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate <rate>
    When a person runs the program with "<arguments>"
    Then standard output is "<output>"
    And the exit code is 0

    Examples:
      | arguments    | rate     | output     |
      | 10 USD EUR   | 0.8888   | 8.89 EUR   |
      | 2.50 USD JPY | 157.8014 | 394.50 JPY |

  Scenario: The program takes codes in lower case
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: An amount that is not a number
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    When a person runs the program with "abc USD EUR"
    Then the program sends no request
    And standard error holds an error
    And the exit code is not 0

  Scenario: The key is unset
    Given the environment variable EXCHANGERATE_API_KEY is unset
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error names "EXCHANGERATE_API_KEY"
    And the exit code is 1
    And standard output is empty

  Scenario: The key is empty
    Given the environment variable EXCHANGERATE_API_KEY holds ""
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error names "EXCHANGERATE_API_KEY"
    And the exit code is 1
    And standard output is empty

  Scenario: The service lacks a currency code
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with status 404 and the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error says the service lacks one of the codes "USD" and "ZZZ"
    And the exit code is 1
    And standard output is empty
    And standard output and standard error hold no text of the key "test-key"

  Scenario Outline: The service answers with another error
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with status 403 and the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error holds "<error-type>"
    And the exit code is 1
    And standard output is empty
    And standard output and standard error hold no text of the key "test-key"

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error says the service gave no answer
    And the exit code is 1
    And standard output is empty
    And standard output and standard error hold no text of the key "test-key"
