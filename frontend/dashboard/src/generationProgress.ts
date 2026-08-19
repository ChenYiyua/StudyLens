export type GenerationStage = 'context' | 'lecture' | 'exercise' | 'practice' | 'grade'

export interface GenerationStep {
  label: string
  state: 'done' | 'active' | 'pending'
}

const stageLabels: Record<GenerationStage, { title: string; steps: [string, string, string, string] }> = {
  context: {
    title: 'Explaining selected web context',
    steps: ['Validate course context', 'Retrieve cited passages', 'Generate grounded explanation', 'Attach auditable sources'],
  },
  lecture: {
    title: 'Building your lecture lesson',
    steps: ['Resolve selected lecture', 'Retrieve representative pages', 'Generate a coherent lesson', 'Attach auditable sources'],
  },
  exercise: {
    title: 'Working through the course exercise',
    steps: ['Pair exercise and solution', 'Retrieve both evidence sets', 'Generate the walkthrough', 'Attach auditable sources'],
  },
  practice: {
    title: 'Creating your knowledge check',
    steps: ['Resolve the selected material', 'Retrieve cited passages', 'Generate structured questions', 'Validate question schema'],
  },
  grade: {
    title: 'Comparing your answer with the course',
    steps: ['Validate your submission', 'Retrieve cited passages', 'Generate formative feedback', 'Validate and save the attempt'],
  },
}

export function generationProgress(stage: GenerationStage, elapsedMilliseconds: number) {
  const activeIndex = elapsedMilliseconds < 700 ? 0 : elapsedMilliseconds < 1500 ? 1 : 2
  const definition = stageLabels[stage]
  return {
    title: definition.title,
    steps: definition.steps.map((label, index): GenerationStep => ({
      label,
      state: index < activeIndex ? 'done' : index === activeIndex ? 'active' : 'pending',
    })),
  }
}
