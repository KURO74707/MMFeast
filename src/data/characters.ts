// Characters from the GDD (角色与天赋). Perks are from the GDD; abilities, upgrades
// and unlock state are PLACEHOLDERS until the design team fills them in.

export interface Character {
  id: string;
  name: string;
  nameZh: string;
  perk: { name: string; nameZh: string; description: string };
  abilities: string[];
  upgrades: string[];
  unlocked: boolean;
  unlockHint: string;
}

export const CHARACTERS: Character[] = [
  {
    id: 'cat',
    name: 'Cat',
    nameZh: '猫',
    perk: {
      name: 'Price of Greed',
      nameZh: '贪婪的代价',
      description: 'Can pick more than one upgrade, but every time the whole team pays a multiplied cost.',
    },
    abilities: ['Ability 1 — TBD', 'Ability 2 — TBD'],
    upgrades: ['Upgrade 1 — TBD', 'Upgrade 2 — TBD', 'Upgrade 3 — TBD'],
    unlocked: true,
    unlockHint: 'Starter character',
  },
  {
    id: 'mouse',
    name: 'Mouse',
    nameZh: '老鼠',
    perk: {
      name: 'No Collision',
      nameZh: '无体积碰撞',
      description: 'Passes through other bodies — collision between entities is turned off.',
    },
    abilities: ['Ability 1 — TBD', 'Ability 2 — TBD'],
    upgrades: ['Upgrade 1 — TBD', 'Upgrade 2 — TBD', 'Upgrade 3 — TBD'],
    unlocked: true,
    unlockHint: 'Starter character',
  },
  {
    id: 'dog',
    name: 'Dog',
    nameZh: '狗',
    perk: {
      name: 'Many Hands',
      nameZh: '能者多劳',
      description: 'Can carry something in each hand.',
    },
    abilities: ['Ability 1 — TBD', 'Ability 2 — TBD'],
    upgrades: ['Upgrade 1 — TBD', 'Upgrade 2 — TBD', 'Upgrade 3 — TBD'],
    unlocked: false,
    unlockHint: 'Unlock condition — TBD',
  },
  {
    id: 'bird',
    name: 'Bird',
    nameZh: '鸟',
    perk: {
      name: 'Soothing Song',
      nameZh: '精神抚慰',
      description: 'Stand still for 3 seconds to start singing, restoring SAN to everyone nearby.',
    },
    abilities: ['Ability 1 — TBD', 'Ability 2 — TBD'],
    upgrades: ['Upgrade 1 — TBD', 'Upgrade 2 — TBD', 'Upgrade 3 — TBD'],
    unlocked: false,
    unlockHint: 'Unlock condition — TBD',
  },
];

export const characterById = (id: string) => CHARACTERS.find((c) => c.id === id) ?? CHARACTERS[0];
