# A Responsible AI-Assisted Programming Workflow

Generative AI can accelerate programming, but a student remains responsible for understanding and verifying the result. A reliable workflow has six stages: define the goal, provide relevant context, request a small change, inspect the diff, run tests, and document the decision.

Begin with a concrete specification. State the expected behavior, constraints, examples, and what must remain unchanged. Give the AI only the code and data needed for the task. Do not paste passwords, private student data, proprietary source code, or unrelated files into a hosted model.

Ask for one reviewable change at a time. After generation, read the code instead of assuming that plausible-looking output is correct. Check data boundaries, error handling, privacy, and accessibility. Run automated tests and also exercise the actual user workflow. A test passing only proves the behavior covered by that test.

When a test fails, preserve the exact error message and reduce the problem to the smallest reproducible example. Compare expected and actual behavior, form a hypothesis, and change one variable at a time. Asking an AI to explain evidence is more reliable than repeatedly requesting random rewrites.

Finish by recording what changed, why it changed, how it was tested, and any known limitations. Git commits should be focused and descriptive. Documentation should enable another student to run the project without access to the original chat conversation.
