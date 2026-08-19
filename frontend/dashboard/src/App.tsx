import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties, FormEvent } from 'react'
import './App.css'
import {
  clearStudyHistory,
  createPractice,
  explainCourse,
  explainExercise,
  explainLecture,
  fetchAiStatus,
  fetchCourses,
  fetchLearningPath,
  fetchStudyHistory,
  gradeAnswer,
  importCourse,
} from './api'
import type {
  AiCatalog,
  AiStatus,
  CourseCatalog,
  CourseExerciseUnit,
  CourseLearningPath,
  CourseMaterial,
  ExplainResponse,
  GradeResponse,
  PracticeQuestion,
  PracticeResponse,
  StudyAttempt,
  StudyHistory,
  TutorCitation,
} from './types'
import { addressWithoutHandoff, parseExtensionHandoff } from './extensionHandoff'
import type { ExtensionHandoff } from './extensionHandoff'

type WorkspaceMode = 'study' | 'feedback'
type WorkingStage = 'course' | 'context' | 'lecture' | 'exercise' | 'practice' | 'grade' | null
type CheckScope = 'lecture' | 'exercise'
type LaunchPhase = 'active' | 'leaving' | 'hidden'

function App() {
  const [launchPhase, setLaunchPhase] = useState<LaunchPhase>(() =>
    window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'hidden' : 'active')
  const [catalog, setCatalog] = useState<CourseCatalog | null>(null)
  const [courseId, setCourseId] = useState('')
  const [learningPath, setLearningPath] = useState<CourseLearningPath | null>(null)
  const [aiCatalog, setAiCatalog] = useState<AiCatalog | null>(null)
  const [modelId, setModelId] = useState('')
  const [mode, setMode] = useState<WorkspaceMode>('study')
  const [selectedLectureId, setSelectedLectureId] = useState('')
  const [selectedExerciseId, setSelectedExerciseId] = useState('')
  const [checkScope, setCheckScope] = useState<CheckScope>('lecture')
  const [lectureExplanation, setLectureExplanation] = useState<ExplainResponse | null>(null)
  const [exerciseExplanation, setExerciseExplanation] = useState<ExplainResponse | null>(null)
  const [practice, setPractice] = useState<PracticeResponse | null>(null)
  const [practiceDocumentId, setPracticeDocumentId] = useState('')
  const [selectedQuestion, setSelectedQuestion] = useState<PracticeQuestion | null>(null)
  const [studentAnswer, setStudentAnswer] = useState('')
  const [grade, setGrade] = useState<GradeResponse | null>(null)
  const [history, setHistory] = useState<StudyHistory | null>(null)
  const [workingStage, setWorkingStage] = useState<WorkingStage>('course')
  const [error, setError] = useState('')
  const [courseImportOpen, setCourseImportOpen] = useState(false)
  const [importingCourse, setImportingCourse] = useState(false)
  const [modelSetupOpen, setModelSetupOpen] = useState(false)
  const [extensionContext, setExtensionContext] = useState<ExtensionHandoff | null>(null)
  const [extensionExplanation, setExtensionExplanation] = useState<ExplainResponse | null>(null)

  const status = useMemo(
    () => catalog?.courses.find((course) => course.courseId === courseId) ?? null,
    [catalog, courseId],
  )
  const selectedModel = useMemo(
    () => aiCatalog?.models.find((model) => model.id === modelId) ?? null,
    [aiCatalog, modelId],
  )
  const lectureMaterials = useMemo(() => {
    if (!learningPath) return []
    return learningPath.lectures.length > 0
      ? learningPath.lectures
      : learningPath.otherMaterials.filter((material) => material.materialType !== 'exam')
  }, [learningPath])
  const selectedLecture = useMemo(
    () => lectureMaterials.find((material) => material.documentId === selectedLectureId) ?? null,
    [lectureMaterials, selectedLectureId],
  )
  const selectedExercise = useMemo(
    () => learningPath?.exercises.find((unit) => unit.id === selectedExerciseId) ?? null,
    [learningPath, selectedExerciseId],
  )

  useEffect(() => {
    if (launchPhase === 'hidden') return
    document.body.classList.add('launch-active')
    return () => document.body.classList.remove('launch-active')
  }, [launchPhase])

  useEffect(() => {
    if (launchPhase === 'active') {
      const timer = window.setTimeout(() => setLaunchPhase('leaving'), 2550)
      return () => window.clearTimeout(timer)
    }
    if (launchPhase === 'leaving') {
      const timer = window.setTimeout(() => setLaunchPhase('hidden'), 620)
      return () => window.clearTimeout(timer)
    }
  }, [launchPhase])

  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const root = document.documentElement
    let frameId = 0
    const updatePointer = (event: PointerEvent) => {
      window.cancelAnimationFrame(frameId)
      frameId = window.requestAnimationFrame(() => {
        root.style.setProperty('--pointer-x', `${event.clientX}px`)
        root.style.setProperty('--pointer-y', `${event.clientY}px`)
      })
    }
    const resetPointer = () => {
      root.style.setProperty('--pointer-x', '65vw')
      root.style.setProperty('--pointer-y', '24vh')
    }
    window.addEventListener('pointermove', updatePointer, { passive: true })
    window.addEventListener('blur', resetPointer)
    return () => {
      window.cancelAnimationFrame(frameId)
      window.removeEventListener('pointermove', updatePointer)
      window.removeEventListener('blur', resetPointer)
      root.style.removeProperty('--pointer-x')
      root.style.removeProperty('--pointer-y')
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    async function initialise() {
      try {
        const [courseCatalog, modelCatalog] = await Promise.all([fetchCourses(), fetchAiStatus()])
        if (cancelled) return
        setCatalog(courseCatalog)
        setAiCatalog(modelCatalog)
        const preferredModel = modelCatalog.models.find((model) =>
          model.id === modelCatalog.defaultModelId && model.available)
          ?? modelCatalog.models.find((model) => model.available)
          ?? modelCatalog.models[0]
        setModelId(preferredModel?.id ?? '')
        const handoff = parseExtensionHandoff(
          window.location.hash,
          courseCatalog.courses.filter((course) => course.ready).map((course) => course.courseId),
        )
        if (handoff) {
          setExtensionContext(handoff)
          window.history.replaceState(null, '', addressWithoutHandoff(window.location))
        }
        const initialCourseId = handoff?.courseId
          || courseCatalog.defaultCourseId
          || courseCatalog.courses[0]?.courseId
          || ''
        setCourseId(initialCourseId)
        if (initialCourseId) {
          const [path, studyHistory] = await Promise.all([
            fetchLearningPath(initialCourseId),
            fetchStudyHistory(initialCourseId),
          ])
          if (!cancelled) {
            applyLearningPath(path)
            setHistory(studyHistory)
          }
        }
      } catch (requestError) {
        if (!cancelled) setError(readError(requestError, 'StudyLens could not load the course workspace.'))
      } finally {
        if (!cancelled) setWorkingStage(null)
      }
    }
    void initialise()
    return () => { cancelled = true }
  }, [])

  function applyLearningPath(path: CourseLearningPath) {
    setLearningPath(path)
    const lectures = path.lectures.length > 0
      ? path.lectures
      : path.otherMaterials.filter((material) => material.materialType !== 'exam')
    setSelectedLectureId(lectures[0]?.documentId ?? '')
    const firstCompleteExercise = path.exercises.find((unit) => unit.exercise && unit.solution)
      ?? path.exercises.find((unit) => unit.exercise)
      ?? path.exercises[0]
    setSelectedExerciseId(firstCompleteExercise?.id ?? '')
  }

  function resetLearningResults() {
    setLectureExplanation(null)
    setExerciseExplanation(null)
    setPractice(null)
    setPracticeDocumentId('')
    setSelectedQuestion(null)
    setStudentAnswer('')
    setGrade(null)
    setExtensionExplanation(null)
  }

  async function switchCourse(nextCourseId: string) {
    setCourseId(nextCourseId)
    setWorkingStage('course')
    setError('')
    resetLearningResults()
    try {
      const [path, studyHistory] = await Promise.all([
        fetchLearningPath(nextCourseId),
        fetchStudyHistory(nextCourseId),
      ])
      applyLearningPath(path)
      setHistory(studyHistory)
      setMode('study')
    } catch (requestError) {
      setError(readError(requestError, 'This course could not be opened.'))
    } finally {
      setWorkingStage(null)
    }
  }

  function selectModel(nextModelId: string) {
    const model = aiCatalog?.models.find((item) => item.id === nextModelId)
    setModelId(nextModelId)
    resetLearningResults()
    if (model && !model.available) setModelSetupOpen(true)
  }

  async function teachLecture() {
    if (!courseId || !selectedLectureId || !selectedModel?.available) return
    setWorkingStage('lecture')
    setError('')
    try {
      setLectureExplanation(await explainLecture(courseId, selectedLectureId, modelId))
    } catch (requestError) {
      setError(readError(requestError, 'The selected lecture could not be explained.'))
    } finally {
      setWorkingStage(null)
    }
  }

  async function explainExtensionContext() {
    if (!courseId || !extensionContext || !selectedModel?.available) return
    setWorkingStage('context')
    setError('')
    try {
      setExtensionExplanation(await explainCourse(courseId, extensionContext.question, modelId))
    } catch (requestError) {
      setError(readError(requestError, 'The selected web text could not be explained.'))
    } finally {
      setWorkingStage(null)
    }
  }

  async function teachExercise() {
    if (!courseId || !selectedExercise || !selectedModel?.available) return
    setWorkingStage('exercise')
    setError('')
    try {
      setExerciseExplanation(await explainExercise(
        courseId,
        selectedExercise.exercise?.documentId ?? null,
        selectedExercise.solution?.documentId ?? null,
        modelId,
      ))
    } catch (requestError) {
      setError(readError(requestError, 'The selected exercise could not be explained.'))
    } finally {
      setWorkingStage(null)
    }
  }

  async function generateKnowledgeCheck() {
    if (!courseId || !selectedModel?.available) return
    const scopedMaterial = checkScope === 'lecture'
      ? selectedLecture
      : selectedExercise?.exercise ?? selectedExercise?.solution ?? null
    if (!scopedMaterial) return
    setWorkingStage('practice')
    setError('')
    setGrade(null)
    try {
      const response = await createPractice(
        courseId,
        scopedMaterial.title,
        modelId,
        scopedMaterial.documentId,
      )
      setPractice(response)
      setPracticeDocumentId(scopedMaterial.documentId)
      setSelectedQuestion(response.questions[0] ?? null)
      setStudentAnswer('')
    } catch (requestError) {
      setError(readError(requestError, 'The knowledge check could not be generated.'))
    } finally {
      setWorkingStage(null)
    }
  }

  async function submitAnswer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!courseId || !selectedQuestion || !studentAnswer.trim() || !selectedModel?.available) return
    setWorkingStage('grade')
    setError('')
    try {
      const response = await gradeAnswer(
        courseId,
        selectedQuestion.question,
        studentAnswer.trim(),
        modelId,
        practiceDocumentId || undefined,
      )
      setGrade(response)
      setHistory(await fetchStudyHistory(courseId))
    } catch (requestError) {
      setError(readError(requestError, 'Your answer could not be graded.'))
    } finally {
      setWorkingStage(null)
    }
  }

  function viewAttempt(attempt: StudyAttempt) {
    setGrade({
      courseId: attempt.courseId,
      question: attempt.question,
      score: attempt.score,
      maxScore: attempt.maxScore,
      summary: attempt.summary,
      strengths: attempt.strengths,
      missingPoints: attempt.missingPoints,
      improvedAnswer: attempt.improvedAnswer,
      model: attempt.model,
      citations: attempt.citations,
      attemptId: attempt.id,
      createdAtUtc: attempt.createdAtUtc,
    })
  }

  async function deleteHistory() {
    if (!courseId || !window.confirm('Delete all locally saved attempts for this course?')) return
    setWorkingStage('grade')
    setError('')
    try {
      await clearStudyHistory(courseId)
      setHistory(await fetchStudyHistory(courseId))
      setGrade(null)
    } catch (requestError) {
      setError(readError(requestError, 'Study history could not be deleted.'))
    } finally {
      setWorkingStage(null)
    }
  }

  async function importNewCourse(courseName: string, files: File[]) {
    setImportingCourse(true)
    try {
      const imported = await importCourse(courseName, files)
      const [refreshedCatalog, path, studyHistory] = await Promise.all([
        fetchCourses(),
        fetchLearningPath(imported.courseId),
        fetchStudyHistory(imported.courseId),
      ])
      setCatalog(refreshedCatalog)
      setCourseId(imported.courseId)
      applyLearningPath(path)
      setHistory(studyHistory)
      resetLearningResults()
      setMode('study')
      setCourseImportOpen(false)
    } finally {
      setImportingCourse(false)
    }
  }

  return <>
    {launchPhase !== 'hidden' && <LaunchSequence phase={launchPhase} onSkip={() => setLaunchPhase('leaving')} />}
    <AmbientBackground />
    <div className={`app-frame ${launchPhase === 'hidden' ? 'workspace-live' : 'workspace-standby'}`} inert={launchPhase !== 'hidden'}>
      <aside className="sidebar">
        <div className="brand"><span className="brand-mark"><i /><b /></span><div><strong>StudyLens</strong><span>Learning intelligence</span></div></div>
        <div className="workspace-chip"><i /> AI WORKSPACE · ONLINE</div>
        <nav aria-label="Tutor sections">
          <button className={`nav-item ${mode === 'study' ? 'active' : ''}`} onClick={() => setMode('study')}><span>⌁</span> Study path</button>
          <button className={`nav-item ${mode === 'feedback' ? 'active' : ''}`} onClick={() => setMode('feedback')}><span>✓</span> Past feedback</button>
        </nav>

        <div className="course-card">
          <p>ACTIVE COURSE</p>
          <select aria-label="Select course" className="course-select" disabled={workingStage === 'course'} value={courseId} onChange={(event) => { void switchCourse(event.target.value) }}>
            {catalog?.courses.map((course) => <option value={course.courseId} key={course.courseId}>{course.courseName ?? course.courseId}</option>)}
          </select>
          <span>{status?.courseName ?? 'No course configured'}</span>
          <button className="add-course-button" onClick={() => setCourseImportOpen(true)}>＋ Add files or folder</button>
          <div className={`corpus-state ${status?.ready ? 'ready' : ''}`}><i /> {status?.ready ? 'Learning path ready' : 'Setup required'}</div>
        </div>

        <div className="model-card">
          <p>AI TEACHER</p>
          <select aria-label="Select AI model" value={modelId} onChange={(event) => selectModel(event.target.value)}>
            {aiCatalog?.models.map((model) => <option key={model.id} value={model.id}>{model.displayName}{model.available ? '' : ' · connect first'}</option>)}
          </select>
          <div className={`model-state ${selectedModel?.available ? 'ready' : ''}`}><i /><span>{selectedModel?.message ?? 'Checking available models…'}</span></div>
          {!selectedModel?.available && <button className="model-setup-button" onClick={() => setModelSetupOpen(true)}>How to connect</button>}
        </div>

        <div className="privacy-note"><span>⌂</span><p><strong>{selectedModel?.local === false ? 'Cloud teacher' : 'Local teacher'}</strong>{selectedModel?.local === false ? 'Only the selected course passages are sent to this API.' : 'Course files and inference stay on this computer.'}</p></div>
      </aside>

      <main className="workspace">
        <header className="workspace-header">
          <div className="hero-copy"><div className="hero-badge"><i /> AI-POWERED LEARNING SYSTEM <span>v1.0</span></div><p className="eyebrow">GUIDED COURSE LEARNING</p><h1>{mode === 'study' ? <>Learn. Apply. <em>Master.</em></> : <>Turn feedback into <em>progress.</em></>}</h1><p className="intro">Follow the material in order: understand a lecture, work through the course exercise, then prove what you learned.</p><div className="hero-signals"><span><i /> Source grounded</span><span><i /> Private by design</span><span><i /> Multi-model AI</span></div></div>
          <div className="index-summary" aria-label="Course learning materials"><div><strong>{lectureMaterials.length}</strong><span>lectures</span></div><div><strong>{learningPath?.exercises.length ?? '—'}</strong><span>exercise sets</span></div><div><strong>{history?.attemptCount ?? 0}</strong><span>attempts</span></div></div>
        </header>

        {error && <div className="error-banner" role="alert">{error}</div>}
        {selectedModel && !selectedModel.available && <div className="model-warning"><strong>{selectedModel.displayName} is not connected yet.</strong><span>You can select it now, but connect its API or local runtime before starting a lesson.</span><button onClick={() => setModelSetupOpen(true)}>Show setup</button></div>}
        {mode === 'study' && extensionContext && <ExtensionContextPanel
          aiReady={selectedModel?.available === true}
          context={extensionContext}
          explanation={extensionExplanation}
          courseId={courseId}
          sourceAvailable={status?.sourceAvailable === true}
          working={workingStage === 'context'}
          onDismiss={() => { setExtensionContext(null); setExtensionExplanation(null) }}
          onExplain={() => { void explainExtensionContext() }}
        />}

        {mode === 'study' ? <LearningJourney
          aiReady={selectedModel?.available === true}
          checkScope={checkScope}
          courseId={courseId}
          exerciseExplanation={exerciseExplanation}
          exerciseUnits={learningPath?.exercises ?? []}
          grade={grade}
          lectureExplanation={lectureExplanation}
          lectureMaterials={lectureMaterials}
          modelName={selectedModel?.displayName ?? 'AI teacher'}
          practice={practice}
          selectedExercise={selectedExercise}
          selectedExerciseId={selectedExerciseId}
          selectedLecture={selectedLecture}
          selectedLectureId={selectedLectureId}
          selectedQuestion={selectedQuestion}
          sourceAvailable={status?.sourceAvailable === true}
          studentAnswer={studentAnswer}
          workingStage={workingStage}
          onCheckScopeChange={setCheckScope}
          onGenerateCheck={() => { void generateKnowledgeCheck() }}
          onGrade={submitAnswer}
          onSelectExercise={(id) => { setSelectedExerciseId(id); setExerciseExplanation(null); setPractice(null); setGrade(null) }}
          onSelectLecture={(id) => { setSelectedLectureId(id); setLectureExplanation(null); setPractice(null); setGrade(null) }}
          onSelectQuestion={(question) => { setSelectedQuestion(question); setStudentAnswer(''); setGrade(null) }}
          onStudentAnswerChange={setStudentAnswer}
          onTeachExercise={() => { void teachExercise() }}
          onTeachLecture={() => { void teachLecture() }}
        /> : <FeedbackWorkspace
          courseId={courseId}
          grade={grade}
          history={history}
          sourceAvailable={status?.sourceAvailable === true}
          working={workingStage !== null}
          onClearHistory={() => { void deleteHistory() }}
          onPractice={() => setMode('study')}
          onSelectAttempt={viewAttempt}
        />}

        <footer><span>StudyLens · Guided, source-grounded learning</span><span>React · ASP.NET Core · Python · Ollama / OpenAI / Gemini</span></footer>
      </main>
    </div>

    {courseImportOpen && <CourseImportDialog importing={importingCourse} onClose={() => { if (!importingCourse) setCourseImportOpen(false) }} onImport={importNewCourse} />}
    {modelSetupOpen && selectedModel && <ModelSetupDialog model={selectedModel} onClose={() => setModelSetupOpen(false)} />}
  </>
}

function AmbientBackground() {
  return <div aria-hidden="true" className="ambient-scene"><div className="ambient-grid" /><div className="pointer-aura" /><div className="ambient-orb orb-one" /><div className="ambient-orb orb-two" /><div className="ambient-orb orb-three" /><div className="particle-field">{Array.from({ length: 32 }, (_, index) => <i key={index} style={{ '--particle-x': `${(index * 37 + 11) % 100}%`, '--particle-y': `${(index * 61 + 7) % 100}%`, '--particle-size': `${2 + (index % 4)}px`, '--particle-delay': `${-(index % 11) * 1.2}s`, '--particle-duration': `${12 + (index % 7) * 2}s` } as CSSProperties} />)}</div></div>
}

function LaunchSequence(props: { phase: LaunchPhase; onSkip: () => void }) {
  return <section className="launch-screen" data-phase={props.phase} aria-label="StudyLens is opening" role="status">
    <div aria-hidden="true" className="launch-grid" />
    <div aria-hidden="true" className="launch-horizon" />
    <div aria-hidden="true" className="launch-scan" />
    <div aria-hidden="true" className="launch-particles">{Array.from({ length: 18 }, (_, index) => <i key={index} style={{ '--launch-x': `${(index * 29 + 7) % 96}%`, '--launch-y': `${(index * 47 + 13) % 88}%`, '--launch-delay': `${index * -.17}s` } as CSSProperties} />)}</div>
    <button className="launch-skip" onClick={props.onSkip}>Skip intro <span>↗</span></button>
    <div className="launch-content">
      <p className="launch-kicker"><i /> STUDYLENS / INTELLIGENCE WORKSPACE</p>
      <div className="launch-emblem" aria-hidden="true">
        <span className="launch-orbit orbit-outer"><i /><b /></span>
        <span className="launch-orbit orbit-inner"><i /></span>
        <span className="launch-logo"><i /><b /></span>
      </div>
      <h1>Knowledge,<br /><em>operationalized.</em></h1>
      <p className="launch-copy">Connecting course evidence, model intelligence, and measurable learning progress.</p>
      <div className="launch-status-grid" aria-label="Workspace initialization status">
        <div><span>COURSE GRAPH</span><strong><i /> ONLINE</strong></div>
        <div><span>EVIDENCE ENGINE</span><strong><i /> VERIFIED</strong></div>
        <div><span>AI ORCHESTRATION</span><strong><i /> READY</strong></div>
      </div>
      <div className="launch-progress"><span /><i /></div>
      <p className="launch-progress-copy"><span>Initializing source-grounded workspace</span><b>01 — 03</b></p>
    </div>
  </section>
}

function ExtensionContextPanel(props: {
  aiReady: boolean
  context: ExtensionHandoff
  courseId: string
  explanation: ExplainResponse | null
  sourceAvailable: boolean
  working: boolean
  onDismiss: () => void
  onExplain: () => void
}) {
  return <section className="extension-context-panel">
    <div className="extension-context-heading">
      <div className="extension-context-icon">↗</div>
      <div><p className="eyebrow">BROWSER EXTENSION HANDOFF</p><h2>Connect this page to your course</h2><span>Selected on {props.context.source}</span></div>
      <button aria-label="Dismiss browser context" onClick={props.onDismiss}>×</button>
    </div>
    <blockquote>{props.context.question}</blockquote>
    <div className="extension-context-actions">
      <span><i /> The URL fragment has already been removed.</span>
      <button disabled={!props.aiReady || props.working} onClick={props.onExplain}>{props.working ? 'Finding course evidence…' : 'Explain with course evidence'}</button>
    </div>
    {props.explanation && <LessonResult label="WEB CONTEXT EXPLANATION" response={props.explanation} courseId={props.courseId} sourceAvailable={props.sourceAvailable} />}
  </section>
}

interface LearningJourneyProps {
  aiReady: boolean
  checkScope: CheckScope
  courseId: string
  exerciseExplanation: ExplainResponse | null
  exerciseUnits: CourseExerciseUnit[]
  grade: GradeResponse | null
  lectureExplanation: ExplainResponse | null
  lectureMaterials: CourseMaterial[]
  modelName: string
  practice: PracticeResponse | null
  selectedExercise: CourseExerciseUnit | null
  selectedExerciseId: string
  selectedLecture: CourseMaterial | null
  selectedLectureId: string
  selectedQuestion: PracticeQuestion | null
  sourceAvailable: boolean
  studentAnswer: string
  workingStage: WorkingStage
  onCheckScopeChange: (scope: CheckScope) => void
  onGenerateCheck: () => void
  onGrade: (event: FormEvent<HTMLFormElement>) => void
  onSelectExercise: (id: string) => void
  onSelectLecture: (id: string) => void
  onSelectQuestion: (question: PracticeQuestion) => void
  onStudentAnswerChange: (value: string) => void
  onTeachExercise: () => void
  onTeachLecture: () => void
}

function LearningJourney(props: LearningJourneyProps) {
  const exerciseScopeAvailable = Boolean(props.selectedExercise?.exercise ?? props.selectedExercise?.solution)
  return <div className="learning-journey">
    <div className="journey-rail" aria-label="Learning stages"><span className="active">1 · Learn lecture</span><i /><span>2 · Understand exercise</span><i /><span>3 · Knowledge check</span></div>

    <section className="learning-stage lecture-learning-stage">
      <StageHeading number="01" eyebrow="LEARN THE LECTURE" title="Let the course teach you first" description="Choose a lecture. The AI follows that document from beginning to end and explains its ideas with page citations." />
      {props.lectureMaterials.length > 0 ? <>
        <MaterialPicker label="Lecture or course chapter" value={props.selectedLectureId} materials={props.lectureMaterials} onChange={props.onSelectLecture} />
        {props.selectedLecture && <MaterialMeta material={props.selectedLecture} />}
        <button className="journey-action" disabled={!props.aiReady || !props.selectedLectureId || props.workingStage !== null} onClick={props.onTeachLecture}>{props.workingStage === 'lecture' ? `${props.modelName} is preparing the lesson…` : `Teach this lecture with ${props.modelName}`}</button>
        {props.lectureExplanation && <LessonResult label="LECTURE LESSON" response={props.lectureExplanation} courseId={props.courseId} sourceAvailable={props.sourceAvailable} />}
      </> : <MissingMaterial kind="lecture" />}
    </section>

    <section className="learning-stage exercise-learning-stage">
      <StageHeading number="02" eyebrow="UNDERSTAND THE COURSE EXERCISE" title="See how the theory is applied" description="StudyLens pairs the exercise sheet with its provided solution and explains the reasoning, not just the final answer." />
      {props.exerciseUnits.length > 0 ? <>
        <label className="material-picker"><span>Exercise set</span><select aria-label="Select course exercise" value={props.selectedExerciseId} onChange={(event) => props.onSelectExercise(event.target.value)}>{props.exerciseUnits.map((unit) => <option key={unit.id} value={unit.id}>{unit.title}</option>)}</select></label>
        {props.selectedExercise && <div className="exercise-pair"><MaterialPairItem label="Exercise" material={props.selectedExercise.exercise} /><span className="pair-arrow">→</span><MaterialPairItem label="Provided solution" material={props.selectedExercise.solution} /></div>}
        <button className="journey-action" disabled={!props.aiReady || !props.selectedExercise || props.workingStage !== null} onClick={props.onTeachExercise}>{props.workingStage === 'exercise' ? `${props.modelName} is working through it…` : `Explain exercise and solution`}</button>
        {props.exerciseExplanation && <LessonResult label="EXERCISE WALKTHROUGH" response={props.exerciseExplanation} courseId={props.courseId} sourceAvailable={props.sourceAvailable} />}
      </> : <MissingMaterial kind="exercise" />}
    </section>

    <section className="learning-stage check-learning-stage">
      <StageHeading number="03" eyebrow="CHECK YOUR UNDERSTANDING" title="Now answer without looking" description="The AI creates new questions from what you selected, grades your answer against the same source, and shows exactly what is missing." />
      <div className="scope-choice" role="group" aria-label="Knowledge check source"><button className={props.checkScope === 'lecture' ? 'selected' : ''} disabled={!props.selectedLecture} onClick={() => props.onCheckScopeChange('lecture')}>Selected lecture</button><button className={props.checkScope === 'exercise' ? 'selected' : ''} disabled={!exerciseScopeAvailable} onClick={() => props.onCheckScopeChange('exercise')}>Selected exercise</button></div>
      <button className="journey-action" disabled={!props.aiReady || props.workingStage !== null || (props.checkScope === 'lecture' ? !props.selectedLecture : !exerciseScopeAvailable)} onClick={props.onGenerateCheck}>{props.workingStage === 'practice' ? `${props.modelName} is creating questions…` : 'Generate 3 knowledge-check questions'}</button>

      {props.practice && <div className="knowledge-check-grid">
        <div className="question-list"><p className="eyebrow">CHOOSE A QUESTION</p>{props.practice.questions.map((question, index) => <button className={`question-card ${props.selectedQuestion?.id === question.id ? 'selected' : ''}`} key={question.id} onClick={() => props.onSelectQuestion(question)}><span>{String(index + 1).padStart(2, '0')}</span><div><small>{question.commandWord} · {question.maxScore} pts</small><strong>{question.question}</strong></div></button>)}</div>
        <div className="answer-card"><p className="eyebrow">YOUR ANSWER</p><h2>{props.selectedQuestion?.question ?? 'Choose a question'}</h2><form onSubmit={props.onGrade}><textarea aria-label="Your answer" placeholder="Answer from memory. English is recommended for exam practice…" value={props.studentAnswer} onChange={(event) => props.onStudentAnswerChange(event.target.value)} /><button className="primary-action" disabled={!props.studentAnswer.trim() || props.workingStage !== null} type="submit">{props.workingStage === 'grade' ? 'Comparing with course evidence…' : 'Check my answer'}</button></form></div>
      </div>}
      {props.grade && <InlineGrade grade={props.grade} courseId={props.courseId} sourceAvailable={props.sourceAvailable} />}
    </section>
  </div>
}

function StageHeading(props: { number: string; eyebrow: string; title: string; description: string }) {
  return <div className="stage-heading"><span className="stage-number">{props.number}</span><div><p className="eyebrow">{props.eyebrow}</p><h2>{props.title}</h2><p>{props.description}</p></div></div>
}

function MaterialPicker(props: { label: string; value: string; materials: CourseMaterial[]; onChange: (id: string) => void }) {
  return <label className="material-picker"><span>{props.label}</span><select aria-label={`Select ${props.label.toLowerCase()}`} value={props.value} onChange={(event) => props.onChange(event.target.value)}>{props.materials.map((material) => <option key={material.documentId} value={material.documentId}>{material.title}</option>)}</select></label>
}

function MaterialMeta(props: { material: CourseMaterial }) {
  return <div className="material-meta"><strong>{displayPath(props.material.relativePath)}</strong><span>{props.material.pageCount} pages · {props.material.chunkCount} evidence passages</span></div>
}

function MaterialPairItem(props: { label: string; material: CourseMaterial | null }) {
  return <div className={props.material ? 'available' : 'missing'}><small>{props.label}</small><strong>{props.material?.title ?? 'Not found in this course'}</strong>{props.material && <span>{props.material.pageCount} pages</span>}</div>
}

function MissingMaterial(props: { kind: 'lecture' | 'exercise' }) {
  return <div className="missing-material"><strong>No {props.kind} material was detected.</strong><span>{props.kind === 'lecture' ? 'Put files in a Lecture folder, or use names such as L01, L02.' : 'Put sheets in Exercise and answers in Solution folders so StudyLens can pair them.'}</span></div>
}

function LessonResult(props: { label: string; response: ExplainResponse; courseId: string; sourceAvailable: boolean }) {
  return <article className="lesson-result"><div className="lesson-result-heading"><div><p className="eyebrow">{props.label}</p><h3>{props.response.question}</h3></div><span>{props.response.model}</span></div><div className="lesson-answer">{props.response.answer}</div><CourseVisuals citations={props.response.citations} courseId={props.courseId} sourceAvailable={props.sourceAvailable} /><CitationLinks citations={props.response.citations} courseId={props.courseId} sourceAvailable={props.sourceAvailable} /></article>
}

function InlineGrade(props: { grade: GradeResponse; courseId: string; sourceAvailable: boolean }) {
  return <article className="inline-grade"><div className="inline-score"><strong>{props.grade.score}</strong><span>/ {props.grade.maxScore}</span></div><div><p className="eyebrow">KNOWLEDGE-CHECK FEEDBACK · {props.grade.model}</p><h2>{props.grade.summary}</h2><div className="feedback-columns"><div><h3>What you understood</h3><ul>{props.grade.strengths.map((item) => <li key={item}>{item}</li>)}</ul></div><div><h3>What to review</h3><ul>{props.grade.missingPoints.map((item) => <li key={item}>{item}</li>)}</ul></div></div><div className="improved-answer"><p className="eyebrow">MODEL EXAM ANSWER</p><div>{props.grade.improvedAnswer}</div></div><CitationLinks citations={props.grade.citations} courseId={props.courseId} sourceAvailable={props.sourceAvailable} /></div></article>
}

interface FeedbackWorkspaceProps {
  courseId: string
  grade: GradeResponse | null
  history: StudyHistory | null
  sourceAvailable: boolean
  working: boolean
  onClearHistory: () => void
  onPractice: () => void
  onSelectAttempt: (attempt: StudyAttempt) => void
}

function FeedbackWorkspace(props: FeedbackWorkspaceProps) {
  return <div className="feedback-layout">
    <aside className="history-panel"><div className="history-heading"><div><p className="eyebrow">LOCAL STUDY HISTORY</p><h2>Past attempts</h2></div>{props.history && props.history.attemptCount > 0 && <button disabled={props.working} onClick={props.onClearHistory}>Clear</button>}</div><div className="history-stats"><div><strong>{props.history?.attemptCount ?? 0}</strong><span>attempts</span></div><div><strong>{props.history?.averagePercentage == null ? '—' : `${props.history.averagePercentage}%`}</strong><span>average</span></div></div><div className="history-list">{props.history?.attempts.map((attempt) => <button className={props.grade?.attemptId === attempt.id ? 'selected' : ''} key={attempt.id} onClick={() => props.onSelectAttempt(attempt)}><span>{attempt.score}/{attempt.maxScore}</span><div><strong>{attempt.question}</strong><small>{formatDate(attempt.createdAtUtc)} · {attempt.model}</small></div></button>)}{props.history?.attempts.length === 0 && <p className="history-empty">Completed knowledge checks will be saved locally on this computer.</p>}</div></aside>
    {props.grade ? <section className="feedback-report"><div className="score-ring"><strong>{props.grade.score}</strong><span>/ {props.grade.maxScore}</span></div><div className="feedback-main"><p className="eyebrow">FORMATIVE FEEDBACK · {props.grade.model}</p><h2>{props.grade.summary}</h2><p className="feedback-question">{props.grade.question}</p><div className="feedback-columns"><div><h3>What worked</h3><ul>{props.grade.strengths.map((item) => <li key={item}>{item}</li>)}</ul></div><div><h3>What is missing</h3><ul>{props.grade.missingPoints.map((item) => <li key={item}>{item}</li>)}</ul></div></div><div className="improved-answer"><p className="eyebrow">IMPROVED EXAM ANSWER</p><div>{props.grade.improvedAnswer}</div></div><CitationLinks citations={props.grade.citations} courseId={props.courseId} sourceAvailable={props.sourceAvailable} /></div></section> : <div className="empty-feedback"><span>✓</span><h2>No feedback selected</h2><p>Complete a knowledge check or open a saved attempt.</p><button className="primary-action" onClick={props.onPractice}>Start learning</button></div>}
  </div>
}

interface CourseImportDialogProps {
  importing: boolean
  onClose: () => void
  onImport: (courseName: string, files: File[]) => Promise<void>
}

function CourseImportDialog(props: CourseImportDialogProps) {
  const [courseName, setCourseName] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [selectionKind, setSelectionKind] = useState<'files' | 'folder' | null>(null)
  const [skippedFileCount, setSkippedFileCount] = useState(0)
  const [importError, setImportError] = useState('')

  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape' && !props.importing) props.onClose() }
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', closeOnEscape)
    return () => { document.body.style.overflow = previousOverflow; window.removeEventListener('keydown', closeOnEscape) }
  }, [props])

  async function submitImport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (courseName.trim().length < 2 || files.length === 0) return
    setImportError('')
    try { await props.onImport(courseName.trim(), files) } catch (requestError) { setImportError(readError(requestError, 'This course could not be imported.')) }
  }

  function chooseFiles(selected: FileList | null, kind: 'files' | 'folder') {
    const selectedFiles = Array.from(selected ?? [])
    const supportedFiles = selectedFiles.filter((file) => /\.(pdf|md|txt)$/i.test(file.name))
    setFiles(supportedFiles)
    setSkippedFileCount(selectedFiles.length - supportedFiles.length)
    setImportError(supportedFiles.length === 0 && selectedFiles.length > 0
      ? 'This selection contains no supported PDF, Markdown, or text files.'
      : '')
    setSelectionKind(kind)
  }

  const totalMegabytes = files.reduce((sum, file) => sum + file.size, 0) / 1024 / 1024
  const firstPath = files[0]?.webkitRelativePath || files[0]?.name

  return <div className="course-import-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget && !props.importing) props.onClose() }}><section aria-labelledby="course-import-title" aria-modal="true" className="course-import-dialog" role="dialog"><header><div><p className="eyebrow">BUILD A COURSE LEARNING PATH</p><h2 id="course-import-title">Add course material</h2><span>Select individual files or one complete course folder. Folder structure helps identify lectures, exercises, and solutions.</span></div><button aria-label="Close course import" className="detail-close" disabled={props.importing} onClick={props.onClose}>×</button></header><form onSubmit={(event) => { void submitImport(event) }}><label className="course-name-field"><span>Course name</span><input autoFocus maxLength={100} minLength={2} placeholder="e.g. Database Systems" required value={courseName} onChange={(event) => setCourseName(event.target.value)} /></label><div className="import-choice-grid"><label className="course-file-drop"><strong>Choose files</strong><span>Select several PDF, Markdown, or text files</span><input accept=".pdf,.md,.txt,application/pdf,text/markdown,text/plain" multiple type="file" onChange={(event) => chooseFiles(event.target.files, 'files')} /></label><label className="course-file-drop folder-drop"><strong>Choose a folder</strong><span>Best for Lecture / Exercise / Solution folders</span><input accept=".pdf,.md,.txt" ref={(element) => { if (element) element.setAttribute('webkitdirectory', '') }} multiple type="file" onChange={(event) => chooseFiles(event.target.files, 'folder')} /></label></div>{files.length > 0 && <div className="course-file-summary"><strong>{selectionKind === 'folder' ? 'Folder selected' : 'Files selected'} · {files.length} file{files.length === 1 ? '' : 's'} · {totalMegabytes.toFixed(1)} MB</strong><span>{firstPath}{files.length > 1 ? ` · +${files.length - 1} more` : ''}</span></div>}{skippedFileCount > 0 && <div className="course-import-note">Skipped {skippedFileCount} unsupported file{skippedFileCount === 1 ? '' : 's'}; only PDF, Markdown, and text are imported.</div>}<div className="folder-tip"><strong>Recommended structure</strong><code>My Course / Lecture / Exercise / Solution</code></div><div className="course-import-privacy"><span>⌂</span><p><strong>Your files stay on this computer.</strong>They are copied into StudyLens and indexed locally. A cloud model receives only selected passages when you use it.</p></div>{importError && <div className="course-import-error" role="alert">{importError}</div>}<div className="course-import-actions"><button disabled={props.importing} onClick={props.onClose} type="button">Cancel</button><button className="import-action" disabled={props.importing || courseName.trim().length < 2 || files.length === 0} type="submit">{props.importing ? 'Building learning path…' : 'Import course'}</button></div></form></section></div>
}

function ModelSetupDialog(props: { model: AiStatus; onClose: () => void }) {
  const isGemini = props.model.provider.toLowerCase().includes('gemini')
  const isOpenAi = props.model.provider.toLowerCase().includes('openai')
  const command = isGemini
    ? 'powershell -ExecutionPolicy Bypass -File scripts\\setup-cloud-ai.ps1 -Provider Gemini'
    : isOpenAi
      ? 'powershell -ExecutionPolicy Bypass -File scripts\\setup-cloud-ai.ps1 -Provider OpenAI'
      : 'powershell -ExecutionPolicy Bypass -File scripts\\setup-local-ai.ps1'
  const keyUrl = isGemini ? 'https://aistudio.google.com/app/apikey' : isOpenAi ? 'https://platform.openai.com/api-keys' : null
  return <div className="course-import-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) props.onClose() }}><section aria-labelledby="model-setup-title" aria-modal="true" className="model-setup-dialog" role="dialog"><header><div><p className="eyebrow">CONNECT AI TEACHER</p><h2 id="model-setup-title">{props.model.displayName}</h2><span>{props.model.message}</span></div><button aria-label="Close model setup" className="detail-close" onClick={props.onClose}>×</button></header><div className="model-setup-body"><ol>{keyUrl && <li><strong>Create an API key</strong><span>The provider—not ChatGPT Plus or Gemini Advanced—issues the developer API key.</span><a href={keyUrl} target="_blank" rel="noreferrer">Open provider key page ↗</a></li>}<li><strong>Save it without putting it in the repository</strong><span>Run this command from the StudyLens project folder. Your typed key is hidden.</span><code>{command}</code></li><li><strong>Restart StudyLens</strong><span>The model becomes ready in this selector after the backend restarts.</span></li></ol><button className="journey-action" onClick={props.onClose}>Got it</button></div></section></div>
}

function CitationLinks(props: { citations: TutorCitation[]; courseId: string; sourceAvailable: boolean }) {
  return <div className="citation-list"><strong>Course pages used</strong>{props.citations.map((citation) => props.sourceAvailable ? <a key={citation.chunkId} href={documentUrl(props.courseId, citation.documentId, citation.page)} target="_blank" rel="noreferrer">[{citation.number}] {citation.title}, p. {citation.page}</a> : <span key={citation.chunkId}>[{citation.number}] {citation.title}, p. {citation.page}</span>)}</div>
}

function CourseVisuals(props: { citations: TutorCitation[]; courseId: string; sourceAvailable: boolean }) {
  const [failedPreviews, setFailedPreviews] = useState<string[]>([])
  if (!props.sourceAvailable) return null

  const pages = props.citations
    .filter((citation) => citation.relativePath.toLowerCase().endsWith('.pdf'))
    .filter((citation, index, citations) => citations.findIndex((candidate) => candidate.documentId === citation.documentId && candidate.page === citation.page) === index)
    .slice(0, 3)
  const visiblePages = pages.filter((citation) => !failedPreviews.includes(`${citation.documentId}:${citation.page}`))
  if (visiblePages.length === 0) return null

  return <section className="course-visuals"><div className="course-visuals-heading"><div><p className="eyebrow">VISUAL EXPLANATION</p><h4>Referenced course pages</h4></div><span>These are real pages from your material, not AI-generated images.</span></div><div className="course-visual-grid">{visiblePages.map((citation) => {
    const previewKey = `${citation.documentId}:${citation.page}`
    return <a href={documentUrl(props.courseId, citation.documentId, citation.page)} key={previewKey} target="_blank" rel="noreferrer"><figure><img alt={`${citation.title}, page ${citation.page}`} loading="lazy" onError={() => setFailedPreviews((current) => current.includes(previewKey) ? current : [...current, previewKey])} src={pagePreviewUrl(props.courseId, citation.documentId, citation.page)} /><figcaption><strong>[{citation.number}] {citation.title}</strong><span>Page {citation.page} · Open original ↗</span></figcaption></figure></a>
  })}</div></section>
}

function documentUrl(courseId: string, documentId: string, page: number) {
  return `/api/courses/${encodeURIComponent(courseId)}/documents/${encodeURIComponent(documentId)}#page=${page}`
}

function pagePreviewUrl(courseId: string, documentId: string, page: number) {
  return `/api/courses/${encodeURIComponent(courseId)}/documents/${encodeURIComponent(documentId)}/pages/${page}/preview`
}

function displayPath(value: string) {
  return value.replaceAll('/', ' › ')
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function readError(value: unknown, fallback: string) {
  return value instanceof Error && value.message ? value.message : fallback
}

export default App
