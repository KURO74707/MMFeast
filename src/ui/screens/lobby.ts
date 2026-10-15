import { CHARACTERS, characterById } from '../../data/characters';
import { DIFFICULTIES, difficultyById } from '../../data/difficulties';
import { WEAPONS, weaponById } from '../../data/weapons';
import { MAX_PLAYERS } from '../../session/session';
import type { App } from '../app';
import { esc, section, type Screen } from '../router';

export function lobbyScreen(app: App): Screen {
  const el = section('lobby');
  let unsubscribe: (() => void) | null = null;

  const render = () => {
    const session = app.session;
    if (!session) return;
    if (session.state.status === 'running') {
      app.router.go('run');
      return;
    }
    const me = session.me();
    const { players, code, difficultyId } = session.state;
    const myChar = characterById(me.characterId);
    const myWeapon = weaponById(me.weaponId);
    const start = session.canStart();
    const difficulty = difficultyById(difficultyId);

    el.innerHTML = `
      <header class="topbar lobby-top">
        <button class="btn" data-action="leave">← Leave</button>
        <h2>Session</h2>
        <div class="session-code">Code <b>${esc(code)}</b></div>
        <button class="btn" data-action="invite">Invite</button>
        <span class="spacer"></span>
        <span class="muted">${players.length} / ${MAX_PLAYERS} players</span>
      </header>

      <!-- The live van scene (LobbyScene, drawn in the game canvas) shows through here -->
      <div class="scene-window"></div>

      <div class="lobby-strip">
        <div class="box panel loadout">
          <div class="picker-row">
            <h4>Character</h4>
            <div class="picker">
              ${CHARACTERS.map((c) => option('character', c.id, c.name, c.unlocked, c.id === me.characterId)).join('')}
            </div>
          </div>
          <div class="picker-row">
            <h4>Weapon</h4>
            <div class="picker">
              ${WEAPONS.map((w) => option('weapon', w.id, w.name, w.unlocked, w.id === me.weaponId)).join('')}
            </div>
          </div>
          <div class="summary">
            <div><b>Perk · ${esc(myChar.perk.name)}</b> — ${esc(myChar.perk.description)}</div>
            <div><b>Weapon · ${esc(myWeapon.name)}</b> — ${esc(myWeapon.effect)}</div>
          </div>
        </div>

        <div class="box panel start-panel">
          <h4>Difficulty ${me.isHost ? '' : '<span class="muted">· host picks</span>'}</h4>
          <div class="segmented">
            ${DIFFICULTIES.map(
              (d) =>
                `<button class="seg ${d.id === difficultyId ? 'active' : ''}" data-difficulty="${d.id}" ${me.isHost ? '' : 'disabled'}>${esc(d.name)}</button>`,
            ).join('')}
          </div>
          <p class="muted small">${esc(difficulty.description)}</p>
          <span class="spacer"></span>
          ${
            me.isHost
              ? `<p class="status">${esc(start.reason)}</p>
                 <button class="btn primary big" data-action="start" ${start.ok ? '' : 'disabled'}>Start Game</button>`
              : `<p class="status">${me.ready ? 'Waiting for the host to start' : 'Ready up when your loadout is set'}</p>
                 <button class="btn primary big" data-action="ready">${me.ready ? 'Cancel Ready' : 'Ready'}</button>`
          }
        </div>
      </div>`;
  };

  el.addEventListener('click', (e) => {
    const session = app.session;
    if (!session) return;
    const target = e.target as HTMLElement;
    const pick = target.closest<HTMLElement>('[data-pick]');
    if (pick && !pick.hasAttribute('disabled')) {
      if (pick.dataset.pick === 'character') session.setCharacter(pick.dataset.id!);
      else session.setWeapon(pick.dataset.id!);
    }
    const difficulty = target.closest<HTMLElement>('[data-difficulty]')?.dataset.difficulty;
    if (difficulty) session.setDifficulty(difficulty);

    const action = target.closest<HTMLElement>('[data-action]')?.dataset.action;
    if (action === 'leave') confirmLeave(app);
    if (action === 'invite') openInviteModal(app);
    if (action === 'start') session.start();
    if (action === 'ready') session.setReady(!session.me().ready);
  });

  return {
    id: 'lobby',
    el,
    enter() {
      unsubscribe = app.session?.onChange(render) ?? null;
      app.game?.scene.start('lobby');
      render();
    },
    leave() {
      unsubscribe?.();
      unsubscribe = null;
      app.game?.scene.stop('lobby');
    },
    onEscape() {
      confirmLeave(app); // never drop out of a session by accident
      return true;
    },
  };
}

function option(kind: 'character' | 'weapon', id: string, name: string, unlocked: boolean, active: boolean): string {
  return `
    <button class="option ${active ? 'active' : ''}" data-pick="${kind}" data-id="${id}" ${unlocked ? '' : 'disabled'} title="${unlocked ? esc(name) : 'Locked'}">
      <div class="box art">${unlocked ? '' : '🔒'}</div>
      <span>${esc(name)}</span>
    </button>`;
}

function openInviteModal(app: App): void {
  const code = app.session!.state.code;
  const link = `${location.origin}${location.pathname}?join=${code}`;
  const modal = document.createElement('div');
  modal.innerHTML = `
    <h3>Invite teammates</h3>
    <p class="muted">Share the code or the link. Up to ${MAX_PLAYERS} players.</p>
    <div class="box invite-code">${esc(code)}</div>
    <div class="row">
      <input class="input" value="${esc(link)}" readonly />
      <button class="btn" data-copy>Copy link</button>
    </div>
    <div class="row end"><button class="btn primary" data-close autofocus>Done</button></div>`;
  modal.querySelector('[data-copy]')!.addEventListener('click', async (e) => {
    const btn = e.currentTarget as HTMLButtonElement;
    try {
      await navigator.clipboard.writeText(link);
      btn.textContent = 'Copied!';
    } catch {
      btn.textContent = 'Copy failed';
    }
  });
  app.router.openModal(modal);
}

function confirmLeave(app: App): void {
  const modal = document.createElement('div');
  modal.innerHTML = `
    <h3>Leave session?</h3>
    <p class="muted">${app.session?.me().isHost ? 'You are the host — the session will close for everyone.' : 'You can rejoin with the code.'}</p>
    <div class="row end">
      <button class="btn" data-close autofocus>Stay</button>
      <button class="btn primary" data-leave>Leave</button>
    </div>`;
  modal.querySelector('[data-leave]')!.addEventListener('click', () => app.leaveSession());
  app.router.openModal(modal);
}
