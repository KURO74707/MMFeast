import type Phaser from 'phaser';
import { FakeSession } from '../session/session';
import { Router } from './router';

// Shared state every screen can reach.
export class App {
  router: Router;
  game: Phaser.Game | null = null; // set once the game is created; screens use it to show scenes
  session: FakeSession | null = null;
  private sessionListeners = new Set<(s: FakeSession | null) => void>();

  constructor(root: HTMLElement) {
    this.router = new Router(root);
  }

  /** Called whenever a session is created, joined or left. */
  onSessionChange(fn: (s: FakeSession | null) => void): void {
    this.sessionListeners.add(fn);
  }

  createSession(): void {
    this.setSession(FakeSession.host());
    this.router.go('lobby');
  }

  joinSession(code: string): void {
    this.setSession(FakeSession.join(code));
    this.router.go('lobby');
  }

  leaveSession(): void {
    this.session?.leave();
    this.setSession(null);
    this.router.go('home', { reset: true });
  }

  private setSession(s: FakeSession | null): void {
    this.session = s;
    for (const fn of this.sessionListeners) fn(s);
  }
}
