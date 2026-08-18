export interface DimensionCount {
  label: string
  count: number
}

export interface DailyUsage {
  date: string
  sessions: number
  minutes: number
}

export interface Insights {
  totalSessions: number
  totalMinutes: number
  averageHelpfulness: number
  byProvider: DimensionCount[]
  byActivity: DimensionCount[]
  dailyUsage: DailyUsage[]
}

export interface LearningEvent {
  id: string
  participantId: string
  provider: string
  activity: string
  startedAtUtc: string
  durationMinutes: number
  interactionCount: number
  promptWordCount: number
  helpfulnessRating: number
}

export interface LearningDataExport {
  schemaVersion: number
  exportedAtUtc: string
  participantId: string
  events: LearningEvent[]
}
