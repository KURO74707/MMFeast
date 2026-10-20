import { characterById } from '../../data/characters';
import { difficultyById } from '../../data/difficulties';
import { weaponById } from '../../data/weapons';
import type { App } from '../app';
import { esc, section, type Screen } from '../router';

// Placeholder for the actual game. Only here to close the lobby → run → lobby loop.

export function runScreen(app: App): Screen {
  const el = section('run');
  let unsubscribe: (() => void) | null = null;

  const render = () => {
    const session = app.session;
    if (!session) return;
    if (session.state.status === 'lobby') {
      app.router.back();
      return;
    }
    const { players, difficultyId } = session.state;
    el.innerHTML = `
      <header class="topbar">
        <h2>Run in progress</h2>
        <span class="muted">Difficulty: ${esc(difficultyById(difficultyId).name)}</span>
        <span class="spacer"></span>
        <button class="btn" data-action="end">End run → Lobby</button>
      </header>
      <div class="box run-area">
        [gameplay — placeholder]
        <ul class="team">
          ${players.map((p) => `<li>${esc(p.name)} — ${esc(characterById(p.characterId).name)} · ${esc(weaponById(p.weaponId).name)}</li>`).join('')}
        </ul>
      </div>`;
  };

  el.addEventListener('click', (e) => {
    if ((e.target as HTMLElement).closest('[data-action="end"]')) app.session?.endRun();
  });

  return {
    id: 'run',
    el,
    enter() {
      unsubscribe = app.session?.onChange(render) ?? null;
      render();
    },
    leave() {
      unsubscribe?.();
      unsubscribe = null;
    },
    onEscape: () => true, // no pause menu yet
  };
}
