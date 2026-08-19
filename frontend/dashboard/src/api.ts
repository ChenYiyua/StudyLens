import type {
  AiCatalog,
  CourseCatalog,
  CourseLearningPath,
  CourseStatus,
  CourseSearchResponse,
  CourseChunkDetail,
  ExplainResponse,
  GradeResponse,
  PracticeResponse,
  StudyHistory,
  DeleteStudyHistoryResponse,
} from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`)
  if (!response.ok) {
    throw new Error(`API request failed with status ${response.status}`)
  }
  return response.json() as Promise<T>
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; error?: string } | null
    throw new Error(problem?.detail ?? problem?.error ?? `API request failed with status ${response.status}`)
  }
  return response.json() as Promise<T>
}

async function deleteJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, { method: 'DELETE' })
  if (!response.ok) {
    throw new Error(`API request failed with status ${response.status}`)
  }
  return response.json() as Promise<T>
}

export function fetchCourses() {
  return getJson<CourseCatalog>('/api/courses')
}

export function fetchAiStatus() {
  return getJson<AiCatalog>('/api/ai/status')
}

export function fetchLearningPath(courseId: string) {
  return getJson<CourseLearningPath>(
    `/api/courses/${encodeURIComponent(courseId)}/learning-path`,
  )
}

export async function importCourse(courseName: string, files: File[]) {
  const body = new FormData()
  body.append('courseName', courseName)
  files.forEach((file) => {
    body.append('files', file)
    body.append('relativePaths', file.webkitRelativePath || file.name)
  })
  const response = await fetch(`${API_BASE_URL}/api/courses/import`, {
    method: 'POST',
    body,
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; error?: string } | null
    throw new Error(problem?.detail ?? problem?.error ?? `Course import failed with status ${response.status}`)
  }
  return response.json() as Promise<CourseStatus>
}

export function searchCourse(courseId: string, query: string, materialType = 'all') {
  const parameters = new URLSearchParams({ query, limit: '6' })
  if (materialType !== 'all') parameters.set('materialType', materialType)
  return getJson<CourseSearchResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/search?${parameters.toString()}`,
  )
}

export function fetchCourseChunk(courseId: string, chunkId: string) {
  return getJson<CourseChunkDetail>(
    `/api/courses/${encodeURIComponent(courseId)}/chunks/${encodeURIComponent(chunkId)}`,
  )
}

export function explainCourse(courseId: string, question: string, modelId: string) {
  return postJson<ExplainResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/tutor/explain`,
    { question, language: 'bilingual', modelId },
  )
}

export function explainLecture(courseId: string, documentId: string, modelId: string) {
  return postJson<ExplainResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/tutor/lecture`,
    { documentId, language: 'bilingual', modelId },
  )
}

export function explainExercise(
  courseId: string,
  exerciseDocumentId: string | null,
  solutionDocumentId: string | null,
  modelId: string,
) {
  return postJson<ExplainResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/tutor/exercise`,
    { exerciseDocumentId, solutionDocumentId, language: 'bilingual', modelId },
  )
}

export function createPractice(courseId: string, topic: string, modelId: string, documentId?: string) {
  return postJson<PracticeResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/tutor/practice`,
    { topic, difficulty: 'exam', questionCount: 3, modelId, documentId },
  )
}

export function gradeAnswer(
  courseId: string,
  question: string,
  studentAnswer: string,
  modelId: string,
  documentId?: string,
) {
  return postJson<GradeResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/tutor/grade`,
    { question, studentAnswer, modelId, documentId },
  )
}

export function fetchStudyHistory(courseId: string, limit = 20) {
  return getJson<StudyHistory>(
    `/api/courses/${encodeURIComponent(courseId)}/history?limit=${limit}`,
  )
}

export function clearStudyHistory(courseId: string) {
  return deleteJson<DeleteStudyHistoryResponse>(
    `/api/courses/${encodeURIComponent(courseId)}/history`,
  )
}
