export interface ExtensionHandoff {
  courseId: string
  question: string
  source: string
}

const maximumQuestionLength = 300
const maximumSourceLength = 160

export function parseExtensionHandoff(
  hash: string,
  availableCourseIds: readonly string[],
): ExtensionHandoff | null {
  const parameters = new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash)
  if (parameters.get('from') !== 'extension') return null

  const question = (parameters.get('question') ?? '').trim().slice(0, maximumQuestionLength)
  if (question.length < 2) return null

  const requestedCourseId = (parameters.get('course') ?? '').trim()
  const courseId = availableCourseIds.includes(requestedCourseId) ? requestedCourseId : ''
  const source = (parameters.get('source') ?? '').trim().slice(0, maximumSourceLength)

  return {
    courseId,
    question,
    source: source || 'Current page',
  }
}

export function addressWithoutHandoff(location: Pick<Location, 'pathname' | 'search'>) {
  return `${location.pathname}${location.search}`
}
