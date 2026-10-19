# Plan (output of prompt 2, validated)

## Structure

```
TaskFlow.slnx
Directory.Build.props        net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
Directory.Packages.props     Central Package Management
src/
  TaskFlow.Domain/           Common/ (Result, Error)
                             Tasks/  (TaskItem, TaskStatus, TaskPriority)
                             Users/  (User, Email)
  TaskFlow.Application/      Tasks/  (TaskService, ITaskRepository, TaskQuery, PagedResult, DTOs)
                             Auth/   (AuthService, PasswordPolicy, IUserRepository, IRefreshTokenRepository,
                                      ILoginAttemptRepository, IPasswordHasher, ITokenService)
  TaskFlow.Infrastructure/   Persistence/ (Mongo repositories, index setup, DemoSeeder)
                             Security/    (PasswordHasher, JwtTokenService)
  TaskFlow.Api.Common/       JWT validation, Result→ProblemDetails mapping, IExceptionHandler, health checks
  TaskFlow.Identity.Api/     AuthController
  TaskFlow.Tasks.Api/        TasksController
tests/
  TaskFlow.Domain.Tests/  TaskFlow.Application.Tests/  TaskFlow.Infrastructure.Tests/
  TaskFlow.Identity.Api.Tests/  TaskFlow.Tasks.Api.Tests/
frontend/                    Vite + React + TS
docker-compose.yml, nginx/
docs/
```

## References

- Domain → nothing
- Application → Domain
- Infrastructure → Application
- Api.Common → Application
- Identity.Api, Tasks.Api → Application, Infrastructure, Api.Common
- Each test project → the project it tests

## Endpoints

**Identity API**
- `POST /api/auth/register`: 201 with an access token, and sets the refresh cookie. A taken email gives 409.
- `POST /api/auth/login`: 200 with an access token, and sets the refresh cookie. Any failure gives 401 with the same error. A locked-out email gives 429.
- `POST /api/auth/refresh`: rotates the refresh token and returns a new access token.
- `POST /api/auth/logout`: revokes the refresh token and clears the cookie.
- `GET /api/auth/me`: requires authorization.
- `GET /health`: public.

**Tasks API** (every endpoint requires authorization; the owner id is taken from the token)
- `GET /api/tasks?status=&priority=&overdue=&sortBy=&desc=&page=&pageSize=`
- `GET /api/tasks/{id}`
- `POST /api/tasks`
- `PUT /api/tasks/{id}`: edits the title, description, priority and due date. Last write wins.
- `PATCH /api/tasks/{id}/status`: what the status buttons call.
- `DELETE /api/tasks/{id}`: a soft delete.
- `GET /health`: public.

A task that is missing, owned by someone else or soft-deleted gives 404 in every case.

## Data (one Mongo database, `taskflow`)

- `users`: a unique index on the normalized email.
- `tasks`: an index on `{ ownerId, deletedAt }`. `deletedAt` is null until the task is deleted.
- `refresh_tokens`: stores a SHA-256 hash of the token, never the raw value. Has a TTL index on `expiresAt`.
- `login_attempts`: one document per normalized email, with a failure count and `lockedUntil`.

## Packages (need approval)

- Infrastructure: MongoDB.Driver, Microsoft.IdentityModel.JsonWebTokens
- Api.Common: Microsoft.AspNetCore.Authentication.JwtBearer
- APIs: Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore
- Tests: NSubstitute, Microsoft.Extensions.TimeProvider.Testing, Testcontainers.MongoDb, Microsoft.AspNetCore.Mvc.Testing
- These need no package: `PasswordHasher<T>` (through `FrameworkReference Microsoft.AspNetCore.App`), the rate limiter, health checks, ProblemDetails, `TimeProvider`, `MailAddress`.
- Frontend packages are listed in step 10.

## Assumptions (need validation)

1. **Extra `Api.Common` project:** both APIs share JWT validation and the Result→HTTP mapping. Application can't hold ASP.NET code, and Infrastructure isn't the place for HTTP concerns.
2. **One test project per API:** each API generates its own global `Program` class, so a single test project referencing both won't compile.
3. **`Result`/`Error` live in Domain**, because entity methods return them. An `Error` has a code, a message and an optional field, which becomes the per-field validation errors in the ProblemDetails.
4. **The Domain gets `now` as a parameter** (for example `TaskItem.Create(..., now)`) rather than a `TimeProvider`. That keeps the entities pure. The services read the time from `TimeProvider` and pass it in.
5. **Rules live where the data does:** the task rules, the email format and status transitions live in Domain. The password policy and lockout live in Application, because the domain only ever sees the password hash.
6. **Email format is checked with `MailAddress.TryCreate`**, plus a check that the parsed address equals the trimmed input. It accepts `a@b`, which has no TLD.
7. **Due date is a `DateTimeOffset`, normalized to UTC.** The UI shows it in local time. This replaces the old `DateOnly` idea.
8. **Services, not one handler per use case:** `TaskService` and `AuthService`.
9. **Tokens:** HS256 with the key from an environment variable, shared by both APIs. The refresh token goes in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie scoped to `/api/auth`, and the access token is kept only in frontend memory. This works because the browser sees a single origin. **Reuse detection:** if an already-rotated refresh token is used again, every one of that user's tokens is revoked.
10. **Lockout is stored in Mongo**, not in memory, so it survives restarts and works with more than one instance. Expiry is checked in code against `TimeProvider`. A successful login resets the count.
11. **Per-IP limit:** the built-in fixed-window rate limiter on `register` and `login`, set to 10 requests per minute per IP. Bots spread across many IPs can still enumerate emails slowly; this is the accepted trade-off.
12. **Paging defaults:** `page` is 1, `pageSize` is 20 (maximum 100), and the response includes `totalCount`. The default sort is newest first. When sorting by due date, tasks without one go last.
13. **The demo seeder lives in Infrastructure** and runs when the Identity API starts. It writes the demo user and tasks only when the demo email isn't there yet.
14. **xUnit v2** from the SDK template (v3 needs an extra template package).
