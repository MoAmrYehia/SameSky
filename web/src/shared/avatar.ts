// Stylized, preset-based avatars. No face upload or likeness is needed; the same definition renders as
// an SVG portrait in the companion and as procedural geometry in the XR room (src/xr/avatar3d.ts).

import type { AvatarDefinition } from './contracts';

export const AVATAR_OPTIONS = {
  skin: ['#f6d7c3', '#e8b896', '#d29a72', '#b0754d', '#8a5634', '#5e3a22'],
  hair: ['short', 'wavy', 'long', 'bun', 'curly', 'buzz', 'none'],
  hairColor: ['#2b1d14', '#4a2f1f', '#7a4a2a', '#b98a4e', '#d9c49a', '#8e8e8e', '#1d1d24'],
  top: ['#3f6b52', '#e9e0cf', '#2f4a6b', '#8a3b3b', '#c98f3a', '#5b4a7a'],
  accessory: ['none', 'glasses', 'earrings', 'scarf', 'cap'],
} as const;

export const DEFAULT_AVATAR: AvatarDefinition = { skin: '#e8b896', hair: 'wavy', hairColor: '#4a2f1f', top: '#3f6b52', accessory: 'none' };

function shade(hex: string, amount: number): string {
  const n = parseInt(hex.slice(1), 16);
  const r = Math.max(0, Math.min(255, (n >> 16) + amount));
  const g = Math.max(0, Math.min(255, ((n >> 8) & 255) + amount));
  const b = Math.max(0, Math.min(255, (n & 255) + amount));
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')}`;
}

function hairBack(def: AvatarDefinition): string {
  const c = def.hairColor;
  switch (def.hair) {
    case 'long':
      return `<path d="M30 52 C28 26 44 14 60 14 C78 14 92 26 90 52 L94 96 C80 104 40 104 26 96 Z" fill="${c}"/>`;
    case 'wavy':
      return `<path d="M30 54 C26 28 44 15 60 15 C78 15 94 28 90 54 C96 66 92 80 86 84 C80 72 40 72 34 84 C28 80 24 66 30 54 Z" fill="${c}"/>`;
    case 'curly':
      return `<g fill="${c}">${[[36, 34], [48, 24], [62, 21], [76, 26], [86, 38], [30, 48], [90, 52], [33, 62], [88, 66]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="12"/>`).join('')}</g>`;
    case 'bun':
      return `<circle cx="60" cy="14" r="11" fill="${c}"/>`;
    default:
      return '';
  }
}

function hairFront(def: AvatarDefinition): string {
  const c = def.hairColor;
  switch (def.hair) {
    case 'short':
      return `<path d="M36 46 C36 26 50 20 61 20 C76 20 86 28 85 46 C78 36 66 32 56 34 C48 35 41 40 36 46 Z" fill="${c}"/>`;
    case 'buzz':
      return `<path d="M37 44 C38 28 50 22 60 22 C72 22 83 28 83 44 C74 36 46 36 37 44 Z" fill="${c}" opacity="0.85"/>`;
    case 'wavy':
    case 'long':
      return `<path d="M35 50 C34 28 50 21 61 21 C76 21 87 30 85 50 C80 38 70 32 60 33 C52 36 46 42 35 50 Z" fill="${c}"/>`;
    case 'bun':
      return `<path d="M36 46 C36 28 50 21 60 21 C72 21 84 28 84 46 C76 34 44 34 36 46 Z" fill="${c}"/>`;
    case 'curly':
      return `<g fill="${c}">${[[44, 30], [56, 26], [68, 27], [78, 33]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="9"/>`).join('')}</g>`;
    default:
      return '';
  }
}

function accessory(def: AvatarDefinition): string {
  switch (def.accessory) {
    case 'glasses':
      return `<g fill="none" stroke="#2a2a2a" stroke-width="2.2"><circle cx="50" cy="55" r="7.5"/><circle cx="70" cy="55" r="7.5"/><path d="M57.5 55 h5"/></g>`;
    case 'earrings':
      return `<g fill="#d8b24a"><circle cx="37" cy="66" r="2.8"/><circle cx="83" cy="66" r="2.8"/></g>`;
    case 'scarf':
      return `<path d="M38 86 C50 94 70 94 82 86 L84 96 C70 104 50 104 36 96 Z" fill="#c98f3a"/>`;
    case 'cap':
      return `<path d="M34 42 C36 22 84 22 86 42 Z" fill="${shade(def.top, -10)}"/><path d="M60 40 C74 40 92 42 98 46 C86 46 72 45 60 44 Z" fill="${shade(def.top, -30)}"/>`;
    default:
      return '';
  }
}

/** Returns a self-contained SVG portrait (120x120 viewBox). */
export function avatarSvg(def: AvatarDefinition, opts: { background?: string } = {}): string {
  const skin = def.skin;
  const bg = opts.background ?? '#efe6d6';
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" role="img">
  <circle cx="60" cy="60" r="60" fill="${bg}"/>
  ${hairBack(def)}
  <path d="M18 120 C20 96 38 86 60 86 C82 86 100 96 102 120 Z" fill="${def.top}"/>
  <path d="M50 78 h20 v12 c-6 4 -14 4 -20 0 Z" fill="${shade(skin, -18)}"/>
  <ellipse cx="60" cy="55" rx="24" ry="27" fill="${skin}"/>
  <ellipse cx="36" cy="58" rx="4" ry="6" fill="${shade(skin, -10)}"/><ellipse cx="84" cy="58" rx="4" ry="6" fill="${shade(skin, -10)}"/>
  ${hairFront(def)}
  <g fill="#2b2222"><ellipse cx="50" cy="56" rx="3" ry="3.6"/><ellipse cx="70" cy="56" rx="3" ry="3.6"/></g>
  <g fill="#fff" opacity="0.9"><circle cx="51" cy="54.8" r="1"/><circle cx="71" cy="54.8" r="1"/></g>
  <path d="M45 48 q5 -3 10 0 M65 48 q5 -3 10 0" stroke="${shade(def.hairColor, 10)}" stroke-width="2" fill="none" stroke-linecap="round"/>
  <ellipse cx="45" cy="65" rx="5" ry="3" fill="#e58f7a" opacity="0.35"/><ellipse cx="75" cy="65" rx="5" ry="3" fill="#e58f7a" opacity="0.35"/>
  <path d="M52 68 q8 7 16 0" stroke="#8a4a3a" stroke-width="2.4" fill="none" stroke-linecap="round"/>
  ${accessory(def)}
</svg>`;
}

export function avatarDataUrl(def: AvatarDefinition, opts?: { background?: string }): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(avatarSvg(def, opts))}`;
}
