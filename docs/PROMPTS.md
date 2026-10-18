# Prompts

The prompts I used to build TaskFlow with Claude Code, in order.

How I worked:
- **Requirements and rules live in files, not in prompts.** `CLAUDE.md` holds the stack and working rules, and `docs/USER_STORY.md` holds the business rules. Each prompt only says what's new for that step.
- **Ambiguities surfaced before code.** The AI reviews the user story and asks questions first; I make the product decisions.
- **TDD on every layer.** Red → green → refactor, one commit per phase, so the git history shows it.
- **One step per prompt**, and I review before the AI continues.

How I verify AI output:
- **I review the tests before any implementation exists.** A test states *what* must happen in a few lines, so that's where my judgment goes: missing cases, tests that prove nothing, misread rules.
- **Every test is seen failing for the right reason** (a failed assertion, not a compile error) before it goes green.
- **Mutation check:** I break a rule on purpose and confirm that a test catches it.
- **Review in a fresh session**, so the reviewer doesn't share the context that wrote the code.

The *Notes* line under each prompt records what the AI got wrong and how I caught it.

---

## 0. Context (`CLAUDE.md`)

```
.NET 10 Web API (controllers) + React/TS. Requirements: @docs/USER_STORY.md
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

## Reusable prompts

**Green**
```
Make the failing tests pass with the minimum implementation. Don't touch the tests.
```

**Refactor**
```
All green. Refactor for readability and duplication without changing behavior; tests must stay green.
If nothing is worth changing, say so instead of changing it.
```

**Mutation check**
```
Break the riskiest rule we just implemented, run the tests and show me which test fails. Then revert.
```

---

## 1. User story

```
This is my user story. Before any design, list what it leaves ambiguous
and the edge cases it doesn't cover, and ask me about them. Don't propose solutions yet.
```
Then:
```
Update the acceptance criteria with my answers. Don't add any rule I didn't give you.
```
*Notes:*

## 2. Plan

```
Propose the solution structure following CLAUDE.md. Show me the project references
and list the assumptions you're making so I can validate them.
```
*Notes:*

## 3. Scaffold

```
Scaffold the solution we agreed on. Projects and references only, no classes yet.
Central Package Management, warnings as errors, and it must build clean.
```
*Notes:*

## 4. Domain: red

```
Red phase. Tests for the TaskItem and User entities from the user story's rules. Cover the boundaries.
Keep the entities rich: the tests should expect them to enforce their own invariants.
```
*Notes:*

## 5. Application: red

```
Red phase. Tests for a TaskService on top of a repository port, covering the task and privacy criteria.
Someone else's task must be indistinguishable from one that doesn't exist.
```
*Notes:*

## 6. Infrastructure: red

```
Red phase. Integration tests for the Mongo repositories against a real Mongo via Testcontainers; don't mock the driver.
One container shared per test class. Email uniqueness must be enforced by the database,
and a duplicate comes back as a Result, not an exception.
```
*Notes:*

## 7. Identity API

```
Red phase. Tests for auth: register, login returning a JWT, a protected /me and a public health check.
Unit tests for the AuthService, integration tests through WebApplicationFactory for the endpoints.
```
Green:
```
Hash passwords with ASP.NET's PasswordHasher. The JWT key comes from the environment; fail at startup if it's missing.
Use the built-in health checks and rate limit the login endpoint.
```
*Notes:*

## 8. Tasks API

```
Red phase. Integration tests for the Tasks API: CRUD, every endpoint authorized, user taken from the token.
Prove that user A can't read, edit or delete user B's tasks, and that a missing token gives 401.
```
Green:
```
Keep controllers thin and map Result to HTTP in one place. Unexpected errors go through IExceptionHandler.
Built-in OpenAPI with Scalar as the UI, with Bearer auth.
```
*Notes:*

## 9. Seed + Docker

```
Add an idempotent seeder for the demo account in the user story. Then a docker-compose so a clean clone
runs with one command. The browser sees a single origin: Vite proxy in dev, nginx in compose,
routing /api/auth and /api/tasks to each API. No CORS.
```
*Notes:*

## 10. Frontend

```
Red phase. Vitest + Testing Library tests for the task form and the tasks hook:
server validation errors are shown in the form, and a 401 logs the user out.
```
Green:
```
React frontend for the user story: login/register and a tasks screen. Feature-based folders, a typed API client,
auth in a context. Responsive without a UI library. Loading, empty and error states. No console warnings under StrictMode.
```
*Notes:*

## 11. Adversarial review (fresh session)

```
Review this repo the way an interviewer would: architecture leaks, missing ownership checks, timezone bugs,
tests that only prove the happy path. Don't fix anything; list the issues by severity with file and line.
```
*Notes:*

## 12. Fix review findings

```
Fix the high-severity issues one at a time. For each bug, first write the failing test that reproduces it.
```
*Notes:*

## 13. Docs + CI

```
Write the README: what it is, the user story, one-command setup, demo credentials, how to run the tests and coverage.
Add docs/ARCHITECTURE.md with a Mermaid diagram of the layers and the main decisions with their trade-offs.
Then a GitHub Actions workflow that builds and tests on every push, with Docker for Testcontainers, and its badge in the README.
```
*Notes:*

---

## GenAI exercise

My answer is the sequence above. If I had to condense it into one prompt:

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
*Notes:*
