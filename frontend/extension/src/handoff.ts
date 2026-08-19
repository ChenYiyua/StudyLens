export const maximumSelectionLength = 300
export const pendingSelectionKey = 'studylensPendingSelection'
export const pendingSelectionMaxAgeMs = 2 * 60 * 1000

export interface StudyLensHandoff {
  courseId: string
  question: string
  source: string
}

export interface PendingPageSelection {
  text: string
  title: string
  capturedAt: number
}

export function normaliseSelection(value: string) {
  return value.trim().slice(0, maximumSelectionLength)
}

export function readPendingSelection(value: unknown, now = Date.now()): PendingPageSelection | null {
  if (typeof value !== 'object' || value === null) return null
  const record = value as Record<string, unknown>
  if (typeof record.text !== 'string' || typeof record.title !== 'string' || typeof record.capturedAt !== 'number') {
    return null
  }
  const text = normaliseSelection(record.text)
  if (text.length < 2 || now - record.capturedAt > pendingSelectionMaxAgeMs || record.capturedAt > now) {
    return null
  }
  return { text, title: record.title.trim() || 'Current page', capturedAt: record.capturedAt }
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
