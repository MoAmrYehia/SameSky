import { useEffect, useState, type ReactNode } from 'react';
import { DEFAULT_AVATAR } from '../shared/avatar';
import type { AvatarDefinition, AvatarView, MediaInfo } from '../shared/contracts';
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
  share: <><path d="M12 3v12" /><path d="m7.5 7.5 4.5-4.5 4.5 4.5" /><path d="M5 12v6.5A1.5 1.5 0 0 0 6.5 20h11a1.5 1.5 0 0 0 1.5-1.5V12" /></>,
  refresh: <><path d="M20 11a8 8 0 0 0-14.6-4.5M4 4v3h3" /><path d="M4 13a8 8 0 0 0 14.6 4.5M20 20v-3h-3" /></>,
  trash: <><path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13" /></>,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v6M12 7.5v.5" /></>,
  clock: <><circle cx="12" cy="12" r="8.5" /><path d="M12 7.5V12l3 2" /></>,
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

// Portraits and keepsakes are rendered from the same 3D assets used in the room (src/shared/portrait.ts).
// The renderer is code-split so three.js only loads when a portrait is first needed.
const renderer = () => import('../shared/portrait');

function usePortrait(avatar: AvatarView | null | undefined, def: AvatarDefinition | null | undefined, px: number) {
  // photo-derived portraits are keyed by asset + version (never by the short-lived URL)
  const key = avatar?.kind === 'photo_derived' ? `photo|${avatar.assetId}|${avatar.assetVersion}` : JSON.stringify(avatar?.definition ?? def ?? DEFAULT_AVATAR);
  const [src, setSrc] = useState<string | null>(null);
  useEffect(() => {
    let alive = true;
    setSrc(null);
    renderer()
      .then((m) => m.renderAvatarPortrait(avatar ?? (def ? { definition: def, version: 0 } : null), def ?? DEFAULT_AVATAR, px))
      .then((url) => alive && setSrc(url))
      .catch(() => alive && setSrc(null));
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, px]);
  return src;
}

/**
 * A person's portrait, rendered from the same 3D avatar they appear as in the room: their owner-approved
 * photo-derived model when this viewer may see it, otherwise the labelled demo preset (`demo` adds a badge).
 */
export function Avatar({ avatar, def, size = 64, name, demoBadge = false }: { avatar?: AvatarView | null; def?: AvatarDefinition | null; size?: number; name?: string; demoBadge?: boolean }) {
  const px = size <= 56 ? 128 : size <= 120 ? 256 : 384;
  const src = usePortrait(avatar, def, px);
  const isDemo = !avatar || avatar.kind !== 'photo_derived';
  return (
    <span className="avatar" style={{ width: size, height: size }}>
      {src ? <img src={src} alt={name ? `${name}'s ${isDemo ? 'demo avatar' : 'avatar'}` : ''} width={size} height={size} /> : <span className="avatar-loading" role="img" aria-label={name ? `${name}'s avatar (loading)` : undefined} />}
      {demoBadge && isDemo && size >= 72 ? <span className="avatar-demo-badge" aria-hidden="true">Demo</span> : null}
    </span>
  );
}

export function KeepsakeRender({ kind, progress = 1, target = 1, earned = true, title }: { kind: string; progress?: number; target?: number; earned?: boolean; title: string }) {
  const [src, setSrc] = useState<string | null>(null);
  useEffect(() => {
    let alive = true;
    renderer()
      .then((m) => m.renderKeepsake(kind, progress, target, earned, 520, 320))
      .then((url) => alive && setSrc(url))
      .catch(() => alive && setSrc(null));
    return () => {
      alive = false;
    };
  }, [kind, progress, target, earned]);
  return src ? <img className="keepsake-render" src={src} alt={`${title} keepsake`} /> : <span className="keepsake-render loading" aria-hidden />;
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

export function SourceIcon({ kind }: { kind: string }) {
  return <Icon name={kind === 'photo' ? 'image' : kind === 'clip' ? 'video' : kind === 'voice' ? 'mic' : 'text'} />;
}

export function deviceLabel(device?: string | null): string {
  return ({ phone: 'Phone', tablet: 'Tablet', webcam: 'Webcam', laptop: 'Laptop', camera: 'Camera', action_camera: 'Action camera', drone: 'Drone', glasses: 'AI glasses', headset: 'Headset', other: 'Other device' } as Record<string, string>)[device ?? ''] ?? 'Device';
}

/** "20:55 in Beijing": original local capture time, named by city rather than an opaque abbreviation. */
export function localWhen(m: { captureLocal: { time: string; zone: string } }): string {
  return `${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`;
}

export function kindLabel(kind: string, device?: string | null, origin?: MediaInfo['origin']): string {
  // sample media says how it was made, so it can't pass for a real recording
  if (origin === 'simulated' && kind === 'clip') return 'Simulated camera pan';
  if (origin === 'synthesized' && kind === 'voice') return 'Synthesized voice note';
  if (origin === 'stock' && kind === 'photo') return 'Sample photo';
  if (kind === 'voice') return 'Voice note';
  if (kind === 'text') return 'Note';
  if (kind === 'clip') return `${deviceLabel(device)} clip`;
  return `${deviceLabel(device)} photo`;
}
