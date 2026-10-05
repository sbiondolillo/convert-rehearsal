# Greeter

A console program that prints a greeting for a name, and `Hello, world!` when it gets no name.

## Commands

Restore the packages from the lock file:

```sh
dotnet restore --locked-mode
```

Build the solution:

```sh
dotnet build --no-restore
```

Run the tests:

```sh
dotnet test --no-restore
```

Run the program with the name `Ada`:

```sh
dotnet run --project src/App -- Ada
```
