import { useEffect, useMemo, useState } from 'react'
import './App.css'
import {
  createStudyLensUrl,
  maximumSelectionLength,
  normaliseSelection,
  pendingSelectionKey,
  readPendingSelection,
} from './handoff'

interface CourseStatus {
  courseId: string
  courseName: string | null
  ready: boolean
}

interface CourseCatalog {
  defaultCourseId: string
  courses: CourseStatus[]
}

interface PageSelection {
  text: string
  title: string
}

const API_URL = 'http://127.0.0.1:5080'
function App() {
  const [courses, setCourses] = useState<CourseStatus[]>([])
  const [courseId, setCourseId] = useState('')
  const [selection, setSelection] = useState('')
  const [sourceTitle, setSourceTitle] = useState('Current page')
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [message, setMessage] = useState('Reading the text you selected…')

  useEffect(() => {
    async function initialise() {
      try {
        const [catalog, pageSelection] = await Promise.all([
          fetch(`${API_URL}/api/courses`).then(async (response) => {
            if (!response.ok) throw new Error('StudyLens API is unavailable.')
            return response.json() as Promise<CourseCatalog>
          }),
          readPageSelection(),
        ])
        const readyCourses = catalog.courses.filter((course) => course.ready)
        setCourses(readyCourses)
        setCourseId(
          readyCourses.some((course) => course.courseId === catalog.defaultCourseId)
            ? catalog.defaultCourseId
            : readyCourses[0]?.courseId ?? '',
        )
        setSelection(normaliseSelection(pageSelection.text))
        setSourceTitle(pageSelection.title || 'Current page')
        setStatus('ready')
        setMessage(pageSelection.text
          ? 'Review or edit the selected text before opening StudyLens.'
          : 'No text is selected. Select text on the page, then reopen this extension.')
      } catch (error) {
        setStatus('error')
        setMessage(error instanceof Error ? error.message : 'StudyLens could not initialise.')
      }
    }

    void initialise()
  }, [])

  const canOpen = useMemo(
    () => status === 'ready' && courseId.length > 0 && selection.trim().length >= 2,
    [courseId, selection, status],
  )

  function openStudyLens() {
    if (!canOpen) return
    const url = createStudyLensUrl(API_URL, {
      courseId,
      question: selection,
      source: sourceTitle,
    })
    if (typeof chrome !== 'undefined' && chrome.tabs?.create) {
      void chrome.tabs.create({ url })
    } else {
      window.open(url, '_blank', 'noopener,noreferrer')
    }
  }

  return <main className="popup-shell">
    <header>
      <div className="brand-mark">S</div>
      <div><strong>StudyLens Bridge</strong><span>Selected text → course-grounded explanation</span></div>
    </header>

    <section className="privacy-card">
      <span>✓</span>
      <p><strong>You stay in control</strong>Only the text shown below is transferred. StudyLens never reads cookies, browsing history, or the rest of the page.</p>
    </section>

    <label>
      Course
      <select value={courseId} onChange={(event) => setCourseId(event.target.value)}>
        {courses.map((course) => <option value={course.courseId} key={course.courseId}>{course.courseName ?? course.courseId}</option>)}
      </select>
    </label>

    <label>
      Selected text
      <textarea
        aria-label="Selected text"
        maxLength={maximumSelectionLength}
        placeholder="Select a concept, paragraph, or error message on the current page."
        value={selection}
        onChange={(event) => {
          setSelection(event.target.value)
          if (status === 'ready') {
            setMessage(event.target.value.trim()
              ? 'Review or edit this text before opening StudyLens.'
              : 'No text is selected. Select text on the page, then reopen this extension.')
          }
        }}
      />
      <small>{selection.length}/{maximumSelectionLength} characters · source: {sourceTitle}</small>
    </label>

    <p className={`status-message ${status}`}>{message}</p>
    <button className="open-button" disabled={!canOpen} onClick={openStudyLens}><span>Explain with course evidence</span><b>↗</b></button>
    <p className="transfer-note"><i /> Local handoff · the fragment is removed as soon as StudyLens opens.</p>
  </main>
}

async function readPageSelection(): Promise<PageSelection> {
  const pendingSelection = await readContextMenuSelection()
  if (pendingSelection) return pendingSelection

  if (typeof chrome === 'undefined' || !chrome.tabs?.query || !chrome.scripting?.executeScript) {
    return { text: '', title: document.title }
  }

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true })
  if (tab?.id == null) return { text: '', title: tab?.title ?? 'Current page' }

  try {
    const [result] = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: () => window.getSelection()?.toString() ?? '',
    })
    return { text: typeof result?.result === 'string' ? result.result : '', title: tab.title ?? 'Current page' }
  } catch {
    return { text: '', title: tab.title ?? 'Restricted browser page' }
  }
}

async function readContextMenuSelection(): Promise<PageSelection | null> {
  if (typeof chrome === 'undefined' || !chrome.storage?.session) return null
  try {
    const stored = await chrome.storage.session.get(pendingSelectionKey)
    await chrome.storage.session.remove(pendingSelectionKey)
    const pending = readPendingSelection(stored[pendingSelectionKey])
    return pending ? { text: pending.text, title: pending.title } : null
  } catch {
    return null
  }
}

export default App
