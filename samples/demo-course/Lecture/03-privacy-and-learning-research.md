# Privacy-Aware Learning Research

Learning tools can collect valuable evidence about where students struggle, but collection must be purposeful and transparent. Define the research question before collecting data. Store the minimum fields needed to answer it, explain the purpose to participants, and avoid using raw personal identifiers when aggregated or pseudonymous data is sufficient.

StudyLens distinguishes course evidence, interaction metadata, and student-authored content. Course evidence supports retrieval. Interaction metadata can include the selected course, workflow type, timestamp, latency, and whether an error occurred. Student-authored answers and generated feedback are more sensitive and should remain local by default.

A browser extension must not silently scrape an entire page. It should act only after an explicit user gesture, show the text that will be transferred, and let the user edit or cancel it. The extension should send the minimum selected excerpt rather than browsing history, cookies, or page contents unrelated to the learning task.

For evaluation, report aggregate measures such as retrieval success, completion rate, response latency, and voluntarily submitted usefulness feedback. Qualitative feedback explains why a workflow succeeded or failed. Researchers should document exclusions, missing data, and known measurement limitations instead of presenting a dashboard metric as objective truth.
