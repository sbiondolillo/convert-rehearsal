Feature: Shout

  Scenario: The console program shouts the greeting of a name
    When the person runs the console program with "--shout Ada"
    Then the console program writes "HELLO, ADA!" to standard output
    And the console program exits with the code 0

  Scenario: The console program shouts the greeting of the default name
    When the person runs the console program with "--shout"
    Then the console program writes "HELLO, WORLD!" to standard output
    And the console program exits with the code 0

  Scenario: The console program without --shout keeps the greeting as it is
    When the person runs the console program with "Ada"
    Then the console program writes "Hello, Ada!" to standard output
    And the console program exits with the code 0

  Scenario: The help lists the option --shout
    When the person runs the console program with "--help"
    Then the standard output lists the option "--shout"
    And the console program exits with the code 0
