import { useEffect, useState, type FormEvent } from 'react'
import './App.css'

type Provider = 'ChatGpt' | 'Claude' | 'Gemini' | 'Copilot' | 'Other'
type Activity = 'UnderstandConcept' | 'Practice' | 'Research' | 'Writing' | 'Coding' | 'Other'

interface SessionForm {
  provider: Provider
  activity: Activity
  durationMinutes: number
  interactionCount: number
  promptWordCount: number
  helpfulnessRating: number
}

const API_BASE_URL = import.meta.env.DEV ? '' : 'http://127.0.0.1:5080'

const initialForm: SessionForm = {
  provider: 'Other',
  activity: 'UnderstandConcept',
  durationMinutes: 20,
  interactionCount: 3,
  promptWordCount: 0,
  helpfulnessRating: 4,
}

function App() {
  const [participantId, setParticipantId] = useState('')
  const [form, setForm] = useState<SessionForm>(initialForm)
  const [status, setStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    async function initializePopup() {
      const id = await getOrCreateParticipantId()
      setParticipantId(id)

      const currentUrl = await getCurrentTabUrl()
      const detectedProvider = detectProvider(currentUrl)
      if (detectedProvider !== 'Other') {
        setForm((current) => ({ ...current, provider: detectedProvider }))
      }
    }

    void initializePopup()
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    setStatus('saving')

    try {
      const response = await fetch(`${API_BASE_URL}/api/events`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          participantId,
          ...form,
          startedAtUtc: new Date().toISOString(),
        }),
      })

      if (!response.ok) throw new Error(`Request failed: ${response.status}`)
      setStatus('saved')
    } catch {
      setStatus('error')
    }
  }

  return (
    <main className="popup-shell">
      <header>
        <div className="brand-mark">SL</div>
        <div>
          <strong>StudyLens</strong>
          <span>Private learning reflection</span>
        </div>
      </header>

      <div className="privacy-note">
        <span>✓</span>
        <p><strong>Your conversation stays private.</strong><br />Only the metadata below is sent.</p>
      </div>

      <form onSubmit={submit}>
        <div className="field-grid">
          <label>
            AI tool
            <select
              value={form.provider}
              onChange={(event) => setForm({ ...form, provider: event.target.value as Provider })}
            >
              <option value="ChatGpt">ChatGPT</option>
              <option value="Claude">Claude</option>
              <option value="Gemini">Gemini</option>
              <option value="Copilot">Copilot</option>
              <option value="Other">Other</option>
            </select>
          </label>
          <label>
            Learning goal
            <select
              value={form.activity}
              onChange={(event) => setForm({ ...form, activity: event.target.value as Activity })}
            >
              <option value="UnderstandConcept">Understand a concept</option>
              <option value="Practice">Practice</option>
              <option value="Research">Research</option>
              <option value="Writing">Writing</option>
              <option value="Coding">Coding</option>
              <option value="Other">Other</option>
            </select>
          </label>
          <label>
            Minutes
            <input
              type="number"
              min="1"
              max="480"
              value={form.durationMinutes}
              onChange={(event) => setForm({ ...form, durationMinutes: Number(event.target.value) })}
            />
          </label>
          <label>
            Interactions
            <input
              type="number"
              min="1"
              max="100"
              value={form.interactionCount}
              onChange={(event) => setForm({ ...form, interactionCount: Number(event.target.value) })}
            />
          </label>
        </div>

        <label>
          Approximate words you typed
          <input
            type="number"
            min="0"
            max="5000"
            value={form.promptWordCount}
            onChange={(event) => setForm({ ...form, promptWordCount: Number(event.target.value) })}
          />
          <small>Only the number is recorded, never the text.</small>
        </label>

        <fieldset>
          <legend>How helpful was this session?</legend>
          <div className="rating-options">
            {[1, 2, 3, 4, 5].map((rating) => (
              <button
                className={form.helpfulnessRating === rating ? 'selected' : ''}
                key={rating}
                type="button"
                onClick={() => setForm({ ...form, helpfulnessRating: rating })}
                aria-label={`${rating} out of 5`}
              >
                {rating}
              </button>
            ))}
          </div>
        </fieldset>

        <button className="save-button" type="submit" disabled={!participantId || status === 'saving'}>
          {status === 'saving' ? 'Saving…' : status === 'saved' ? 'Saved ✓' : 'Save reflection'}
        </button>

        <button
          className="dashboard-button"
          type="button"
          disabled={!participantId}
          onClick={() => openDashboard(participantId)}
        >
          Open my dashboard ↗
        </button>

        {status === 'error' && (
          <p className="error-message">Could not reach the local API on port 5080.</p>
        )}
      </form>
    </main>
  )
}

async function getOrCreateParticipantId() {
  if (typeof chrome !== 'undefined' && chrome.storage?.local) {
    const stored = await chrome.storage.local.get('participantId')
    if (typeof stored.participantId === 'string') return stored.participantId

    const id = `student-${crypto.randomUUID()}`
    await chrome.storage.local.set({ participantId: id })
    return id
  }

  const existing = localStorage.getItem('participantId')
  if (existing) return existing
  const id = `student-${crypto.randomUUID()}`
  localStorage.setItem('participantId', id)
  return id
}

async function getCurrentTabUrl() {
  if (typeof chrome === 'undefined' || !chrome.tabs?.query) return window.location.href
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true })
  return tab?.url ?? ''
}

function detectProvider(url: string): Provider {
  if (url.includes('chatgpt.com') || url.includes('chat.openai.com')) return 'ChatGpt'
  if (url.includes('claude.ai')) return 'Claude'
  if (url.includes('gemini.google.com')) return 'Gemini'
  if (url.includes('copilot.microsoft.com')) return 'Copilot'
  return 'Other'
}

function openDashboard(participantId: string) {
  const url = `http://localhost:5173/?participantId=${encodeURIComponent(participantId)}`
  if (typeof chrome !== 'undefined' && chrome.tabs?.create) {
    void chrome.tabs.create({ url })
    return
  }
  window.open(url, '_blank', 'noopener,noreferrer')
}

export default App
