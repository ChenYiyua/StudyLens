import assert from 'node:assert/strict'
import test from 'node:test'
import { parseExtensionHandoff } from './extensionHandoff.ts'

test('parses an extension handoff and keeps an available course', () => {
  const handoff = parseExtensionHandoff(
    '#course=eam&question=What+is+an+architecture+viewpoint%3F&source=Lecture+notes&from=extension',
    ['eam', 'demo'],
  )

  assert.deepEqual(handoff, {
    courseId: 'eam',
    question: 'What is an architecture viewpoint?',
    source: 'Lecture notes',
  })
})

test('ignores invalid handoffs and drops unavailable course identifiers', () => {
  assert.equal(parseExtensionHandoff('#from=dashboard&question=hello', ['eam']), null)
  assert.equal(parseExtensionHandoff('#from=extension&question=+', ['eam']), null)
  assert.equal(
    parseExtensionHandoff('#from=extension&course=private&question=hello&source=', ['eam'])?.courseId,
    '',
  )
})
