Feature: Conversion failures

  Scenario Outline: The program needs the key of the service
    Given the environment variable "EXCHANGERATE_API_KEY" is <state>
    When a person runs the program with "10 USD EUR"
    Then the program sends no request
    And standard error has a line that names "EXCHANGERATE_API_KEY"
    And standard output is empty
    And the exit code is 1

    Examples:
      | state |
      | unset |
      | empty |

  Scenario: The service lacks one of the codes
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with the error-type "unsupported-code"
    When a person runs the program with "10 USD ZZZ"
    Then standard error has a line that says the service lacks one of the codes and names "USD" and "ZZZ"
    And standard output is empty
    And the exit code is 1

  Scenario Outline: The service answers with another error
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service answers with the error-type "<error-type>"
    When a person runs the program with "10 USD EUR"
    Then standard error has a line that holds "<error-type>"
    And standard output is empty
    And standard output and standard error hold no text of the key
    And the exit code is 1

    Examples:
      | error-type       |
      | invalid-key      |
      | inactive-account |

  Scenario: The service gives no answer
    Given the environment variable "EXCHANGERATE_API_KEY" holds "test-key"
    And the service gives no answer
    When a person runs the program with "10 USD EUR"
    Then standard error has a line that says the service gave no answer
    And standard output is empty
    And standard output and standard error hold no text of the key
    And the exit code is 1
