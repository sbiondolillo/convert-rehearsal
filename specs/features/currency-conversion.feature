Feature: Currency conversion

  A person converts an amount from one currency to another at the rate of the day.
  The rate comes from ExchangeRate-API, and the key of the service is in the
  environment variable EXCHANGERATE_API_KEY.

  Background:
    Given the environment variable EXCHANGERATE_API_KEY holds "test-key"

  Scenario: The program asks the service for the rate of the pair
    Given the service answers the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then the program sent the request "GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"

  Scenario: The program prints the amount in the other currency
    Given the service answers the rate 0.8888
    When a person runs the program with "10 USD EUR"
    Then standard output is "8.89 EUR"
    And the exit code is 0

  Scenario: The program rounds the converted amount to two decimals
    Given the service answers the rate 157.8014
    When a person runs the program with "2.50 USD JPY"
    Then standard output is "394.50 JPY"
    And the exit code is 0

  Scenario: The program takes the codes in lower case
    Given the service answers the rate 0.8888
    When a person runs the program with "10 usd eur"
    Then standard output is "8.89 EUR"

  Scenario: An amount that is not a number sends no request
    When a person runs the program with "abc USD EUR"
    Then the program sent no request
    And standard error holds an error
    And the exit code is not 0
    And standard output is empty

  Scenario: An unset key sends no request
    Given the environment variable EXCHANGERATE_API_KEY is unset
    When a person runs the program with "10 USD EUR"
    Then the program sent no request
    And standard error names "EXCHANGERATE_API_KEY"
    And the exit code is 1
    And standard output is empty

  Scenario: An empty key sends no request
    Given the environment variable EXCHANGERATE_API_KEY holds ""
    When a person runs the program with "10 USD EUR"
    Then the program sent no request
    And standard error names "EXCHANGERATE_API_KEY"
    And the exit code is 1
    And standard output is empty

  Scenario: The service lacks a currency code
    Given the service answers status 404 with the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error says the service lacks one of the codes "USD" and "ZZZ"
    And the exit code is 1
    And standard output is empty
    And standard error and standard output hold no text of the key

  Scenario Outline: The service answers with another error
    Given the service answers status 403 with the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error holds "<error-type>"
    And the exit code is 1
    And standard output is empty
    And standard error and standard output hold no text of the key

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error says the service gave no answer
    And the exit code is 1
    And standard output is empty
    And standard error and standard output hold no text of the key
