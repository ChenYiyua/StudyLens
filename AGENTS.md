# StudyLens project guide

## Purpose

StudyLens is a multi-course, source-grounded AI tutor. It retrieves evidence from local course material, preserves page-level citations, and uses a replaceable local model for teaching, practice generation, and formative feedback.

## Architecture

- `tools/build_course_index.py`: local PDF extraction and chunking pipeline.
- `backend/StudyLens.Api`: ASP.NET Core 10 multi-course retrieval and tutor API.
- `backend/StudyLens.Api.Tests`: unit and HTTP integration tests.
- `frontend/dashboard`: React/TypeScript Explain, Practice, and Feedback workspace.
- `frontend/extension`: explicit-selection Chrome/Edge extension; never collect passive browsing data.
- MongoDB database `studylens`, collection `study_attempts`: course-scoped answer history.
- `samples/demo-course`: public, reproducible materials used by a clean clone and retrieval evaluation.
- `docs/interview-notes.zh-CN.md`: bilingual interview explanation.

## Verification

Run `powershell -ExecutionPolicy Bypass -File scripts/verify.ps1` from the repository root.

## Engineering boundaries

- Never commit course PDFs, extracted course text, API keys, student answers, or generated feedback.
- MongoDB is the production/default repository. `LocalJsonStudyAttemptRepository` exists only as an explicit fallback and deterministic test adapter.
- Every course-grounded claim shown to a student must retain its source filename and page number.
- Keep PDF extraction local. Any AI provider may receive only the retrieved excerpts needed for one request.
- Treat generated teaching and grading as assistance, not an authoritative course solution.
- Keep JSON deserialization strict and add tests whenever retrieval or privacy behavior changes.
