// Where people appear in the lobby scene. Coordinates are on the 1280×720 stage and
// mark a figure's feet (for 'window', the bottom of the window).
// Players fill spots in join order; edit freely to try other arrangements.

export type SpotKind = 'door' | 'window' | 'stand' | 'sit';

export interface Spot {
  kind: SpotKind;
  x: number;
  y: number;
}

export const GROUND_Y = 440;

// The van, drawn on the left of the scene.
export const VAN = {
  x: 70,
  y: 170,
  width: 640,
  height: 270,
  door: { x: 120, width: 130 }, // opening in the side, with steps at the bottom
  window: { x: 360, y: 205, width: 240, height: 120 }, // serving window
  wheels: [350, 610], // wheel centre x positions
};

const DOOR: Spot = { kind: 'door', x: VAN.door.x + VAN.door.width / 2, y: GROUND_Y - 30 };
const WINDOW: Spot = { kind: 'window', x: VAN.window.x + VAN.window.width / 2, y: VAN.window.y + VAN.window.height };

export const FORMATIONS: Record<number, Spot[]> = {
  1: [DOOR],
  2: [DOOR, { kind: 'stand', x: 950, y: GROUND_Y }],
  3: [DOOR, { kind: 'stand', x: 880, y: GROUND_Y }, { kind: 'sit', x: 1090, y: GROUND_Y }],
  4: [DOOR, WINDOW, { kind: 'stand', x: 880, y: GROUND_Y }, { kind: 'sit', x: 1090, y: GROUND_Y }],
};
