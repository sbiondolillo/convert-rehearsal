Feature: Currency conversion

  A person converts an amount of money to another currency at the rate of the day.

  Scenario: The program asks the service for the pair with the key from the environment
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sent the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario Outline: The program prints the amount in the target currency
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers the rate <rate>
    When a person runs the program with "<arguments>"
    Then standard output is "<output>"
    And the exit code is 0

    Examples:
      | arguments    | rate     | output     |
      | 10 USD EUR   | 0.8888   | 8.89 EUR   |
      | 2.50 USD JPY | 157.8014 | 394.50 JPY |

  Scenario: The program takes codes in lower case
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: The program rejects an amount that is not a number
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    When a person runs the program with "abc USD EUR"
    Then the program sent no request
    And standard error holds an error
    And the exit code is not 0

  Scenario Outline: The program needs the key of the service
    Given the environment variable EXCHANGERATE_API_KEY <state>
    When a person runs the program with "10 USD EUR"
    Then the program sent no request
    And standard error names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

    Examples:
      | state    |
      | is unset |
      | is empty |

  Scenario: The service lacks a currency code
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error says the service lacks one of the two codes
    And standard error names "USD" and "ZZZ"
    And standard output is empty
    And the exit code is 1

  Scenario Outline: The service answers with another error
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error holds "<error-type>"
    And standard error and standard output hold no text of the key
    And standard output is empty
    And the exit code is 1

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error says the service gave no answer
    And standard error and standard output hold no text of the key
    And standard output is empty
    And the exit code is 1
