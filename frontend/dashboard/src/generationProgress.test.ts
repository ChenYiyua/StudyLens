import assert from 'node:assert/strict'
import test from 'node:test'

import { generationProgress } from './generationProgress.ts'

test('generation progress advances without inventing a percentage', () => {
  const starting = generationProgress('lecture', 0)
  const generating = generationProgress('lecture', 2000)

  assert.equal(starting.steps[0].state, 'active')
  assert.equal(generating.steps[0].state, 'done')
  assert.equal(generating.steps[2].state, 'active')
  assert.equal(generating.steps[3].state, 'pending')
})

test('each structured workflow names its validation boundary', () => {
  assert.match(generationProgress('practice', 2000).steps[3].label, /schema/i)
  assert.match(generationProgress('grade', 2000).steps[3].label, /save/i)
})
