import type { FakeSession } from '../session/session';
import type { App } from './app';

// Floating panel outside the game stage that fakes other players, since there is
// no real networking yet. Only shown while you're in a session.

export function mountDebugPanel(app: App): void {
  const el = document.createElement('aside');
  el.id = 'debug';
  el.hidden = true;
  document.body.appendChild(el);

  let session: FakeSession | null = null;
  let unsubscribe: (() => void) | null = null;

  const render = () => {
    if (!session) return;
    const fakes = session.state.players.filter((p) => !p.isYou && !p.isHost);
    const youAreGuest = !session.me().isHost;
    const running = session.state.status === 'running';
    el.innerHTML = `
      <div class="debug-title">DEBUG · simulate teammates</div>
      <button data-debug="add" ${session.state.players.length >= 4 || running ? 'disabled' : ''}>+ Add teammate</button>
      ${fakes
        .map(
          (p) => `
        <div class="debug-row">
          <span>${p.name}</span>
          <button data-debug="ready" data-id="${p.id}" ${running ? 'disabled' : ''}>${p.ready ? 'Unready' : 'Ready'}</button>
          <button data-debug="remove" data-id="${p.id}" ${running ? 'disabled' : ''}>Remove</button>
        </div>`,
        )
        .join('')}
      ${youAreGuest && !running ? `<button data-debug="host-start" ${session.canStart().ok ? '' : 'disabled'}>Host presses Start</button>` : ''}`;
  };

  el.addEventListener('click', (e) => {
    const btn = (e.target as HTMLElement).closest<HTMLButtonElement>('[data-debug]');
    if (!btn || !session) return;
    const id = btn.dataset.id!;
    if (btn.dataset.debug === 'add') session.addFakePlayer();
    if (btn.dataset.debug === 'ready') session.toggleFakeReady(id);
    if (btn.dataset.debug === 'remove') session.removeFakePlayer(id);
    if (btn.dataset.debug === 'host-start') session.hostStarts();
  });

  app.onSessionChange((s) => {
    unsubscribe?.();
    session = s;
    el.hidden = !s;
    unsubscribe = s ? s.onChange(render) : null;
    render();
  });
}
