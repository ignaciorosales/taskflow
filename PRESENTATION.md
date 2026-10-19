# TaskFlow: my thought process

TaskFlow is a private task list: a .NET 10 backend (two Web APIs over MongoDB, Clean Architecture, built test-first) and a React frontend with a kanban board. This document explains how I got there: what I decided, why, and how I used AI along the way without trusting it blindly.

- **Run it:** `docker compose up --build`, then http://localhost:8080 with `demo@taskflow.dev` / `Demo123!` ([README](README.md))
- **Every prompt I used, in order, with notes:** [docs/PROMPTS.md](docs/PROMPTS.md)
- **Architecture and trade-offs:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

## 1. Start with the user story, and pin it down

> **As a** busy professional juggling work and personal to-dos, **I want** a private list of my tasks with a status, a priority and a due date, **so that** I can see at a glance what's pending, what's in progress and what's overdue.

I chose a story that exercises everything the brief asks for: users, login, per-user data, CRUD, and real business rules (overdue, completion, due dates).

Before any design, I asked the AI to list what the story left ambiguous, and I answered every question myself. It raised 20, from "is a title of only spaces empty?" to "in whose time zone is a due date in the past?". In a second round it caught two of my answers contradicting each other: I wanted to hide whether an email exists, yet log users in right after registering. I chose to reveal "already registered" and slow enumeration down with a rate limit, and wrote that down as a trade-off. The result is [docs/USER_STORY.md](docs/USER_STORY.md): every rule the app enforces, decided by me before any code.

## 2. Plan before code

I asked for a solution structure that follows my working rules in `CLAUDE.md` (Clean Architecture, no EF/Dapper/MediatR, Results instead of exceptions, TDD), plus **the assumptions it was making, listed for me to validate**. It came back with 14 ([docs/PLAN.md](docs/PLAN.md)), for example:

- an extra `Api.Common` project, because both APIs share JWT validation and error mapping, and neither the Application nor Infrastructure is the right place for HTTP;
- one test project per API, because each API generates its own `Program` class;
- the Domain receives `now` as a parameter instead of a clock, so entities stay pure and tests use exact instants.

```
Domain            → depends on nothing: TaskItem, User, Email, Result
Application       → owns the ports: TaskService, AuthService, repository interfaces
Infrastructure    → implements them: Mongo repositories, password hashing, JWT
Api.Common        → the HTTP edge both APIs share
Identity.Api      → register, login, refresh, logout, me
Tasks.Api         → CRUD on /api/tasks, every endpoint authorized
```

## 3. Build it test-first, one layer at a time

Every layer went red → green → refactor, one commit per phase, so the history shows it: 17 `test:` commits, each followed by its `feat:`.

- **I reviewed every red phase before any implementation existed.** A test states *what* must happen in a few lines, so that's where my judgment goes: missing cases, tests that prove nothing, misread rules.
- **Every test was seen failing for the right reason:** a failed assertion, not a compile error.
- **The implementation never changed a test.** No `feat:` commit in the history touches a test file.
- **Mongo is tested against a real Mongo** (Testcontainers), because unique indexes, TTL, sorting and paging are Mongo's behavior; a mock would only test my assumptions about it.

Today: **296 tests** (248 backend, 48 frontend), and a GitHub Actions workflow that builds, tests, and starts the whole stack with Docker Compose on every push.

## 4. Decisions I'd defend in a code review

| Decision | Why | Cost I accepted |
|---|---|---|
| Two APIs, one database | The brief asks for a second API for users; the Tasks API never sees a password | A shared signing key |
| Expected failures are `Result`s | Validation, not-found and conflicts are part of the contract; exceptions are for bugs | Some `if (failed) return` lines |
| Rules live in rich entities | `TaskItem.Create`, `Edit`, `ChangeStatus` enforce every rule and report all errors at once | None worth noting |
| Privacy enforced twice | The repository only finds and saves the owner's tasks; the service checks again. Someone else's task is a 404, same as a missing one | None worth noting |
| Due dates are UTC instants | The API rejects a date without an offset rather than guess a time zone | Clients must send `Z` or an offset |
| Refresh token in an HttpOnly cookie, rotated on every use | JavaScript can't read it; reusing a rotated token revokes every session | Up to 15 minutes of access after logout |
| No ORM, no MediatR, no UI library | Ruled out by the brief or not needed: the driver, two services and plain CSS cover it | More code written by hand |

The full list is in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## 5. GenAI: how I used it

I built the whole project with **Claude Code**, one step per prompt, and reviewed every step before the next. All 18 steps are recorded in [docs/PROMPTS.md](docs/PROMPTS.md).

### The prompt I'd use for the exercise's task API

My real answer is the sequence of steps. Condensed into one prompt:

```
Build a REST API in ASP.NET Core 10 for task management.

User story: as a user I want a private list of tasks so I can track what's pending and overdue.
A task has a title (required, max 100), description (optional), status (Todo, InProgress, Done) and due date
(optional, not in the past on creation). Each task belongs to the authenticated user; User already exists
and the user id comes from the JWT.

Rules:
- Full CRUD. Users only ever see their own tasks; someone else's task returns 404, same as a missing one.
- Clean Architecture: validation and business rules in a service layer, a repository over MongoDB. No EF.
- Errors as ProblemDetails, with validation errors per field.

Work test-first: state your assumptions, then give me the service tests for validation and ownership,
and wait for my review before writing the implementation.
```

What makes it work: the rules are concrete (limits, who sees what, what an error looks like), it names what *not* to use, and it makes the AI stop after the tests so I review them before any code exists.

### A sample of the output

The privacy rule, as a test written before the implementation ([TaskServicePrivacyTests.cs](tests/TaskFlow.Application.Tests/Tasks/TaskServicePrivacyTests.cs)), run for every operation against a missing, a deleted and someone else's task:

```csharp
[Theory]
[MemberData(nameof(Cases))]
public async Task Someone_elses_or_deleted_task_looks_exactly_like_a_missing_one(Target target, string operation)
{
    var result = await Operations[operation](Service, UserId, IdOf(target));

    Assert.Equal([TaskErrors.NotFound], result.Errors);
    await Repository.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
}
```

The service that makes it pass ([TaskService.cs](src/TaskFlow.Application/Tasks/TaskService.cs)), and a domain rule ([TaskItem.cs](src/TaskFlow.Domain/Tasks/TaskItem.cs)):

```csharp
private async Task<TaskItem?> FindOwnedAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
{
    var task = await _repository.GetByIdAsync(userId, taskId, cancellationToken);
    return task is not null && task.OwnerId == userId && task.DeletedAt is null ? task : null;
}

public Result ChangeStatus(TaskItemStatus? requested, DateTimeOffset now)
{
    if (requested is not { } status || !Enum.IsDefined(status))
        return Result.Failure(TaskErrors.StatusInvalid);

    if (status != TaskItemStatus.Done)
        CompletedAt = null;
    else if (Status != TaskItemStatus.Done)
        CompletedAt = now;

    Status = status;
    return Result.Success();
}
```

The endpoints are in [TasksController.cs](src/TaskFlow.Tasks.Api/Controllers/TasksController.cs).

### How I validated it

- **Rules in files, not in prompts.** `CLAUDE.md` holds the working rules (TDD, no forbidden libraries, ask before adding a package, ask when something is ambiguous) and the user story holds the business rules, so every step is checked against the same source.
- **Tests before code, reviewed by me.** Implementation only started once I'd approved the red phase.
- **Mutation checks.** I broke a rule on purpose to see a test fail. That caught a test that proved nothing: a StrictMode test passed even with the protection it covered removed.
- **A review in a fresh session,** so the reviewer didn't share the context that wrote the code. It found 1 high, 3 medium and 10 low issues.
- **`/code-review` runs** at the end: one on my own documentation, one on the whole source.
- **Decisions stay mine.** When something was ambiguous, the AI stopped and laid out the options with their trade-offs; I chose.

### What I corrected or improved

| What the AI produced | What was wrong | How it was caught and fixed |
|---|---|---|
| The first task form | Editing an overdue task failed: the form rounded the due date to the minute, so the server saw it "moved into the past" | Fresh-session review; fixed test-first; the old tests only used dates on the minute |
| `PUT` and `PATCH` bodies | A missing priority silently became Low; a missing status became To do | Review; now a 400 with a field error |
| Date parsing | A date without an offset was read in the server's time zone | Review; now rejected |
| The repository | Ownership was only checked in the service | Review; now enforced in the database too, and a save that loses a race to a delete is a 404 instead of resurrecting the task |
| The board | Two tabs refreshing at once logged the user out; *Load more* could mix filters or repeat cards; a selection ending on the backdrop closed the dialog and lost input | `/code-review`; nine of ten findings fixed, eight test-first |
| My own prompt log | Seven claims overstated or misordered what happened | `/code-review` on the docs; corrected against the git history |
| A request to drop the cards' move buttons | It would break the story ("move with buttons") and leave phones and keyboards unable to move tasks | The AI pushed back; I chose a Move menu instead |

### Edge cases, authentication and validation

- **Validation** lives in the Domain and returns every error at once, with the field it belongs to; the API turns them into ProblemDetails, so the form shows each message next to its input.
- **Authentication:** passwords hashed with ASP.NET's PBKDF2 hasher; 15-minute JWTs; refresh tokens stored only as SHA-256 hashes, rotated on every use, with reuse detection.
- **Brute force:** 3 failed logins lock an email for a minute, registered or not, so the lock reveals nothing; login and register are limited to 10 requests per minute per IP. An unknown email still checks a dummy hash, so timing reveals nothing either.
- **Privacy:** the owner always comes from the token, never the request; two real users prove it in the API tests.
- **Edge cases with tests:** limits counted after trimming, a due date exactly at "now", deleted tasks, concurrent registrations of one email, concurrent use of one refresh token, a page number so large it overflowed.

## 6. What I'd do next

- Optimistic concurrency (a version field), instead of last-write-wins on edits.
- Keyboard drag and drop, and an end-to-end test with Playwright.
- OpenTelemetry tracing across both APIs.
- Show the logged-in account in the header.
