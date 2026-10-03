Feature: Currency conversion at the rate of the day

  As a person who runs the console program
  I want to convert an amount of money to another currency at the rate of the day
  So that I know what the amount is worth in that currency

  Scenario: The program asks the service for the rate of the pair
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sends the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario: The program converts an amount and prints it with two decimals
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then standard output is "8.89 EUR"
    And the exit code is 0

  Scenario: The program converts a decimal amount
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate 157.8014
    When a person runs the program with "2.50 USD JPY"
    Then standard output is "394.50 JPY"
    And the exit code is 0

  Scenario: The program takes currency codes in lower case
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: The program refuses an amount that is not a number
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    When a person runs the program with "abc USD EUR"
    Then the program sends no request
    And standard error holds an error
    And the exit code is not 0

  Scenario: The program needs the key of the service when it is unset
    Given the environment variable EXCHANGERATE_API_KEY is unset
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error holds a line that names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

  Scenario: The program needs the key of the service when it is empty
    Given the environment variable EXCHANGERATE_API_KEY is empty
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error holds a line that names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

  Scenario: The service lacks a currency code
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with status 404 and the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error holds a line that says the service lacks one of the two codes and names "USD" and "ZZZ"
    And standard output is empty
    And the exit code is 1

  Scenario Outline: The service answers with another error
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"
    And the service answers with status 403 and the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error holds a line that holds "<error-type>"
    And standard error and standard output hold no text of the key "test-key"
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
    Then standard error holds a line that says the service gave no answer
    And standard error and standard output hold no text of the key "test-key"
    And standard output is empty
    And the exit code is 1
