# Prompts

The prompts I used to build TaskFlow with an AI coding assistant, in the order I used them.

How I worked:
- **Context lives in one file** (`CLAUDE.md`), so each prompt stays short and only says what the AI can't infer.
- **TDD on every layer.** Red → green → refactor, and each phase is a separate commit, so the git history shows it.
- **One step per prompt**, with a checkpoint where I review before the AI continues.

How I verify AI output:
- The AI writes both tests and code, but **in separate steps, and I review the tests before any implementation exists.** A test states *what* must happen in a few lines; that is where my judgment goes: missing cases, tests that prove nothing, misread rules.
- **Tests written first can't mirror the code.** Tests written after the code tend to copy it and pass even when it's wrong.
- **Every test is seen failing, for the right reason** (an assertion, not a compile error), before it goes green.
- The business rules come from my prompts; the AI only translates them into tests.
- On top of that: breaking the code on purpose to confirm a test catches it, an adversarial review pass, and manual testing.

Under each prompt, the *Notes* line records what the AI got wrong, which test cases I added or corrected, and how I caught it.

---

## 0. Project context (`CLAUDE.md`)

```
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
```

## Reusable TDD prompts

Used after every red phase below.

**Green**
```
Make the failing tests pass with the minimum implementation. Don't touch the tests.
```

**Refactor**
```
All green. Refactor for readability and duplication without changing behavior; tests must stay green.
If nothing is worth changing, say so instead of changing it.
```

---

## 1. Plan

```
Before writing any code, propose the solution structure for this exercise following CLAUDE.md.
Show me the project references and list the assumptions you're making so I can validate them.
```
*Notes:*

## 2. Scaffold

```
Scaffold the solution we agreed on. Projects and references only, no classes yet.
Central Package Management, warnings as errors, and it must build clean.
```
*Notes:*

## 3. Domain: red

```
Red phase. Write tests for a TaskItem entity: required title (max 100), optional description and due date,
status flows Todo → InProgress → Done and can be reopened.
A due date can't be in the past when the task is created, but editing an overdue task must still work.
Also a User entity that normalizes and validates its email.
Cover the boundaries. Keep the entities rich: the tests should expect them to enforce their own invariants.
```
*Notes:*

## 4. Application: red

```
Red phase. Tests for a TaskService on top of a repository port. Every operation is scoped to the current user,
and someone else's task must look exactly like a task that doesn't exist.
```
*Notes:*

## 5. Infrastructure: red

```
Red phase. Integration tests for the Mongo repositories against a real Mongo via Testcontainers; don't mock the driver.
One container shared per test class. Email uniqueness must be enforced by the database,
and a duplicate comes back as a Result, not an exception.
```
*Notes:*

## 6. Identity API: red

```
Red phase. Tests first for auth: register, login returning a JWT, a protected /me and a public health check.
Unit tests for the AuthService, integration tests through WebApplicationFactory for the endpoints.
A failed login must not reveal whether the email exists.
```
Then go green with:
```
Hash passwords with ASP.NET's PasswordHasher. The JWT key comes from the environment; fail at startup if it's missing.
Use the built-in health checks and rate limit the login endpoint.
```
*Notes:*

## 7. Tasks API: red

```
Red phase. Integration tests for the Tasks API: CRUD, every endpoint authorized, user taken from the token.
They must prove user A can't read, edit or delete user B's tasks, and that a missing token gives 401.
```
Then go green with:
```
Keep controllers thin and map Result to HTTP in one place. Unexpected errors go through IExceptionHandler.
Built-in OpenAPI with Scalar as the UI, with Bearer auth.
```
*Notes:*

## 8. Seed + Docker

```
Add an idempotent seeder with a demo user and a handful of tasks covering every status, including one overdue.
Then a docker-compose so a clean clone runs with a single command.
The browser should see a single origin: Vite proxy in dev, nginx in compose, routing /api/auth and /api/tasks
to each API. No CORS.
```
*Notes:*

## 9. Frontend

Red first:
```
Red phase. Vitest + Testing Library tests for the task form and the tasks hook:
server validation errors are shown in the form, and a 401 or an expired token logs the user out.
```
Then go green with:
```
React frontend: login/register and a tasks screen with full CRUD and a status filter.
Feature-based folders, a typed API client, auth in a context. Responsive without a UI library.
Handle loading, empty and error states. No console warnings under StrictMode.
```
*Notes:*

## 10. Adversarial review

```
Review the repo the way an interviewer would: architecture leaks, missing ownership checks, timezone bugs,
tests that only prove the happy path. Don't fix anything; list the issues by severity with file and line.
```
*Notes:*

## 11. Fix review findings

```
Fix the high-severity issues one at a time. For each bug, first write the failing test that reproduces it.
```
*Notes:*

## 12. Docs

```
Write the README: what it is, one-command setup, demo credentials, how to run tests and the coverage report.
Add docs/ARCHITECTURE.md with a Mermaid diagram of the layers and the main design decisions with their trade-offs.
```
*Notes:*

## 13. CI

```
GitHub Actions workflow: build and test on every push, Docker available for Testcontainers. Add the badge to the README.
```
*Notes:*

---

## GenAI exercise: one-shot prompt

The prompt the exercise asks for: a single self-contained prompt for a task-management API.

```
Generate a REST API in ASP.NET Core 10 for task management. A task has a title, description, status and due date,
and belongs to the authenticated user (User already exists; the id comes from the JWT).
Full CRUD, and users only ever see their own tasks.
No EF: repository over MongoDB, validation in a service layer, ProblemDetails for errors.
Work test-first: give me the service tests for validation and ownership before the implementation.
State your assumptions before the code.
```
*Notes:*
