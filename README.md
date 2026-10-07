# Currency converter

A console program that takes an amount, a source currency code and a target currency code, and converts the amount at the rate of the day from ExchangeRate-API. It reads the key of the service from the environment variable `EXCHANGERATE_API_KEY`.

## Commands

Restore the packages from the lock file:

```sh
dotnet restore --locked-mode
```

Build the solution:

```sh
dotnet build --no-restore
```

Check that the code follows the format rules, as the `Format` step of CI does:

```sh
dotnet format --verify-no-changes --no-restore
```

Run the build suite (the integration suite runs separately, with `dotnet run --no-restore --project tests/Integration`):

```sh
dotnet test --no-restore
```

Run the program to convert 10 USD to EUR:

```sh
EXCHANGERATE_API_KEY=<key> dotnet run --project src/App -- 10 USD EUR
```
