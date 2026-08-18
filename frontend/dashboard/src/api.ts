import type { Insights, LearningEvent } from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

async function getJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init)
  if (!response.ok) {
    throw new Error(`API request failed with status ${response.status}`)
  }
  return response.json() as Promise<T>
}

export function fetchInsights(participantId: string) {
  return getJson<Insights>(`/api/insights?participantId=${encodeURIComponent(participantId)}`)
}

export function fetchEvents(participantId: string) {
  return getJson<LearningEvent[]>(`/api/events?participantId=${encodeURIComponent(participantId)}`)
}

export function seedDemoData(participantId: string) {
  return getJson<{ inserted: number }>(
    `/api/demo/seed?participantId=${encodeURIComponent(participantId)}`,
    { method: 'POST' },
  )
}
