export const maximumSelectionLength = 300

export interface StudyLensHandoff {
  courseId: string
  question: string
  source: string
}

export function normaliseSelection(value: string) {
  return value.trim().slice(0, maximumSelectionLength)
}

export function createStudyLensUrl(baseUrl: string, handoff: StudyLensHandoff) {
  const fragment = new URLSearchParams({
    course: handoff.courseId,
    question: normaliseSelection(handoff.question),
    source: handoff.source.trim() || 'Current page',
    from: 'extension',
  })
  return `${baseUrl.replace(/\/$/, '')}/#${fragment.toString()}`
}
