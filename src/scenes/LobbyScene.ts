import Phaser from 'phaser';
import { characterById } from '../data/characters';
import { FORMATIONS, GROUND_Y, VAN, type Spot } from '../data/lobbyLayout';
import { weaponById } from '../data/weapons';
import type { SessionPlayer } from '../session/session';
import type { App } from '../ui/app';
import { STAGE } from '../config';

// Live side view of the van and everyone in the session. Mid-fi gray shapes only.
// Redraws the people whenever the session changes; the van and ground are static.

const C = {
  bg: 0xe6e6e6,
  ground: 0xcfcfcf,
  van: 0xbdbdbd,
  vanDark: 0x9a9a9a,
  opening: 0x8a8a8a,
  line: 0x5a5a5a,
  figure: 0xf5f5f5,
  figureYou: 0xffffff,
  text: '#2b2b2b',
  muted: '#6b6b6b',
};

const FONT = 'system-ui, sans-serif';

export class LobbyScene extends Phaser.Scene {
  private people!: Phaser.GameObjects.Container;
  private signature = '';

  constructor(private app: App) {
    super('lobby');
  }

  create(): void {
    this.signature = '';
    this.drawSet();
    this.people = this.add.container(0, 0);
  }

  update(): void {
    const players = this.app.session?.state.players ?? [];
    const sig = JSON.stringify(players);
    if (sig === this.signature) return;
    this.signature = sig;
    this.drawPeople(players);
  }

  // ---- static set -------------------------------------------------------

  private drawSet(): void {
    const g = this.add.graphics();
    g.fillStyle(C.bg).fillRect(0, 0, STAGE.width, STAGE.height);
    g.fillStyle(C.ground).fillRect(0, GROUND_Y, STAGE.width, STAGE.height - GROUND_Y);
    g.lineStyle(2, C.line).lineBetween(0, GROUND_Y, STAGE.width, GROUND_Y);

    // Van body
    g.fillStyle(C.van).fillRoundedRect(VAN.x, VAN.y, VAN.width, VAN.height - 20, { tl: 16, tr: 60, bl: 6, br: 6 });
    g.lineStyle(2, C.line).strokeRoundedRect(VAN.x, VAN.y, VAN.width, VAN.height - 20, { tl: 16, tr: 60, bl: 6, br: 6 });

    // Door opening + steps
    const doorTop = VAN.y + 40;
    const doorBottom = GROUND_Y - 30;
    g.fillStyle(C.opening).fillRect(VAN.door.x, doorTop, VAN.door.width, doorBottom - doorTop);
    g.lineStyle(2, C.line).strokeRect(VAN.door.x, doorTop, VAN.door.width, doorBottom - doorTop);
    g.fillStyle(C.vanDark);
    g.fillRect(VAN.door.x - 10, doorBottom, VAN.door.width + 20, 14);
    g.fillRect(VAN.door.x - 20, doorBottom + 14, VAN.door.width + 40, 16);
    g.lineStyle(2, C.line);
    g.strokeRect(VAN.door.x - 10, doorBottom, VAN.door.width + 20, 14);
    g.strokeRect(VAN.door.x - 20, doorBottom + 14, VAN.door.width + 40, 16);

    // Serving window
    const w = VAN.window;
    g.fillStyle(C.opening).fillRect(w.x, w.y, w.width, w.height);
    g.lineStyle(2, C.line).strokeRect(w.x, w.y, w.width, w.height);
    g.fillStyle(C.vanDark).fillRect(w.x - 10, w.y + w.height, w.width + 20, 10); // counter

    // Wheels
    for (const x of VAN.wheels) {
      g.fillStyle(C.vanDark).fillCircle(x, GROUND_Y - 22, 34);
      g.lineStyle(2, C.line).strokeCircle(x, GROUND_Y - 22, 34);
      g.fillStyle(C.van).fillCircle(x, GROUND_Y - 22, 12);
    }

    this.label(VAN.x + VAN.width - 120, VAN.y + 18, '[ van ]', C.muted, 13);
  }

  // ---- people -----------------------------------------------------------

  private drawPeople(players: SessionPlayer[]): void {
    this.people.removeAll(true);
    const spots = FORMATIONS[Math.min(players.length, 4)] ?? [];
    players.forEach((p, i) => {
      const spot = spots[i];
      if (spot) this.people.add(this.figure(p, spot));
    });
  }

  private figure(p: SessionPlayer, spot: Spot): Phaser.GameObjects.Container {
    const g = this.add.graphics();
    const fill = p.isYou ? C.figureYou : C.figure;
    const stroke = p.isYou ? 3 : 2;
    const items: Phaser.GameObjects.GameObject[] = [g];
    let headY: number;
    let handX: number;
    let handY: number;

    if (spot.kind === 'sit') {
      // Seated: short body, legs out to the side
      g.fillStyle(fill).fillRoundedRect(-30, -70, 60, 70, 20);
      g.fillRoundedRect(-30, -18, 90, 18, 8);
      g.lineStyle(stroke, C.line).strokeRoundedRect(-30, -70, 60, 70, 20);
      g.strokeRoundedRect(-30, -18, 90, 18, 8);
      headY = -100;
      handX = -42;
      handY = -50;
    } else if (spot.kind === 'window') {
      // Only head and shoulders show above the counter
      g.fillStyle(fill).fillRoundedRect(-34, -40, 68, 40, { tl: 18, tr: 18, bl: 0, br: 0 });
      g.lineStyle(stroke, C.line).strokeRoundedRect(-34, -40, 68, 40, { tl: 18, tr: 18, bl: 0, br: 0 });
      headY = -66;
      handX = 46;
      handY = -30;
    } else {
      // Standing (ground or doorway)
      g.fillStyle(fill).fillRoundedRect(-30, -110, 60, 110, 24);
      g.lineStyle(stroke, C.line).strokeRoundedRect(-30, -110, 60, 110, 24);
      headY = -140;
      handX = 42;
      handY = -70;
    }

    // Head
    g.fillStyle(fill).fillCircle(0, headY, 30);
    g.lineStyle(stroke, C.line).strokeCircle(0, headY, 30);

    // Weapon: a labelled box held at the side
    g.fillStyle(C.vanDark).fillRect(handX - 6, handY - 28, 12, 56);
    g.lineStyle(1, C.line).strokeRect(handX - 6, handY - 28, 12, 56);

    const character = characterById(p.characterId);
    const weapon = weaponById(p.weaponId);
    items.push(this.label(0, headY, character.name, C.text, 13, true));
    items.push(this.label(handX + (handX > 0 ? 10 : -10), handY + 36, weapon.name, C.muted, 11, false, handX > 0 ? 0 : 1));

    // Name tag over the head
    const status = p.isHost ? 'Host' : p.ready ? '✓ Ready' : 'Not ready';
    const tag = `${p.name}${p.isYou ? ' (you)' : ''}\n${status}`;
    items.push(
      this.label(0, headY - 56, tag, C.text, 13, true)
        .setAlign('center')
        .setBackgroundColor('#ffffff')
        .setPadding(8, 4, 8, 4),
    );

    return this.add.container(spot.x, spot.y, items);
  }

  private label(x: number, y: number, text: string, color: string, size: number, bold = false, originX = 0.5) {
    return this.add
      .text(x, y, text, { fontFamily: FONT, fontSize: `${size}px`, color, fontStyle: bold ? '600' : 'normal' })
      .setOrigin(originX, 0.5);
  }
}
