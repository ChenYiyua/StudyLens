# StudyLens project handoff

Use this file when opening the repository on another computer or in a new Codex task.

## Current state

- Product: multi-course, source-grounded AI tutor with Lecture teaching, course Exercise/Solution walkthrough, generated Knowledge Check, Feedback, and explicit-selection browser extension workflows.
- Backend: ASP.NET Core 10 with strict JSON input, course isolation, safe source access, retrieval, and replaceable AI/persistence interfaces.
- Frontends: React/TypeScript dashboard and Manifest V3 Chrome/Edge extension.
- Dashboard UI: responsive dark enterprise theme with glass panels, blue-violet lighting, CSS particle motion, staged entrance/hover animations, and reduced-motion accessibility.
- AI: Ollama at `http://127.0.0.1:11434`; portable default `qwen3.5:4b`.
- Persistence: MongoDB default at `mongodb://127.0.0.1:27017`, database `studylens`, collection `study_attempts`.
- Reproducibility: committed original demo materials, generated demo index, and four fixed retrieval cases.
- Visual grounding: cited PDF pages are rendered and cached locally as inline 1200-pixel PNG previews, with links to the original source page.
- Validation baseline: 5 Python tests, 32 C# tests, .NET formatting, dashboard lint/build, extension lint/build.

## First commands on a new Windows computer

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-mongodb.ps1 -Install
powershell -ExecutionPolicy Bypass -File scripts\setup-local-ai.ps1
powershell -ExecutionPolicy Bypass -File scripts\verify.ps1
powershell -ExecutionPolicy Bypass -File scripts\run-local.ps1
```

The public demo should run without private course files. Private PDFs must be copied separately and registered again with `scripts/setup-course.ps1`.

## Local-only state that Git must not contain

- `backend/StudyLens.Api/appsettings.Local.json`
- `backend/StudyLens.Api/App_Data/*.json`
- private course PDFs and extracted text
- Ollama model weights
- MongoDB student attempts
- API keys or `.env` files

## Verified local reference deployment

- EAM: 52 PDFs, 926 pages, 967 chunks.
- Qwen3.5 4B responds on the current 16 GB machine.
- Qwen3.5 9B is downloaded but cannot load on the current machine; evaluate it on the RTX 3060 laptop.
- MongoDB Windows service is running and the application health endpoint reports connected.
- A real EAM answer was graded, persisted, reloaded, and displayed with six source citations.
- A 7,093-character EAM lecture lesson completed without a cut-off and displayed three 1200x900 cited-page previews without browser errors.

## Safe next backlog

1. Add measurement for response latency and model comparison.
2. Expand retrieval benchmark coverage before changing retrieval algorithms.
3. Add authenticated/pseudonymous multi-user support only after defining consent and retention.
4. Add selective OCR and hybrid retrieval as evaluated changes.

Read `AGENTS.md`, `README.md`, and `docs/interview-notes.zh-CN.md` before making architectural changes. Preserve the existing dirty worktree until the user reviews the final Git scope.
