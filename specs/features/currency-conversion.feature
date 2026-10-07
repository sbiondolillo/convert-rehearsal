Feature: Currency conversion

  A person converts an amount of money to another currency at the rate of the day.

  Scenario: The program asks the service for the rate of the pair
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sends the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario: The program prints the amount in the target currency
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then standard output is "8.89 EUR"
    And the exit code is 0

  Scenario: The program rounds to two decimals
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers the rate 157.8014
    When a person runs the program with "2.50 USD JPY"
    Then standard output is "394.50 JPY"
    And the exit code is 0

  Scenario: The program takes codes in lower case
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: An amount that is not a number
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    When a person runs the program with "abc USD EUR"
    Then the program sends no request
    And standard error holds an error
    And the exit code is not 0

  Scenario: The key is unset
    Given the environment variable "EXCHANGERATE_API_KEY" is unset
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

  Scenario: The key is empty
    Given the environment variable "EXCHANGERATE_API_KEY" holds ""
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

  Scenario: The service lacks a code
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with status 404 and the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error says the service lacks one of the codes "USD" and "ZZZ"
    And standard output is empty
    And the exit code is 1

  Scenario Outline: The service answers with another error
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with status 403 and the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error holds "<error-type>"
    And standard error and standard output hold no text of the key "test-key"
    And standard output is empty
    And the exit code is 1

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error says the service gave no answer
    And standard error and standard output hold no text of the key "test-key"
    And standard output is empty
    And the exit code is 1
