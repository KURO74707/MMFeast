import type { App } from '../app';
import { section, type Screen } from '../router';

// Layout only — none of these controls are wired up yet.

const TABS = ['Audio', 'Display', 'Controls', 'Language'] as const;

const slider = (label: string, value: number) =>
  `<label class="setting"><span>${label}</span><input type="range" min="0" max="100" value="${value}" /><output>${value}</output></label>`;
const toggle = (label: string, on: boolean) =>
  `<label class="setting"><span>${label}</span><input type="checkbox" ${on ? 'checked' : ''} /></label>`;

const PANELS: Record<(typeof TABS)[number], string> = {
  Audio: slider('Master volume', 80) + slider('Music', 70) + slider('Sound effects', 80),
  Display: toggle('Fullscreen', false) + toggle('Screen shake', true) + toggle('Show damage numbers', true),
  Controls: `
    <table class="bindings">
      <tr><th>Action</th><th>Key</th><th>Gamepad</th></tr>
      <tr><td>Move</td><td>WASD</td><td>Left stick</td></tr>
      <tr><td>Dash</td><td>Shift</td><td>B</td></tr>
      <tr><td>Attack</td><td>Left click</td><td>X</td></tr>
      <tr><td>Interact / pick up</td><td>E</td><td>A</td></tr>
      <tr><td>Pause</td><td>Esc</td><td>Start</td></tr>
    </table>
    <p class="muted">Bindings are placeholders.</p>`,
  Language: `
    <label class="setting"><span>English</span><input type="radio" name="lang" checked /></label>
    <label class="setting"><span>中文</span><input type="radio" name="lang" /></label>`,
};

export function settingsScreen(app: App): Screen {
  const el = section('settings');
  let tab: (typeof TABS)[number] = 'Audio';

  const render = () => {
    el.innerHTML = `
      <header class="topbar">
        <button class="btn" data-action="back">← Back</button>
        <h2>Settings</h2>
      </header>
      <div class="tabs">
        ${TABS.map((t) => `<button class="tab ${t === tab ? 'active' : ''}" data-tab="${t}">${t}</button>`).join('')}
      </div>
      <div class="box panel settings-panel">${PANELS[tab]}</div>`;
  };

  el.addEventListener('click', (e) => {
    const target = e.target as HTMLElement;
    if (target.closest('[data-action="back"]')) app.router.back();
    const t = target.closest<HTMLElement>('[data-tab]')?.dataset.tab as (typeof TABS)[number] | undefined;
    if (t) {
      tab = t;
      render();
    }
  });
  el.addEventListener('input', (e) => {
    const input = e.target as HTMLInputElement;
    if (input.type === 'range') input.nextElementSibling!.textContent = input.value;
  });

  render();
  return { id: 'settings', el };
}
