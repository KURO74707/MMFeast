import { CHARACTERS, type Character } from '../../data/characters';
import { WEAPONS, type Weapon } from '../../data/weapons';
import type { App } from '../app';
import { esc, section, type Screen } from '../router';

// Collection: vertical list on the left, the selected item spotlighted in the
// middle, description + entrance to its upgrades on the right.

type Tab = 'characters' | 'weapons';

// Shared with the upgrades screen so it knows what it is showing.
const state: { tab: Tab; selected: Record<Tab, string> } = {
  tab: 'characters',
  selected: { characters: CHARACTERS[0].id, weapons: WEAPONS[0].id },
};

const currentItem = (): Character | Weapon =>
  state.tab === 'characters'
    ? CHARACTERS.find((c) => c.id === state.selected.characters)!
    : WEAPONS.find((w) => w.id === state.selected.weapons)!;

export function collectionScreen(app: App): Screen {
  const el = section('collection');

  const render = () => {
    const items = state.tab === 'characters' ? CHARACTERS : WEAPONS;
    const item = currentItem();
    const unlocked = items.filter((i) => i.unlocked).length;
    el.innerHTML = `
      <header class="topbar">
        <button class="btn" data-action="back">← Back</button>
        <h2>Collection</h2>
      </header>
      <div class="tabs">
        <button class="tab ${state.tab === 'characters' ? 'active' : ''}" data-tab="characters">Characters</button>
        <button class="tab ${state.tab === 'weapons' ? 'active' : ''}" data-tab="weapons">Weapons</button>
        <span class="tabs-meta">${unlocked} / ${items.length} unlocked</span>
      </div>
      <div class="collection">
        <ul class="item-list">
          ${items.map((i) => listItem(i, i.id === item.id)).join('')}
        </ul>
        <div class="spotlight ${item.unlocked ? '' : 'locked'}">
          <div class="spotlight-model">
            ${item.unlocked ? `[${esc(item.name)} — ${state.tab === 'characters' ? 'character' : 'weapon'} model]` : '🔒<br />[silhouette]'}
          </div>
          <div class="spotlight-floor"></div>
          <div class="spotlight-name">${esc(item.name)} <span class="muted">${esc(item.nameZh)}</span></div>
        </div>
        <div class="box panel detail">
          ${state.tab === 'characters' ? characterDetail(item as Character) : weaponDetail(item as Weapon)}
          <span class="spacer"></span>
          <button class="btn primary big" data-action="upgrades">Upgrades →</button>
        </div>
      </div>`;
  };

  el.addEventListener('click', (e) => {
    const target = e.target as HTMLElement;
    const action = target.closest<HTMLElement>('[data-action]')?.dataset.action;
    if (action === 'back') return app.router.back();
    if (action === 'upgrades') return app.router.go('upgrades');
    const tab = target.closest<HTMLElement>('[data-tab]')?.dataset.tab as Tab | undefined;
    if (tab) state.tab = tab;
    const id = target.closest<HTMLElement>('[data-item]')?.dataset.item;
    if (id) state.selected[state.tab] = id;
    if (tab || id) render();
  });

  return { id: 'collection', el, enter: render };
}

/** Placeholder — upgrades haven't been designed yet. */
export function upgradesScreen(app: App): Screen {
  const el = section('upgrades');
  el.addEventListener('click', (e) => {
    if ((e.target as HTMLElement).closest('[data-action="back"]')) app.router.back();
  });
  return {
    id: 'upgrades',
    el,
    enter() {
      const item = currentItem();
      el.innerHTML = `
        <header class="topbar">
          <button class="btn" data-action="back">← Back</button>
          <h2>${esc(item.name)} · Upgrades</h2>
        </header>
        <div class="box placeholder">[Upgrades — to be designed]</div>`;
    },
  };
}

function listItem(item: Character | Weapon, active: boolean): string {
  return `
    <li>
      <button class="list-item ${active ? 'active' : ''} ${item.unlocked ? '' : 'locked'}" data-item="${item.id}">
        <span class="box thumb">${item.unlocked ? '' : '🔒'}</span>
        <span>${esc(item.name)}</span>
      </button>
    </li>`;
}

function status(item: Character | Weapon): string {
  return item.unlocked
    ? `<span class="pill">Unlocked</span>`
    : `<span class="pill locked">🔒 Locked</span> <span class="muted small">${esc(item.unlockHint)}</span>`;
}

function list(title: string, rows: string[]): string {
  return `<h4>${title}</h4><ul class="list">${rows.map((r) => `<li class="box">${esc(r)}</li>`).join('')}</ul>`;
}

function characterDetail(c: Character): string {
  return `
    <h3>${esc(c.name)} <span class="muted">${esc(c.nameZh)}</span></h3>
    <div>${status(c)}</div>
    <h4>Perk</h4>
    <div class="box perk"><b>${esc(c.perk.name)}</b> <span class="muted">${esc(c.perk.nameZh)}</span><p>${esc(c.perk.description)}</p></div>
    ${list('Abilities', c.abilities)}`;
}

function weaponDetail(w: Weapon): string {
  return `
    <h3>${esc(w.name)} <span class="muted">${esc(w.nameZh)}</span></h3>
    <div>${status(w)}</div>
    <h4>Effect</h4>
    <div class="box perk"><p>${esc(w.effect)}</p></div>
    <h4>Stats</h4>
    <div class="stats">
      <div class="box">Damage<br />TBD</div>
      <div class="box">Speed<br />TBD</div>
      <div class="box">Range<br />TBD</div>
    </div>`;
}
