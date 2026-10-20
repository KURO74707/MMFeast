// Weapons from the GDD (武器). Effects are from the GDD; stats, upgrades and unlock
// state are PLACEHOLDERS until the design team fills them in.

export interface Weapon {
  id: string;
  name: string;
  nameZh: string;
  effect: string;
  upgrades: string[];
  unlocked: boolean;
  unlockHint: string;
}

const TBD_UPGRADES = ['Upgrade 1 — TBD', 'Upgrade 2 — TBD'];

const weapon = (id: string, name: string, nameZh: string, effect: string, unlocked: boolean): Weapon => ({
  id,
  name,
  nameZh,
  effect,
  upgrades: TBD_UPGRADES,
  unlocked,
  unlockHint: unlocked ? 'Starter weapon' : 'Unlock condition — TBD',
});

export const WEAPONS: Weapon[] = [
  weapon('knife', 'Knife', '刀', 'Causes bleeding.', true),
  weapon('fork', 'Fork', '叉子', 'Hits weak points.', true),
  weapon('cleaver', 'Cleaver', '菜刀', 'Breaks armor.', true),
  weapon('spatula', 'Spatula', '锅铲', 'Fan-shaped attack.', false),
  weapon('pan', 'Frying Pan', '平底锅', 'Area (AOE) damage.', false),
  weapon('tongs', 'Tongs', '夹子', 'Blocks attacks.', false),
  weapon('board', 'Cutting Board', '砧板', 'Straight-line attack.', false),
  weapon('mallet', 'Meat Mallet', '肉锤', 'Knocks enemies back.', false),
  weapon('claypot', 'Clay Pot', '大圆砂锅', 'Low damage, stuns enemies, reduces damage you take.', false),
];

export const weaponById = (id: string) => WEAPONS.find((w) => w.id === id) ?? WEAPONS[0];
