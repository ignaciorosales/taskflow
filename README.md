# TaskFlow

[![CI](https://github.com/ignaciorosales/taskflow/actions/workflows/ci.yml/badge.svg)](https://github.com/ignaciorosales/taskflow/actions/workflows/ci.yml)

A private task list: each user sees their own tasks with a status, a priority and a due date, and can tell at a glance what's pending, in progress or overdue.

Two ASP.NET Core 10 Web APIs (identity and tasks) over MongoDB, a React + TypeScript frontend, and nginx in front so the browser sees a single origin.

## User story

> **As a** busy professional juggling work and personal to-dos,
> **I want** a private list of my tasks with a status, a priority and a due date,
> **so that** I can see at a glance what's pending, what's in progress and what's overdue.

The full acceptance criteria are in [docs/USER_STORY.md](docs/USER_STORY.md). In short:

- Register and log in with email and password. Three failed logins lock that email for a minute, and login and register are rate limited per IP.
- 15-minute access tokens, renewed by a 7-day refresh token that rotates on every use. Logging out revokes it.
- Tasks have a title, an optional description, a priority (Low, Medium or High), an optional UTC due date and a status (To do, In progress or Done). Overdue and *Completed late* tasks stand out.
- Tasks are paged and can be filtered by status, priority and overdue, and sorted by due date, priority or creation date. A deleted task is soft-deleted.
- You only ever see your own tasks. Someone else's task answers 404, the same as one that doesn't exist.

## Run it

You need Docker.

```sh
docker compose up --build
```

Open http://localhost:8080 and log in with the demo account. Its credentials are also on the login screen:

| Email | Password |
|---|---|
| `demo@taskflow.dev` | `Demo123!` |

The demo account has tasks in every status, including an overdue one, with dates relative to the first startup. Restarting changes nothing: the data stays in the `mongo-data` volume. `docker compose down -v` deletes it, and the next start seeds the demo again.

Compose uses a built-in local signing key. Set `JWT_KEY` (at least 32 bytes) for anything that isn't your own machine.

### Without Docker for the apps

You need the .NET 10 SDK, Node 22 and a Mongo:

```sh
docker run -d -p 27017:27017 mongo:8.0

export Mongo__ConnectionString=mongodb://localhost:27017
export Jwt__Key=local-dev-signing-key-at-least-32-bytes
dotnet run --project src/TaskFlow.Identity.Api   # http://localhost:5100
dotnet run --project src/TaskFlow.Tasks.Api      # http://localhost:5059, in a second shell

cd frontend && npm install && npm run dev        # http://localhost:5173
```

Both APIs need the same `Jwt__Key`. Vite proxies `/api/auth` and `/api/tasks` to them, just as nginx does in compose. Each API serves its interactive docs at `/scalar`, for example http://localhost:5059/scalar.

## Tests

```sh
dotnet test                 # every backend test project; Docker must be running
cd frontend && npm test     # Vitest + Testing Library
```

- **Backend:** xUnit and NSubstitute. Infrastructure and API tests run against a real Mongo started by Testcontainers.
- **Frontend:** the task form, the tasks hook and the session refresh.
- **Development process:** test-first throughout, and the git history shows every red, green and refactor commit.

### Coverage

```sh
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

- **Output:** each test project writes a Cobertura file to `TestResults/<run-id>/coverage.cobertura.xml`.
- **Reading it:** each file covers every assembly that project loads, so open one in your IDE's coverage viewer, or merge them with a tool such as ReportGenerator.
- **CI:** every run uploads the files as the `backend-coverage` artifact.
- **Frontend:** there's no frontend coverage yet; it needs `@vitest/coverage-v8`.

## More

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): the layers, and the main decisions with their trade-offs
- [docs/PROMPTS.md](docs/PROMPTS.md): how this was built with an AI assistant, prompt by prompt
