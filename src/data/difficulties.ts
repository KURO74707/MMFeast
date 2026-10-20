// PLACEHOLDER difficulty tiers. Names and descriptions are for layout only.
export interface Difficulty {
  id: string;
  name: string;
  description: string;
}

export const DIFFICULTIES: Difficulty[] = [
  { id: 'easy', name: 'Easy', description: 'Difficulty description — TBD' },
  { id: 'normal', name: 'Normal', description: 'Difficulty description — TBD' },
  { id: 'hard', name: 'Hard', description: 'Difficulty description — TBD' },
];

export const difficultyById = (id: string) => DIFFICULTIES.find((d) => d.id === id) ?? DIFFICULTIES[1];
