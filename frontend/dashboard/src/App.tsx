import { useCallback, useEffect, useMemo, useState } from 'react'
import './App.css'
import {
  deleteLearningData,
  exportLearningData,
  fetchEvents,
  fetchInsights,
  seedDemoData,
} from './api'
import type { DimensionCount, Insights, LearningEvent } from './types'

const requestedParticipantId = new URLSearchParams(window.location.search).get('participantId')
const participantId = requestedParticipantId && requestedParticipantId.length >= 8
  ? requestedParticipantId
  : 'demo-student'

function BarList({ items }: { items: DimensionCount[] }) {
  const max = Math.max(...items.map((item) => item.count), 1)

  if (items.length === 0) {
    return <p className="empty-copy">No sessions yet.</p>
  }

  return (
    <div className="bar-list">
      {items.map((item) => (
        <div className="bar-row" key={item.label}>
          <div className="bar-label">
            <span>{displayLabel(item.label)}</span>
            <strong>{item.count}</strong>
          </div>
          <div className="bar-track" aria-hidden="true">
            <div className="bar-fill" style={{ width: `${(item.count / max) * 100}%` }} />
          </div>
        </div>
      ))}
    </div>
  )
}

function App() {
  const [insights, setInsights] = useState<Insights | null>(null)
  const [events, setEvents] = useState<LearningEvent[]>([])
  const [loading, setLoading] = useState(true)
  const [seeding, setSeeding] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [nextInsights, nextEvents] = await Promise.all([
        fetchInsights(participantId),
        fetchEvents(participantId),
      ])
      setInsights(nextInsights)
      setEvents(nextEvents)
    } catch {
      setError('The API is not reachable. Start the ASP.NET Core server on port 5080.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  const maxDailyMinutes = useMemo(
    () => Math.max(...(insights?.dailyUsage.map((day) => day.minutes) ?? []), 1),
    [insights],
  )

  async function seed() {
    setSeeding(true)
    setError('')
    try {
      await seedDemoData(participantId)
      await load()
    } catch {
      setError('Demo data could not be created. Check that the API is running.')
    } finally {
      setSeeding(false)
    }
  }

  async function downloadExport() {
    setError('')
    setNotice('')
    try {
      const exportData = await exportLearningData(participantId)
      const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `studylens-${participantId}.json`
      link.click()
      URL.revokeObjectURL(url)
      setNotice(`Exported ${exportData.events.length} reflection${exportData.events.length === 1 ? '' : 's'}.`)
    } catch {
      setError('Your data could not be exported. Check that the API is running.')
    }
  }

  async function deleteAllData() {
    const confirmed = window.confirm(
      'Delete every StudyLens reflection for this participant? This cannot be undone.',
    )
    if (!confirmed) return

    setDeleting(true)
    setError('')
    setNotice('')
    try {
      const result = await deleteLearningData(participantId)
      await load()
      setNotice(`Deleted ${result.deleted} reflection${result.deleted === 1 ? '' : 's'}.`)
    } catch {
      setError('Your data could not be deleted. Check that the API is running.')
    } finally {
      setDeleting(false)
    }
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="#top" aria-label="StudyLens home">
          <span className="brand-mark">SL</span>
          <span>StudyLens</span>
        </a>
        <div className="participant-pill">
          <span className="status-dot" /> {participantId === 'demo-student' ? 'Demo participant' : 'Anonymous participant'}
        </div>
      </header>

      <section className="hero" id="top">
        <div>
          <p className="eyebrow">AI learning reflection</p>
          <h1>See the learning pattern,<br />not the private conversation.</h1>
          <p className="hero-copy">
            StudyLens turns opt-in, content-free interaction metadata into a useful weekly reflection.
          </p>
        </div>
        <div className="privacy-card">
          <div className="shield">✓</div>
          <div>
            <strong>Raw prompts stay private</strong>
            <p>Only counts, duration, activity and a usefulness rating reach the API.</p>
          </div>
        </div>
      </section>

      {error && <div className="error-banner" role="alert">{error}</div>}
      {notice && <div className="notice-banner" role="status">{notice}</div>}

      <section className="section-heading">
        <div>
          <p className="eyebrow">This week</p>
          <h2>Your learning snapshot</h2>
        </div>
        <div className="data-actions">
          <button className="secondary-button" onClick={() => void downloadExport()}>
            Export my data
          </button>
          <button
            className="danger-button"
            onClick={() => void deleteAllData()}
            disabled={deleting || events.length === 0}
          >
            {deleting ? 'Deleting…' : 'Delete my data'}
          </button>
          <button className="primary-button" onClick={seed} disabled={seeding}>
            {seeding ? 'Creating…' : 'Add demo data'}
          </button>
        </div>
      </section>

      <section className="kpi-grid" aria-busy={loading}>
        <article className="kpi-card accent-blue">
          <span>Sessions</span>
          <strong>{loading ? '–' : (insights?.totalSessions ?? 0)}</strong>
          <small>AI-assisted learning moments</small>
        </article>
        <article className="kpi-card accent-coral">
          <span>Focused time</span>
          <strong>{loading ? '–' : `${insights?.totalMinutes ?? 0}m`}</strong>
          <small>Self-reported duration</small>
        </article>
        <article className="kpi-card accent-lime">
          <span>Helpfulness</span>
          <strong>{loading ? '–' : `${insights?.averageHelpfulness ?? 0}/5`}</strong>
          <small>Average reflection rating</small>
        </article>
      </section>

      <section className="dashboard-grid">
        <article className="panel usage-panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Consistency</p>
              <h3>Daily focused minutes</h3>
            </div>
          </div>
          <div className="daily-chart">
            {(insights?.dailyUsage ?? []).map((day) => (
              <div className="day-column" key={day.date}>
                <span className="day-value">{day.minutes}</span>
                <div className="day-track">
                  <div
                    className="day-bar"
                    style={{ height: `${Math.max((day.minutes / maxDailyMinutes) * 100, 8)}%` }}
                  />
                </div>
                <span>{formatDay(day.date)}</span>
              </div>
            ))}
            {!loading && (insights?.dailyUsage.length ?? 0) === 0 && (
              <p className="empty-copy">Add demo data or record a session from the extension.</p>
            )}
          </div>
        </article>

        <article className="panel">
          <p className="eyebrow">Tools</p>
          <h3>Sessions by provider</h3>
          <BarList items={insights?.byProvider ?? []} />
        </article>

        <article className="panel">
          <p className="eyebrow">Intent</p>
          <h3>Learning activities</h3>
          <BarList items={insights?.byActivity ?? []} />
        </article>

        <article className="panel recent-panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Latest entries</p>
              <h3>Recent reflections</h3>
            </div>
            <span className="content-free-badge">Content-free</span>
          </div>
          <div className="event-list">
            {events.slice(0, 5).map((event) => (
              <div className="event-row" key={event.id}>
                <div className="provider-icon">{event.provider.slice(0, 1)}</div>
                <div>
                  <strong>{displayLabel(event.activity)}</strong>
                  <span>{displayLabel(event.provider)} · {event.durationMinutes} min</span>
                </div>
                <div className="rating" aria-label={`${event.helpfulnessRating} out of 5 helpful`}>
                  {'●'.repeat(event.helpfulnessRating)}{'○'.repeat(5 - event.helpfulnessRating)}
                </div>
              </div>
            ))}
            {!loading && events.length === 0 && <p className="empty-copy">No reflections recorded yet.</p>}
          </div>
        </article>
      </section>

      <footer>
        <span>StudyLens prototype</span>
        <span>No raw prompts. No response text. Opt-in only.</span>
      </footer>
    </main>
  )
}

function displayLabel(value: string) {
  if (value === 'ChatGpt') return 'ChatGPT'
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatDay(value: string) {
  return new Intl.DateTimeFormat('en', { weekday: 'short' }).format(new Date(`${value}T12:00:00Z`))
}

export default App
