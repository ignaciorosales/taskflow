# Plan (output of prompt 1, pending validation)

## Structure

```
TaskFlow.slnx
Directory.Build.props        net10.0, Nullable, TreatWarningsAsErrors
Directory.Packages.props     Central Package Management
src/
  TaskFlow.Domain/           Tasks/ (TaskItem, TaskStatus), Users/ (User), Common/ (Result, Error)
  TaskFlow.Application/      Tasks/ (TaskService, ITaskRepository, DTOs), Auth/ (AuthService, IUserRepository, IPasswordHasher, ITokenService)
  TaskFlow.Infrastructure/   Persistence/ (Mongo repos, indexes, seeder), Security/ (PasswordHasher, JwtTokenService)
  TaskFlow.Api.Common/       JWT validation setup, Result→ProblemDetails mapping, IExceptionHandler
  TaskFlow.Identity.Api/     AuthController
  TaskFlow.Tasks.Api/        TasksController
tests/
  TaskFlow.Domain.Tests/  TaskFlow.Application.Tests/  TaskFlow.Infrastructure.Tests/
  TaskFlow.Identity.Api.Tests/  TaskFlow.Tasks.Api.Tests/
frontend/
docs/
```

## References

- Domain → nothing
- Application → Domain
- Infrastructure → Application
- Api.Common → Application
- Identity.Api, Tasks.Api → Application, Infrastructure, Api.Common
- Each test project → the project it tests

## Packages (need approval)

- API: MongoDB.Driver, Microsoft.Extensions.Identity.Core, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore
- Tests: NSubstitute, Microsoft.Extensions.TimeProvider.Testing, Testcontainers.MongoDb, Microsoft.AspNetCore.Mvc.Testing

## Assumptions (need validation)

1. Extra `Api.Common` project: both APIs share JWT validation and Result→HTTP mapping; Application can't hold ASP.NET code and Infrastructure isn't the place for HTTP concerns.
2. One test project per API: both APIs generate a global `Program` class, so a single test project referencing both won't compile.
3. `Result`/`Error` live in Domain, since entity methods return them.
4. xUnit v2 from the SDK template (v3 needs an extra template package).
5. Due date is `DateOnly`; verify the Mongo driver serializes it, otherwise add a serializer.
6. Services (`TaskService`, `AuthService`), not one handler per use case.
7. Status changes through `PATCH /api/tasks/{id}/status`: a state transition with its own rules, not a field edit.

## Open question

Who makes the commits: the AI (with a `Co-Authored-By: Claude` line) or me?
