import assert from 'node:assert/strict'
import test from 'node:test'
import {
  createStudyLensUrl,
  maximumSelectionLength,
  pendingSelectionMaxAgeMs,
  normaliseSelection,
  readPendingSelection,
} from './handoff.ts'

test('creates a private local handoff URL with encoded page context', () => {
  const url = createStudyLensUrl('http://127.0.0.1:5080/', {
    courseId: 'eam',
    question: 'What is a viewpoint & why?',
    source: 'Architecture lecture',
  })

  const fragment = new URLSearchParams(url.split('#')[1])
  assert.equal(url.startsWith('http://127.0.0.1:5080/#'), true)
  assert.equal(fragment.get('question'), 'What is a viewpoint & why?')
  assert.equal(fragment.get('source'), 'Architecture lecture')
  assert.equal(fragment.get('from'), 'extension')
})

test('trims and limits selected text before handoff', () => {
  const selection = normaliseSelection(`  ${'a'.repeat(maximumSelectionLength + 20)}  `)
  assert.equal(selection.length, maximumSelectionLength)
  assert.equal(selection.startsWith('a'), true)
})

test('accepts a fresh selection captured by the context menu', () => {
  const now = Date.now()
  const selection = readPendingSelection({
    text: '  Architecture aligns business and IT.  ',
    title: 'Enterprise Architecture',
    capturedAt: now - 500,
  }, now)

  assert.deepEqual(selection, {
    text: 'Architecture aligns business and IT.',
    title: 'Enterprise Architecture',
    capturedAt: now - 500,
  })
})

test('rejects stale context-menu selections', () => {
  const now = Date.now()
  const selection = readPendingSelection({
    text: 'Architecture aligns business and IT.',
    title: 'Enterprise Architecture',
    capturedAt: now - pendingSelectionMaxAgeMs - 1,
  }, now)

  assert.equal(selection, null)
})
