# Grounded AI Tutoring

## Learning objective

A grounded tutor separates retrieval from generation. Retrieval finds a small set of relevant passages in approved course materials. Generation then explains those passages in language appropriate for the learner. The model does not become the source of truth: the course material remains the source of truth.

Every course-specific claim should retain an auditable citation containing the source title and page or section. Citations help a learner check whether an explanation matches the material. They do not guarantee that the model interpreted the evidence correctly, so the interface should make the original source easy to reopen.

The tutor should say when the retrieved evidence is insufficient. Inventing a page number, marking criterion, or course fact is worse than returning an explicit limitation. This is why StudyLens creates citation objects from its own index instead of asking the language model to invent source metadata.

## Retrieval before generation

For each request, the search component ranks local chunks and sends only the most relevant evidence to the model. This reduces irrelevant context, limits disclosure of course material, and makes retrieval quality testable independently from model quality. A useful evaluation set contains representative student questions and the document that should appear near the top of the results.

## Formative feedback

AI feedback should be labelled formative rather than official. A formative score helps a student identify strengths and missing points, but it must not claim to reproduce a lecturer's marking decision. Good feedback names concrete omissions and offers an improved answer that remains linked to course evidence.
