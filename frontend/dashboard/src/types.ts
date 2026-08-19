export interface CourseStatus {
  courseId: string
  ready: boolean
  sourceAvailable: boolean
  courseName: string | null
  generatedAtUtc: string | null
  documentCount: number
  pageCount: number
  emptyPageCount: number
  chunkCount: number
  skippedDocumentCount: number
  materialTypes: Record<string, number>
  message: string | null
}

export interface CourseCatalog {
  defaultCourseId: string
  courses: CourseStatus[]
}

export interface CourseMaterial {
  documentId: string
  title: string
  relativePath: string
  materialType: string
  pageCount: number
  chunkCount: number
}

export interface CourseExerciseUnit {
  id: string
  title: string
  exercise: CourseMaterial | null
  solution: CourseMaterial | null
}

export interface CourseLearningPath {
  courseId: string
  lectures: CourseMaterial[]
  exercises: CourseExerciseUnit[]
  otherMaterials: CourseMaterial[]
}

export interface CourseSearchResult {
  chunkId: string
  documentId: string
  title: string
  relativePath: string
  materialType: string
  page: number
  excerpt: string
  relevanceScore: number
}

export interface CourseChunkDetail {
  courseId: string
  chunkId: string
  documentId: string
  title: string
  relativePath: string
  materialType: string
  page: number
  text: string
}

export interface CourseSearchResponse {
  courseId: string
  query: string
  resultCount: number
  results: CourseSearchResult[]
}

export interface AiStatus {
  id: string
  available: boolean
  provider: string
  displayName: string
  model: string
  local: boolean
  message: string
}

export interface AiCatalog {
  defaultModelId: string
  models: AiStatus[]
}

export interface TutorCitation {
  number: number
  chunkId: string
  documentId: string
  title: string
  relativePath: string
  materialType: string
  page: number
  excerpt: string
}

export interface ExplainResponse {
  courseId: string
  question: string
  answer: string
  model: string
  citations: TutorCitation[]
}

export interface PracticeQuestion {
  id: string
  question: string
  commandWord: string
  difficulty: string
  maxScore: number
  sourceNumbers: number[]
}

export interface PracticeResponse {
  courseId: string
  topic: string
  model: string
  questions: PracticeQuestion[]
  citations: TutorCitation[]
}

export interface GradeResponse {
  courseId: string
  question: string
  score: number
  maxScore: number
  summary: string
  strengths: string[]
  missingPoints: string[]
  improvedAnswer: string
  model: string
  citations: TutorCitation[]
  attemptId: string | null
  createdAtUtc: string | null
}

export interface StudyAttempt {
  id: string
  courseId: string
  question: string
  studentAnswer: string
  score: number
  maxScore: number
  summary: string
  strengths: string[]
  missingPoints: string[]
  improvedAnswer: string
  model: string
  citations: TutorCitation[]
  createdAtUtc: string
}

export interface StudyHistory {
  courseId: string
  attemptCount: number
  averagePercentage: number | null
  latestAttemptAtUtc: string | null
  attempts: StudyAttempt[]
}

export interface DeleteStudyHistoryResponse {
  courseId: string
  deletedCount: number
}
