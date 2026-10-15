import type { App } from '../app';
import { section, type Screen } from '../router';

export function homeScreen(app: App): Screen {
  const el = section('home');
  el.innerHTML = `
    <div class="home">
      <div class="box logo">Meow Meow Feast<small>[logo / key art]</small></div>
      <nav class="menu">
        <button class="btn primary" data-action="create" autofocus>Create Session</button>
        <button class="btn" data-action="join">Join Session</button>
        <button class="btn" data-action="collection">Collection</button>
        <button class="btn" data-action="settings">Settings</button>
      </nav>
      <div class="version">Prototype · v0.0.1</div>
    </div>`;

  el.addEventListener('click', (e) => {
    const action = (e.target as HTMLElement).closest<HTMLElement>('[data-action]')?.dataset.action;
    if (action === 'create') app.createSession();
    if (action === 'join') openJoinModal(app);
    if (action === 'collection') app.router.go('collection');
    if (action === 'settings') app.router.go('settings');
  });

  return { id: 'home', el };
}

function openJoinModal(app: App): void {
  const modal = document.createElement('form');
  modal.innerHTML = `
    <h3>Join Session</h3>
    <p class="muted">Enter the code your host shared with you.</p>
    <input class="input code-input" name="code" maxlength="4" placeholder="ABCD" autocomplete="off" autofocus />
    <p class="error" hidden>Codes are 4 letters.</p>
    <div class="row end">
      <button type="button" class="btn" data-close>Cancel</button>
      <button type="submit" class="btn primary">Join</button>
    </div>`;
  const input = modal.querySelector('input')!;
  const error = modal.querySelector<HTMLElement>('.error')!;
  input.addEventListener('input', () => (input.value = input.value.toUpperCase().replace(/[^A-Z]/g, '')));
  modal.addEventListener('submit', (e) => {
    e.preventDefault();
    if (input.value.length !== 4) {
      error.hidden = false;
      return;
    }
    app.joinSession(input.value);
  });
  app.router.openModal(modal);
}
