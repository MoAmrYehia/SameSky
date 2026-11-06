import type { ReactNode } from 'react';
import { avatarDataUrl, DEFAULT_AVATAR } from '../shared/avatar';
import type { AvatarDefinition } from '../shared/contracts';
import { cityOf } from '../shared/tz';

const P: Record<string, ReactNode> = {
  calendar: <><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M3 10h18M8 3v4M16 3v4" /></>,
  image: <><rect x="3" y="4" width="18" height="16" rx="3" /><path d="m3 17 5-5 4 4 3-3 6 6" /><circle cx="15.5" cy="9" r="1.6" /></>,
  heart: <path d="M12 20s-7-4.4-9-9.2C1.6 7.3 3.8 4 7.2 4c2 0 3.6 1.1 4.8 2.8C13.2 5.1 14.8 4 16.8 4c3.4 0 5.6 3.3 4.2 6.8C19 15.6 12 20 12 20Z" />,
  bookmark: <path d="M6 3h12v18l-6-4.5L6 21V3Z" />,
  settings: <><circle cx="12" cy="12" r="3.2" /><path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-1.8-.3 1.6 1.6 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.6 1.6 0 0 0-1-1.5 1.6 1.6 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.6 1.6 0 0 0 .3-1.8 1.6 1.6 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.6 1.6 0 0 0 1.5-1 1.6 1.6 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 1.8.3H9a1.6 1.6 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.6 1.6 0 0 0 1 1.5 1.6 1.6 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0-.3 1.8V9a1.6 1.6 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.6 1.6 0 0 0-1.5 1Z" /></>,
  bell: <><path d="M6 16V11a6 6 0 1 1 12 0v5l2 2H4l2-2Z" /><path d="M10 20a2 2 0 0 0 4 0" /></>,
  users: <><circle cx="9" cy="8" r="3.5" /><path d="M2.5 20c.6-3.6 3.3-5.5 6.5-5.5s5.9 1.9 6.5 5.5" /><circle cx="17" cy="9" r="2.8" /><path d="M16 14.6c2.8.2 4.9 1.9 5.5 5.4" /></>,
  user: <><circle cx="12" cy="8" r="4" /><path d="M4 21c.8-4.2 4-6.5 8-6.5s7.2 2.3 8 6.5" /></>,
  play: <path d="M7 4.5v15l13-7.5-13-7.5Z" fill="currentColor" />,
  pause: <><rect x="6" y="4.5" width="4" height="15" rx="1" fill="currentColor" /><rect x="14" y="4.5" width="4" height="15" rx="1" fill="currentColor" /></>,
  reply: <><path d="M10 7 4 12l6 5" /><path d="M4 12h10a6 6 0 0 1 6 6v1" /></>,
  lock: <><rect x="5" y="10" width="14" height="11" rx="2.5" /><path d="M8 10V7a4 4 0 0 1 8 0v3" /></>,
  lockCheck: <><rect x="5" y="10" width="14" height="11" rx="2.5" /><path d="M8 10V7a4 4 0 0 1 8 0v3" /><path d="m9 15.5 2 2 4-4" /></>,
  upload: <><path d="M12 16V4M7 9l5-5 5 5" /><path d="M4 16v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3" /></>,
  camera: <><path d="M4 8h3l2-3h6l2 3h3a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1Z" /><circle cx="12" cy="13.5" r="3.6" /></>,
  video: <><rect x="3" y="6" width="13" height="12" rx="2.5" /><path d="m16 10 5-3v10l-5-3" /></>,
  mic: <><rect x="9" y="3" width="6" height="11" rx="3" /><path d="M5 11a7 7 0 0 0 14 0M12 18v3" /></>,
  text: <path d="M5 6V4h14v2M12 4v16M9 20h6" />,
  check: <path d="m5 12.5 4.5 4.5L19 7.5" />,
  checkCircle: <><circle cx="12" cy="12" r="9" fill="currentColor" stroke="none" /><path d="m8 12.3 2.7 2.7L16 9.6" stroke="#fff" /></>,
  moon: <path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5Z" />,
  pin: <><path d="M12 21s-6-5.3-6-11a6 6 0 0 1 12 0c0 5.7-6 11-6 11Z" /><circle cx="12" cy="10" r="2.2" /></>,
  sparkle: <path d="M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M18 6l-2.5 2.5M8.5 15.5 6 18" />,
  send: <path d="m21 3-9.5 18-2.2-7.3L2 11.5 21 3Z" />,
  x: <path d="M6 6l12 12M18 6 6 18" />,
  chevron: <path d="m6 9 6 6 6-6" />,
  next: <path d="m9 6 6 6-6 6" />,
  prev: <path d="m15 6-6 6 6 6" />,
  headset: <><path d="M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2h-4l-1.5-2.5h-3L9 17H5a2 2 0 0 1-2-2V9Z" /><circle cx="8" cy="12" r="1.6" /><circle cx="16" cy="12" r="1.6" /></>,
  home: <><path d="M3 11 12 4l9 7" /><path d="M5 10v10h14V10" /></>,
  sprout: <><path d="M12 21v-8" /><path d="M12 13c0-4-3-6-7-6 0 4 3 6 7 6ZM12 11c0-4 3-6 7-6 0 4-3 6-7 6Z" /></>,
  cc: <><rect x="3" y="5" width="18" height="14" rx="3" /><path d="M10.5 10.2a2.4 2.4 0 1 0 0 3.6M17 10.2a2.4 2.4 0 1 0 0 3.6" /></>,
  link: <><path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1" /><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1" /></>,
  refresh: <><path d="M20 11a8 8 0 0 0-14.6-4.5M4 4v3h3" /><path d="M4 13a8 8 0 0 0 14.6 4.5M20 20v-3h-3" /></>,
  trash: <><path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13" /></>,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v6M12 7.5v.5" /></>,
  sun: <><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></>,
};

export type IconName = keyof typeof P;

export function Icon({ name, title }: { name: IconName | string; title?: string }) {
  return (
    <svg className="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden={title ? undefined : true} role={title ? 'img' : undefined}>
      {title ? <title>{title}</title> : null}
      {P[name] ?? null}
    </svg>
  );
}

export function Logo({ size = 44 }: { size?: number }) {
  return (
    <svg width={size} height={size * 0.6} viewBox="0 0 60 36" aria-hidden>
      <path d="M4 30 C10 10 30 6 40 18" fill="none" stroke="#e3b766" strokeWidth="4" strokeLinecap="round" />
      <path d="M20 32 C26 20 42 18 56 30" fill="none" stroke="#7fbf95" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}

export function Avatar({ def, size = 64, name }: { def?: AvatarDefinition | null; size?: number; name?: string }) {
  return (
    <span className="avatar" style={{ width: size, height: size }}>
      <img src={avatarDataUrl(def ?? DEFAULT_AVATAR)} alt={name ? `${name}'s avatar` : ''} width={size} height={size} />
    </span>
  );
}

export function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return <button type="button" className="toggle" role="switch" aria-checked={checked} aria-label={label} onClick={() => onChange(!checked)} />;
}

export function Checkbox({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <button type="button" className="check" role="checkbox" aria-checked={checked} aria-label={label} onClick={() => onChange(!checked)}>
      {checked ? <Icon name="check" /> : null}
    </button>
  );
}

export function Waveform({ bars = 34, playing = false, seed = 3 }: { bars?: number; playing?: boolean; seed?: number }) {
  const heights = Array.from({ length: bars }, (_, i) => 18 + Math.abs(Math.sin(i * 0.9 + seed) * 60 + Math.sin(i * 2.3) * 22));
  return (
    <div className={`wave${playing ? ' playing' : ''}`} aria-hidden>
      {heights.map((h, i) => (
        <i key={i} style={{ height: `${Math.min(100, h)}%`, animationDelay: `${(i % 7) * 0.08}s` }} />
      ))}
    </div>
  );
}

export function Notice({ kind = 'info', children }: { kind?: 'info' | 'warn' | 'error'; children: ReactNode }) {
  return (
    <div className={`notice${kind === 'info' ? '' : ` ${kind}`}`} role={kind === 'error' ? 'alert' : 'status'}>
      <Icon name={kind === 'info' ? 'info' : 'info'} />
      <div>{children}</div>
    </div>
  );
}

export function KeepsakeArt({ kind, progress = 1, target = 1 }: { kind: string; progress?: number; target?: number }) {
  if (kind === 'frame') {
    return (
      <svg viewBox="0 0 200 130" aria-hidden>
        <ellipse cx="100" cy="122" rx="80" ry="6" fill="rgba(0,0,0,.25)" />
        <rect x="45" y="14" width="110" height="104" rx="6" fill="#a8743f" />
        <rect x="53" y="22" width="94" height="88" fill="#f6efe2" />
        <rect x="59" y="28" width="82" height="62" fill="url(#sky)" />
        <defs>
          <linearGradient id="sky" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stopColor="#f2a86f" /><stop offset="1" stopColor="#6b5a7a" /></linearGradient>
        </defs>
        <path d="M59 78 l10 -12 6 6 10 -22 6 26 12 -8 8 10 30 0 v12 H59Z" fill="#4a3a52" />
        <circle cx="120" cy="52" r="6" fill="#ffe2a8" />
        <rect x="118" y="28" width="16" height="12" fill="#e9dcc6" stroke="#b8a07a" />
      </svg>
    );
  }
  if (kind === 'linked_sculptures') {
    return (
      <svg viewBox="0 0 200 130" aria-hidden>
        <ellipse cx="100" cy="118" rx="70" ry="7" fill="rgba(0,0,0,.25)" />
        <g fill="none" strokeLinecap="round">
          <ellipse cx="82" cy="70" rx="34" ry="40" stroke="#efe6d6" strokeWidth="16" />
          <ellipse cx="118" cy="66" rx="34" ry="40" stroke="#d9cdb8" strokeWidth="16" />
          <path d="M100 32 a34 40 0 0 1 16 2" stroke="#efe6d6" strokeWidth="16" />
        </g>
      </svg>
    );
  }
  const blossoms = Math.max(1, Math.round((progress / target) * 9));
  return (
    <svg viewBox="0 0 200 130" aria-hidden>
      <ellipse cx="100" cy="122" rx="56" ry="6" fill="rgba(0,0,0,.25)" />
      <path d="M78 102 h44 l-6 18 h-32Z" fill="#efe6d6" />
      <path d="M100 102 C98 86 104 76 100 62 M100 80 C90 74 84 70 78 60 M101 74 C112 68 118 62 122 52" stroke="#6b4a2b" strokeWidth="5" fill="none" strokeLinecap="round" />
      {[[76, 52], [88, 44], [102, 40], [116, 44], [126, 50], [70, 62], [132, 60], [96, 54], [110, 56]].map(([x, y], i) => (
        <g key={i}>
          <circle cx={x} cy={y} r="11" fill="#6f9a5c" />
          {i < blossoms ? <circle cx={x + 3} cy={y - 3} r="4" fill="#f6e4d0" /> : null}
        </g>
      ))}
    </svg>
  );
}

export function SourceIcon({ kind }: { kind: string }) {
  return <Icon name={kind === 'photo' ? 'image' : kind === 'clip' ? 'video' : kind === 'voice' ? 'mic' : 'text'} />;
}

export function deviceLabel(device?: string | null): string {
  return ({ phone: 'Phone', tablet: 'Tablet', webcam: 'Webcam', laptop: 'Laptop', camera: 'Camera', action_camera: 'Action camera', drone: 'Drone', glasses: 'AI glasses', other: 'Other device' } as Record<string, string>)[device ?? ''] ?? 'Device';
}

/** "20:55 in Beijing": original local capture time, named by city rather than an opaque abbreviation. */
export function localWhen(m: { captureLocal: { time: string; zone: string } }): string {
  return `${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`;
}

export function kindLabel(kind: string, device?: string | null): string {
  if (kind === 'voice') return 'Voice note';
  if (kind === 'text') return 'Note';
  if (kind === 'clip') return `${deviceLabel(device)} clip`;
  return `${deviceLabel(device)} photo`;
}
