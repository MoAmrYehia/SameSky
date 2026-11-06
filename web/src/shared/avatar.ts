// Avatar preset options shared by the companion editor, the API validation and the 3D character kit.
// Rendering lives in src/shared/character.ts (3D) and src/shared/portrait.ts (portraits from the same model).

import type { AvatarDefinition } from './contracts';

export const AVATAR_OPTIONS = {
  face: ['soft', 'defined'],
  skin: ['#f6d7c3', '#e8b896', '#d29a72', '#b0754d', '#8a5634', '#5e3a22'],
  hair: ['short', 'wavy', 'long', 'bun', 'curly', 'buzz', 'none'],
  hairColor: ['#2b1d14', '#4a2f1f', '#7a4a2a', '#b98a4e', '#d9c49a', '#8e8e8e', '#1d1d24'],
  top: ['#3f6b52', '#e9e0cf', '#2f4a6b', '#8a3b3b', '#c98f3a', '#5b4a7a'],
  accessory: ['none', 'glasses', 'earrings', 'scarf', 'cap'],
} as const;

export const DEFAULT_AVATAR: AvatarDefinition = { skin: '#e8b896', hair: 'wavy', hairColor: '#4a2f1f', top: '#3f6b52', accessory: 'none', face: 'soft' };
