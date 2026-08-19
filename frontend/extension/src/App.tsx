import { useEffect, useMemo, useState } from 'react'
import './App.css'

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
const maximumSelectionLength = 300

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
        setSelection(pageSelection.text.slice(0, maximumSelectionLength))
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
    const fragment = new URLSearchParams({
      course: courseId,
      question: selection.trim(),
      source: sourceTitle,
      from: 'extension',
    })
    const url = `${API_URL}/#${fragment.toString()}`
    if (typeof chrome !== 'undefined' && chrome.tabs?.create) {
      void chrome.tabs.create({ url })
    } else {
      window.open(url, '_blank', 'noopener,noreferrer')
    }
  }

  return <main className="popup-shell">
    <header>
      <div className="brand-mark">S</div>
      <div><strong>StudyLens</strong><span>Selected text → grounded course tutor</span></div>
    </header>

    <section className="privacy-card">
      <span>✓</span>
      <p><strong>Explicit selection only</strong>This extension reads only the text visible below after you click it. It never reads cookies, browsing history, or the rest of the page.</p>
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
        onChange={(event) => setSelection(event.target.value)}
      />
      <small>{selection.length}/{maximumSelectionLength} characters · source: {sourceTitle}</small>
    </label>

    <p className={`status-message ${status}`}>{message}</p>
    <button className="open-button" disabled={!canOpen} onClick={openStudyLens}>Open in StudyLens ↗</button>
    <p className="transfer-note">The text is placed in a local URL fragment, so it is not sent to a remote server.</p>
  </main>
}

async function readPageSelection(): Promise<PageSelection> {
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

export default App
