# StudyLens project guide

## Purpose

StudyLens is a privacy-aware AI-learning reflection prototype built for an AI-for-Education HiWi application. It records only user-approved metadata and must never collect raw prompts, model responses, page content, names, email addresses or university identifiers.

## Architecture

- `backend/StudyLens.Api`: ASP.NET Core 10 API and MongoDB/in-memory repositories.
- `backend/StudyLens.Api.Tests`: unit, HTTP integration and opt-in MongoDB integration tests.
- `frontend/dashboard`: React/TypeScript reflection dashboard.
- `frontend/extension`: Manifest V3 browser-extension popup.
- `docs/interview-notes.zh-CN.md`: bilingual interview explanation.

## Verification

Run `powershell -ExecutionPolicy Bypass -File scripts/verify.ps1` from the repository root. MongoDB integration coverage runs when `RUN_MONGODB_INTEGRATION_TESTS=true` and a server is available at `127.0.0.1:27017`.

## Engineering boundaries

- Keep JSON deserialization strict so undeclared content fields are rejected.
- Preserve participant isolation in every read, export and delete path.
- Never commit connection strings, participant data or generated exports.
- Treat the participant ID as a prototype identifier, not authentication.
- Add tests whenever the privacy boundary or repository behavior changes.
