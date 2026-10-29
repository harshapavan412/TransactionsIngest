# AI Usage Disclosure

**AI tools were used:** yes. I used **Claude** (Anthropic), through the Claude app, as a pair-programming assistant for this exercise.

## How it was used

I worked through the exercise with the assistant one step at a time instead of generating the whole solution at once:

1. **Understanding the requirements.** I asked it to explain the exercise in plain terms (snapshots, reconciliation, revocation window, idempotency) and to propose an order of work.
2. **Setup.** It gave me the commands to install the .NET 10 SDK, create the solution, Web API and xUnit projects, add EF Core, and initialize Git. I ran each command myself and confirmed the result before moving on.
3. **Backend.** For each file (domain model, DbContext, migrations, mock sources, import service, controllers), the assistant proposed the code and explained the reasoning. I created the files, built after each step, and committed after each completed piece.
4. **Verification.** I tested the API manually from PowerShell (first import, re-import for idempotency, editing source files to trigger update and revoke, checking the audit history) before writing automated tests.
5. **Tests.** The assistant wrote the xUnit tests; I ran them locally (15 passed).
6. **Frontend.** The assistant generated the Angular client, checked that it built, and ran its tests in its own environment. I added it to the repository, installed it, and verified it against my running API.
7. **Documentation.** The assistant drafted this README and these disclosure files from our discussion; I reviewed them.

## What I verified, changed or decided

- Ran every build, migration, test run and manual API check on my machine; the results are reflected in the commit history.
- **Caught a misunderstanding during verification:** after my first edit-and-reimport test showed `updated: 0`, I traced it to a source file edit that had not actually been made, not a bug, then confirmed the update and field-level audit worked (`Amount 24.99 → 19.99`).
- Questioned unclear naming (`IImportService` vs `ImportService`) while building.
- Agreed the trade-offs the assistant proposed: VS Code instead of Visual Studio, and a single API project with clear folders instead of multiple class libraries.
- Raised the concern that the code might read as AI-generated. As a result, later files keep comments to explaining *why* rather than restating the code.
- Reviewed the final code, README and these notes.

## Files in this folder

| File | Contents |
|---|---|
| [prompts.md](prompts.md) | The prompts and instructions I gave, in order |
| [conventions.md](conventions.md) | Coding conventions and rules followed |
| [design-discussion.md](design-discussion.md) | Design questions discussed and the decisions made |
