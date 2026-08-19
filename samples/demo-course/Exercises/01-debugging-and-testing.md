# Exercise: Debugging AI-Generated Code

## Scenario

An AI assistant changed a course dashboard so that a score is displayed as a percentage. The automated test expects `80%`, but the interface displays `0.8%`.

## Tasks

1. Write down the expected behavior and the observed behavior.
2. Identify the smallest input that reproduces the defect.
3. Decide whether the stored value represents a fraction or an already-normalized percentage.
4. Add a regression test covering zero, a typical value, and the upper boundary.
5. Make the smallest code change that fixes the defect without changing storage semantics.
6. Document the cause and the evidence used to verify the fix.

## Guidance

Do not ask the AI for repeated rewrites without inspecting the data flow. Trace the value from API response to TypeScript type, transformation, and rendered component. A useful debugging explanation distinguishes the symptom from the root cause. The failing test is evidence, while the model's suggestion is only a hypothesis until the test and real interface both pass.
