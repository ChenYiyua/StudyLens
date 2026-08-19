import assert from 'node:assert/strict'
import test from 'node:test'
import { createStudyLensUrl, maximumSelectionLength, normaliseSelection } from './handoff.ts'

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
