Feature: Currency conversion
  As a person who runs the console program
  I want to convert an amount of money to another currency at the rate of the day
  So that I know what the amount is worth in that currency

  Scenario: The program asks the service for the pair with the key from the environment
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    When a person runs the program with "10 USD EUR"
    Then the program sends the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario Outline: The program prints the converted amount, rounded to two decimals, with the target code
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with the rate <rate>
    When a person runs the program with "<arguments>"
    Then standard output is "<output>"
    And the exit code is 0

    Examples:
      | arguments    | rate     | output     |
      | 10 USD EUR   | 0.8888   | 8.89 EUR   |
      | 2.50 USD JPY | 157.8014 | 394.50 JPY |

  Scenario: The program takes the codes in lower case and prints the target code in upper case
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: An amount that is not a number
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    When a person runs the program with "abc USD EUR"
    Then the program sends no request
    And standard error holds an error
    And the exit code is not 0
    And standard output is empty

  Scenario: The key is unset
    Given the environment variable "EXCHANGERATE_API_KEY" is unset
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error is a line that names "EXCHANGERATE_API_KEY"
    And the exit code is 1
    And standard output is empty

  Scenario: The key is empty
    Given the environment variable "EXCHANGERATE_API_KEY" holds ""
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error is a line that names "EXCHANGERATE_API_KEY"
    And the exit code is 1
    And standard output is empty

  Scenario: The service lacks a code
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with the status 404 and the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error is a line that says the service lacks one of the two codes and names "USD" and "ZZZ"
    And the exit code is 1
    And standard output is empty
    And standard output and standard error hold no text of the key

  Scenario Outline: The service answers with another error
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with the status 403 and the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error is a line that holds "<error-type>"
    And the exit code is 1
    And standard output is empty
    And standard output and standard error hold no text of the key

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error is a line that says the service gave no answer
    And the exit code is 1
    And standard output is empty
    And standard output and standard error hold no text of the key
