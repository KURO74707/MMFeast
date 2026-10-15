// Tiny screen switcher. Each screen is a full-stage <section> inside #ui.
// Esc closes an open modal first, otherwise asks the screen, otherwise goes back.

export interface Screen {
  id: string;
  el: HTMLElement;
  enter?(): void;
  leave?(): void;
  /** Return true if the screen handled Esc itself. */
  onEscape?(): boolean;
}

export class Router {
  private screens = new Map<string, Screen>();
  private stack: string[] = [];
  private modal: HTMLElement | null = null;

  constructor(private root: HTMLElement) {
    window.addEventListener('keydown', (e) => {
      if (e.code !== 'Escape' || e.repeat) return;
      if (this.modal) this.closeModal();
      else if (!this.currentScreen()?.onEscape?.()) this.back();
    });
  }

  register(screen: Screen): void {
    screen.el.classList.add('screen');
    screen.el.hidden = true;
    this.root.appendChild(screen.el);
    this.screens.set(screen.id, screen);
  }

  /** Go to a screen. `replace` swaps the current one instead of stacking (no Back to it). */
  go(id: string, opts: { replace?: boolean; reset?: boolean } = {}): void {
    this.closeModal();
    const current = this.currentScreen();
    if (current) {
      current.leave?.();
      current.el.hidden = true;
    }
    if (opts.reset) this.stack = [];
    else if (opts.replace) this.stack.pop();
    this.stack.push(id);
    const next = this.screens.get(id)!;
    next.el.hidden = false;
    next.enter?.();
    next.el.querySelector<HTMLElement>('[autofocus]')?.focus();
  }

  back(): void {
    if (this.stack.length < 2) return;
    const current = this.currentScreen()!;
    current.leave?.();
    current.el.hidden = true;
    this.stack.pop();
    const prev = this.currentScreen()!;
    prev.el.hidden = false;
    prev.enter?.();
  }

  /** Show a modal dialog. Clicks on [data-close] or the backdrop close it. */
  openModal(content: HTMLElement): void {
    this.closeModal();
    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop';
    content.classList.add('modal');
    backdrop.appendChild(content);
    backdrop.addEventListener('click', (e) => {
      if (e.target === backdrop || (e.target as HTMLElement).closest('[data-close]')) this.closeModal();
    });
    this.root.appendChild(backdrop);
    this.modal = backdrop;
    content.querySelector<HTMLElement>('[autofocus]')?.focus();
  }

  closeModal(): void {
    this.modal?.remove();
    this.modal = null;
  }

  private currentScreen(): Screen | undefined {
    return this.screens.get(this.stack[this.stack.length - 1]);
  }
}

/** Escape text for use inside innerHTML templates. */
export function esc(s: string): string {
  return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);
}

export function section(id: string): HTMLElement {
  const el = document.createElement('section');
  el.id = `screen-${id}`;
  return el;
}
