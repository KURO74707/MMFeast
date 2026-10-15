// An online session (lobby + run). Screens only talk to the `Session` interface, so the
// fake in-memory version below can later be swapped for real networking.

import { CHARACTERS } from '../data/characters';
import { WEAPONS } from '../data/weapons';

export const MIN_PLAYERS = 2; // no solo yet
export const MAX_PLAYERS = 4;

export interface SessionPlayer {
  id: string;
  name: string;
  characterId: string;
  weaponId: string;
  ready: boolean; // the host is always considered ready
  isHost: boolean;
  isYou: boolean;
}

export interface SessionState {
  code: string;
  players: SessionPlayer[];
  difficultyId: string;
  status: 'lobby' | 'running';
}

export interface Session {
  readonly state: SessionState;
  me(): SessionPlayer;
  onChange(fn: () => void): () => void;
  setCharacter(id: string): void;
  setWeapon(id: string): void;
  setReady(ready: boolean): void;
  setDifficulty(id: string): void; // host only
  canStart(): { ok: boolean; reason: string };
  start(): void; // host only
  endRun(): void;
  leave(): void;
}

// ---- fake implementation --------------------------------------------------

const firstUnlocked = <T extends { id: string; unlocked: boolean }>(list: T[]) => list.find((x) => x.unlocked)!.id;
const randomUnlocked = <T extends { id: string; unlocked: boolean }>(list: T[]) => {
  const options = list.filter((x) => x.unlocked);
  return options[Math.floor(Math.random() * options.length)].id;
};

export class FakeSession implements Session {
  readonly state: SessionState;
  private listeners = new Set<() => void>();
  private nextId = 1;

  private constructor(code: string) {
    this.state = { code, players: [], difficultyId: 'normal', status: 'lobby' };
  }

  /** You create the session and are the host. */
  static host(): FakeSession {
    const s = new FakeSession(randomCode());
    s.addPlayer({ name: 'Player 1', isHost: true, isYou: true, characterId: firstUnlocked(CHARACTERS), weaponId: firstUnlocked(WEAPONS) });
    return s;
  }

  /** You join someone else's session with a code; a fake host is already there. */
  static join(code: string): FakeSession {
    const s = new FakeSession(code.toUpperCase());
    s.addPlayer({ name: 'Player 1', isHost: true, isYou: false, characterId: randomUnlocked(CHARACTERS), weaponId: randomUnlocked(WEAPONS) });
    s.addPlayer({ name: 'Player 2', isHost: false, isYou: true, characterId: firstUnlocked(CHARACTERS), weaponId: firstUnlocked(WEAPONS) });
    return s;
  }

  me(): SessionPlayer {
    return this.state.players.find((p) => p.isYou)!;
  }

  onChange(fn: () => void): () => void {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }

  setCharacter(id: string): void {
    this.me().characterId = id;
    this.emit();
  }

  setWeapon(id: string): void {
    this.me().weaponId = id;
    this.emit();
  }

  setReady(ready: boolean): void {
    this.me().ready = ready;
    this.emit();
  }

  setDifficulty(id: string): void {
    if (!this.me().isHost) return;
    this.state.difficultyId = id;
    this.emit();
  }

  canStart(): { ok: boolean; reason: string } {
    const { players } = this.state;
    if (players.length < MIN_PLAYERS) return { ok: false, reason: `Need at least ${MIN_PLAYERS} players` };
    const waiting = players.filter((p) => !p.isHost && !p.ready).length;
    if (waiting > 0) return { ok: false, reason: `Waiting for ${waiting} player${waiting > 1 ? 's' : ''} to ready up` };
    return { ok: true, reason: 'Everyone is ready' };
  }

  start(): void {
    if (!this.me().isHost || !this.canStart().ok) return;
    this.state.status = 'running';
    this.emit();
  }

  endRun(): void {
    this.state.status = 'lobby';
    for (const p of this.state.players) p.ready = false;
    this.emit();
  }

  leave(): void {
    this.listeners.clear();
  }

  // ---- debug controls (simulate teammates) -------------------------------

  addFakePlayer(): void {
    if (this.state.players.length >= MAX_PLAYERS) return;
    const taken = new Set(this.state.players.map((p) => p.name));
    let n = 1;
    while (taken.has(`Player ${n}`)) n++;
    this.addPlayer({ name: `Player ${n}`, isHost: false, isYou: false, characterId: randomUnlocked(CHARACTERS), weaponId: randomUnlocked(WEAPONS) });
    this.emit();
  }

  removeFakePlayer(id: string): void {
    this.state.players = this.state.players.filter((p) => p.id !== id || p.isYou || p.isHost);
    this.emit();
  }

  toggleFakeReady(id: string): void {
    const p = this.state.players.find((pl) => pl.id === id);
    if (!p || p.isYou || p.isHost) return;
    p.ready = !p.ready;
    this.emit();
  }

  /** When you're a guest: pretend the host pressed Start. */
  hostStarts(): void {
    if (this.me().isHost || !this.canStart().ok) return;
    this.state.status = 'running';
    this.emit();
  }

  private addPlayer(p: Omit<SessionPlayer, 'id' | 'ready'>): void {
    this.state.players.push({ ...p, id: `p${this.nextId++}`, ready: false });
  }

  private emit(): void {
    for (const fn of this.listeners) fn();
  }
}

function randomCode(): string {
  const letters = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
  let code = '';
  for (let i = 0; i < 4; i++) code += letters[Math.floor(Math.random() * letters.length)];
  return code;
}
