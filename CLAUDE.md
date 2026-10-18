.NET 10 Web API (controllers) + React/TS.
Clean Architecture: Domain has no dependencies, Application owns the ports, Infrastructure implements them.
MongoDB with the official driver. No EF, Dapper, MediatR or AutoMapper.
Expected failures return a Result, not exceptions. Errors go out as ProblemDetails. Time goes through TimeProvider.
Structured logging with ILogger; never log passwords or tokens.
xUnit + NSubstitute; Testcontainers for anything that touches Mongo.

TDD always: red, then green, then refactor. Never write implementation in the red phase,
never change tests in the green phase. After the red phase, show me the tests and the failure output and wait.
Run the tests after every change and show me the result.
Commits: test: for red, feat: for green, refactor: for refactor.

Ask before adding any NuGet or npm package.
Do only the step I ask for. If something is ambiguous, ask before assuming.
