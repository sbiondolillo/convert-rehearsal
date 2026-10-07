Feature: Currency conversion

  Background:
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"

  Scenario: The program asks the service for the rate of the pair
    Given the service answers the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sent the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario Outline: The program prints the converted amount
    Given the service answers the rate <rate>
    When a person runs the program with "<arguments>"
    Then standard output is "<output>"
    And the exit code is 0

    Examples:
      | arguments    | rate     | output     |
      | 10 USD EUR   | 0.8888   | 8.89 EUR   |
      | 2.50 USD JPY | 157.8014 | 394.50 JPY |

  Scenario: The program takes the codes in lower case
    Given the service answers the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"
    And the exit code is 0

  Scenario: An amount that is not a number
    When a person runs the program with "abc USD EUR"
    Then the program sent no request
    And standard error is not empty
    And standard output is empty
    And the exit code is not 0

  Scenario Outline: The key is missing
    Given the environment variable "EXCHANGERATE_API_KEY" is <state>
    When a person runs the program with "10 USD EUR"
    Then the program sent no request
    And standard error holds one line, and it names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

    Examples:
      | state |
      | unset |
      | empty |

  Scenario: The service lacks a code
    Given the service answers with status 404 and the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error holds one line, and it says the service lacks one of the codes "USD" and "ZZZ"
    And standard output is empty
    And the exit code is 1

  Scenario Outline: The service answers with another error
    Given the service answers with status 403 and the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error holds one line, and it holds "<error-type>"
    And standard error does not hold "test-key"
    And standard output is empty
    And the exit code is 1

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error holds one line, and it says the service gave no answer
    And standard error does not hold "test-key"
    And standard output does not hold "test-key"
    And standard output is empty
    And the exit code is 1
