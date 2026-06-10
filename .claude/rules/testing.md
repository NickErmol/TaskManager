# Testing Rules

## Test-first per phase
Each implementation step has an `a` (failing tests) and `b` (green tests) half.
Do not write production code until the test step is in place and red.

## Architecture tests (NetArchTest.Rules)
Every service test project asserts the onion dependency rule.
If a build fails an architecture test, restructure the code — **never suppress or skip the test**.

## Test stack per service
`xUnit` + `FluentAssertions` + `NSubstitute` + `Microsoft.AspNetCore.Mvc.Testing` + `Bogus` + `NetArchTest.Rules` + service-specific `Testcontainers`.

## E2E tests
`tests/TaskManager.E2E.Tests/` uses `Microsoft.Playwright`. No service project references allowed there.

## Running tests
```powershell
# All tests in a project
dotnet test tests/TaskManager.Identity.Tests

# Single test by name (substring match)
dotnet test tests/TaskManager.Identity.Tests --filter "FullyQualifiedName~MethodName"
```
