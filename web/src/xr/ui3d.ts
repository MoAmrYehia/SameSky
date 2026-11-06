// Spatial UI built from canvas textures on planes: crisp, legible text at seated distances, cheap to
// render on Quest (one draw call per panel), and every control is both poke-able (direct touch with a
// fingertip) and ray-selectable (pinch from a distance) through IWSDK's interaction tags.
//
// Visual language: tinted translucent "glass" (a cheap alpha fill, a top highlight and a hairline border,
// never a transmission material), the self-hosted Figtree typeface, and unlit materials (toneMapped:false)
// so interface colours are not tone mapped twice.

import { type Entity, Hovered, PokeInteractable, RayInteractable, type World } from '@iwsdk/core';
import { CanvasTexture, Mesh, MeshBasicMaterial, type Object3D, PlaneGeometry, SRGBColorSpace } from 'three';

export const PX_PER_M = 1600;
export const FONT = '"Figtree Variable", "Segoe UI", "Roboto", "Helvetica Neue", system-ui, sans-serif';
export const COLORS = {
  cream: '#f7f2ea',
  cream2: '#ece3d4',
  ink: '#1d211f',
  muted: '#5e625c',
  green: '#3d6b4f',
  greenBright: '#7fd09a',
  greenSoft: '#d9e8da',
  dark: 'rgba(22, 28, 26, 0.88)',
  gold: '#e3b766',
  glass: 'rgba(42, 38, 35, 0.58)',
  glassDark: 'rgba(24, 24, 22, 0.72)',
  glassLight: 'rgba(250, 244, 236, 0.20)',
  hairline: 'rgba(255, 255, 255, 0.42)',
  onGlass: '#ffffff',
  onGlassMuted: 'rgba(255, 255, 255, 0.78)',
};

/** Make sure canvas text uses the real font (canvas does not wait for web fonts on its own). */
export async function loadUiFont() {
  try {
    await Promise.all([document.fonts.load(`500 32px ${FONT}`), document.fonts.load(`600 32px ${FONT}`), document.fonts.load(`700 32px ${FONT}`)]);
  } catch {
    /* fall back to the system face */
  }
}

export const ICONS: Record<string, string> = {
  play: 'M7 4.5v15l13-7.5-13-7.5Z',
  pause: 'M6 4.5h4v15H6zM14 4.5h4v15h-4z',
  reply: 'M10 7 4 12l6 5M4 12h10a6 6 0 0 1 6 6v1',
  heart: 'M12 20s-7-4.4-9-9.2C1.6 7.3 3.8 4 7.2 4c2 0 3.6 1.1 4.8 2.8C13.2 5.1 14.8 4 16.8 4c3.4 0 5.6 3.3 4.2 6.8C19 15.6 12 20 12 20Z',
  cc: 'M3 5h18v14H3zM10.5 10.2a2.4 2.4 0 1 0 0 3.6M17 10.2a2.4 2.4 0 1 0 0 3.6',
  mute: 'M4 9h4l5-4v14l-5-4H4zM17 9l5 6M22 9l-5 6',
  sound: 'M4 9h4l5-4v14l-5-4H4zM17 8a5 5 0 0 1 0 8M19.5 5.5a8.5 8.5 0 0 1 0 13',
  recenter: 'M12 3v3M12 18v3M3 12h3M18 12h3M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z',
  reset: 'M20 11a8 8 0 0 0-14.6-4.5M4 4v3h3M4 13a8 8 0 0 0 14.6 4.5M20 20v-3h-3',
  exit: 'M10 4H5v16h5M14 8l4 4-4 4M18 12H9',
  calendar: 'M3 5h18v16H3zM3 10h18M8 3v4M16 3v4',
  users: 'M9 11.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7ZM2.5 20c.6-3.6 3.3-5.5 6.5-5.5s5.9 1.9 6.5 5.5M17 11.8a2.8 2.8 0 1 0 0-5.6M16 14.6c2.8.2 4.9 1.9 5.5 5.4',
  x: 'M6 6l12 12M18 6 6 18',
  check: 'm5 12.5 4.5 4.5L19 7.5',
  sprout: 'M12 21v-8M12 13c0-4-3-6-7-6 0 4 3 6 7 6ZM12 11c0-4 3-6 7-6 0 4-3 6-7 6Z',
  screen: 'M3 4.5h18v12H3zM8 20h8M12 16.5V20',
  wave: 'M3 12h1M6.5 9v6M10 6v12M13.5 8v8M17 10v4M20.5 11.5v1',
};

export function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  const rr = Math.max(0, Math.min(r, w / 2, h / 2));
  ctx.beginPath();
  ctx.moveTo(x + rr, y);
  ctx.arcTo(x + w, y, x + w, y + h, rr);
  ctx.arcTo(x + w, y + h, x, y + h, rr);
  ctx.arcTo(x, y + h, x, y, rr);
  ctx.arcTo(x, y, x + w, y, rr);
  ctx.closePath();
}

/** Tinted translucent panel with a soft top highlight and a hairline border (the "glass" treatment). */
export function glass(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number, opts: { tint?: string; border?: string; highlight?: number } = {}) {
  ctx.save();
  roundRect(ctx, x, y, w, h, r);
  ctx.fillStyle = opts.tint ?? COLORS.glass;
  ctx.fill();
  const hi = opts.highlight ?? 0.16;
  if (hi > 0) {
    const g = ctx.createLinearGradient(0, y, 0, y + h);
    g.addColorStop(0, `rgba(255,255,255,${hi})`);
    g.addColorStop(0.45, `rgba(255,255,255,${hi * 0.2})`);
    g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g;
    ctx.fill();
  }
  const lw = Math.max(2, Math.min(w, h) * 0.022);
  ctx.lineWidth = lw;
  ctx.strokeStyle = opts.border ?? COLORS.hairline;
  roundRect(ctx, x + lw / 2, y + lw / 2, w - lw, h - lw, Math.max(0, r - lw / 2));
  ctx.stroke();
  ctx.restore();
}

export function wrapText(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string[] {
  const words = text.split(/\s+/);
  const lines: string[] = [];
  let line = '';
  for (const w of words) {
    const test = line ? `${line} ${w}` : w;
    if (ctx.measureText(test).width > maxWidth && line) {
      lines.push(line);
      line = w;
    } else line = test;
  }
  if (line) lines.push(line);
  return lines;
}

const iconPaths = new Map<string, Path2D>();
export function drawIcon(ctx: CanvasRenderingContext2D, name: string, cx: number, cy: number, size: number, color: string, fill = false) {
  const path = ICONS[name];
  if (!path) return;
  let p = iconPaths.get(name);
  if (!p) iconPaths.set(name, (p = new Path2D(path)));
  ctx.save();
  ctx.translate(cx - size / 2, cy - size / 2);
  ctx.scale(size / 24, size / 24);
  if (fill || name === 'play' || name === 'pause' || name === 'heart') {
    ctx.fillStyle = color;
    ctx.fill(p);
  } else {
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.stroke(p);
  }
  ctx.restore();
}

/** A plane with a canvas texture. Redraw by calling draw(). */
export class CanvasPanel {
  readonly canvas: HTMLCanvasElement;
  readonly ctx: CanvasRenderingContext2D;
  readonly texture: CanvasTexture;
  readonly mesh: Mesh<PlaneGeometry, MeshBasicMaterial>;
  readonly W: number;
  readonly H: number;

  constructor(readonly widthM: number, readonly heightM: number, opts: { transparent?: boolean; renderOrder?: number; pxPerM?: number } = {}) {
    const density = opts.pxPerM ?? PX_PER_M;
    const scale = Math.min(1, 2048 / Math.max(widthM, heightM) / density);
    this.W = Math.max(8, Math.round(widthM * density * scale));
    this.H = Math.max(8, Math.round(heightM * density * scale));
    this.canvas = document.createElement('canvas');
    this.canvas.width = this.W;
    this.canvas.height = this.H;
    this.ctx = this.canvas.getContext('2d')!;
    this.texture = new CanvasTexture(this.canvas);
    this.texture.colorSpace = SRGBColorSpace;
    this.texture.anisotropy = 4;
    const transparent = opts.transparent ?? true;
    const material = new MeshBasicMaterial({ map: this.texture, transparent, toneMapped: false, depthWrite: !transparent });
    this.mesh = new Mesh(new PlaneGeometry(widthM, heightM), material);
    if (opts.renderOrder) this.mesh.renderOrder = opts.renderOrder;
  }

  draw(fn: (ctx: CanvasRenderingContext2D, W: number, H: number) => void) {
    this.ctx.clearRect(0, 0, this.W, this.H);
    fn(this.ctx, this.W, this.H);
    this.texture.needsUpdate = true;
  }

  dispose() {
    this.texture.dispose();
    this.mesh.geometry.dispose();
    this.mesh.material.dispose();
  }
}

export interface PillStyle {
  tint?: string;
  fg?: string;
  dot?: string;
  icon?: string;
  iconColor?: string;
  weight?: number;
  /** Wrap width in metres (before legibility scaling); defaults to the pill's maximum width. */
  wrapM?: number;
}

/**
 * A pill label cropped to its measured text. The canvas is allocated once at the maximum size; each change
 * redraws it and crops the plane (with a matching UV window) to the drawn width, wrapping to a second line
 * instead of truncating. set() is memoized, so calling it with unchanged text costs nothing.
 */
export class FittedPill {
  readonly panel: CanvasPanel;
  private key = '';
  /** Current physical size (metres) after fitting and legibility scaling. */
  width = 0;
  height = 0;
  private fw = 1;
  private fh = 0.5;
  private k = 1;

  constructor(readonly maxWidthM: number, readonly lineHeightM: number, renderOrder = 6) {
    this.panel = new CanvasPanel(maxWidthM, lineHeightM * 2, { renderOrder });
  }

  get mesh() {
    return this.panel.mesh;
  }

  set(text: string, style: PillStyle = {}) {
    const key = `${text}\u0000${style.tint ?? ''}\u0000${style.fg ?? ''}\u0000${style.dot ?? ''}\u0000${style.icon ?? ''}\u0000${style.iconColor ?? ''}\u0000${style.wrapM ?? ''}`;
    if (key === this.key) return;
    this.key = key;
    const { ctx, W, H } = this.panel;
    const lineH = H / 2;
    let size = Math.round(lineH * 0.4);
    ctx.clearRect(0, 0, W, H);
    const wrapW = Math.min(W, style.wrapM ? (style.wrapM / this.maxWidthM) * W : W);
    // wrap to at most two lines; if the text still doesn't fit, use a smaller size — never truncate
    let lines: string[];
    let pad: number;
    let lead: number;
    for (;;) {
      ctx.font = `${style.weight ?? 600} ${size}px ${FONT}`;
      pad = size * 0.95;
      lead = (style.dot ? size * 1.05 : 0) + (style.icon ? size * 1.45 : 0);
      const maxText = wrapW - pad * 2 - lead;
      lines = ctx.measureText(text).width > maxText ? wrapText(ctx, text, maxText) : [text];
      if (lines.length <= 2 || size <= lineH * 0.22) break;
      size -= 1;
    }
    let textW = 0;
    for (const l of lines) textW = Math.max(textW, ctx.measureText(l).width);
    const w = Math.min(W, Math.ceil(textW + pad * 2 + lead));
    const h = lines.length > 1 ? H : lineH;
    glass(ctx, 1, 1, w - 2, h - 2, lines.length > 1 ? lineH * 0.42 : (h - 2) / 2, { tint: style.tint ?? COLORS.glassDark });
    let x = pad;
    if (style.dot) {
      ctx.fillStyle = style.dot;
      ctx.beginPath();
      ctx.arc(x + size * 0.3, h / 2, size * 0.3, 0, Math.PI * 2);
      ctx.fill();
      x += size * 1.05;
    }
    if (style.icon) {
      drawIcon(ctx, style.icon, x + size * 0.55, h / 2, size * 1.15, style.iconColor ?? COLORS.greenBright);
      x += size * 1.45;
    }
    ctx.fillStyle = style.fg ?? COLORS.onGlass;
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    const n = lines.length;
    lines.forEach((l, i) => ctx.fillText(l, x, n > 1 ? h * ((i + 0.5) / n) * 0.84 + h * 0.08 : h / 2 + 1));
    const fw = w / W;
    const fh = h / H;
    this.panel.texture.repeat.set(fw, fh);
    this.panel.texture.offset.set(0, 1 - fh);
    this.panel.texture.needsUpdate = true;
    this.fw = fw;
    this.fh = fh;
    this.applyScale();
  }

  /** Flat-screen legibility: enlarge uniformly (1 = real-world size used in a headset). */
  setUiScale(k: number) {
    if (Math.abs(k - this.k) < 1e-3) return;
    this.k = k;
    this.applyScale();
  }

  private applyScale() {
    this.panel.mesh.scale.set(this.fw * this.k, this.fh * this.k, 1);
    this.width = this.fw * this.panel.widthM * this.k;
    this.height = this.fh * this.panel.heightM * this.k;
  }

  dispose() {
    this.panel.dispose();
  }
}

/**
 * A two-line glass card (icon, title, subtitle, optional round trailing button), cropped to its measured
 * content like FittedPill. Used for the cup's waiting-message affordance.
 */
export class FittedCard {
  readonly panel: CanvasPanel;
  private key = '';
  width = 0;
  height: number;
  private fw = 1;
  private k = 1;

  constructor(readonly maxWidthM: number, heightM: number, renderOrder = 6) {
    this.panel = new CanvasPanel(maxWidthM, heightM, { renderOrder });
    this.height = heightM;
  }

  /** Flat-screen legibility: enlarge uniformly (1 = real-world size used in a headset). */
  setUiScale(k: number) {
    if (Math.abs(k - this.k) < 1e-3) return;
    this.k = k;
    this.applyScale();
  }

  private applyScale() {
    this.panel.mesh.scale.set(this.fw * this.k, this.k, 1);
    this.width = this.fw * this.panel.widthM * this.k;
    this.height = this.panel.heightM * this.k;
  }

  get mesh() {
    return this.panel.mesh;
  }

  set(title: string, subtitle: string, opts: { icon?: string; trailing?: 'play' | 'pause' | null; hovered?: boolean } = {}) {
    const key = `${title}\u0000${subtitle}\u0000${opts.icon ?? ''}\u0000${opts.trailing ?? ''}\u0000${opts.hovered ? 1 : 0}`;
    if (key === this.key) return;
    this.key = key;
    const { ctx, W, H } = this.panel;
    ctx.clearRect(0, 0, W, H);
    const pad = H * 0.24;
    const iconW = opts.icon ? H * 0.62 : 0;
    const trailW = opts.trailing ? H * 0.78 : 0;
    ctx.font = `700 ${Math.round(H * 0.3)}px ${FONT}`;
    const tW = ctx.measureText(title).width;
    ctx.font = `500 ${Math.round(H * 0.22)}px ${FONT}`;
    const sW = ctx.measureText(subtitle).width;
    const w = Math.min(W, Math.ceil(pad * 2 + iconW + Math.max(tW, sW) + trailW + (trailW ? pad * 0.6 : 0)));
    glass(ctx, 1, 1, w - 2, H - 2, H * 0.3, { tint: COLORS.glass });
    if (opts.hovered) {
      ctx.strokeStyle = '#a8e2bb';
      ctx.lineWidth = 5;
      roundRect(ctx, 5, 5, w - 10, H - 10, H * 0.27);
      ctx.stroke();
    }
    let x = pad;
    if (opts.icon) {
      drawIcon(ctx, opts.icon, x + iconW * 0.42, H / 2, H * 0.5, COLORS.greenBright);
      x += iconW;
    }
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    ctx.fillStyle = COLORS.onGlass;
    ctx.font = `700 ${Math.round(H * 0.3)}px ${FONT}`;
    ctx.fillText(title, x, H * 0.37);
    ctx.fillStyle = COLORS.onGlassMuted;
    ctx.font = `500 ${Math.round(H * 0.22)}px ${FONT}`;
    ctx.fillText(subtitle, x, H * 0.69);
    if (opts.trailing) {
      const cx = w - pad - trailW / 2;
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      ctx.beginPath();
      ctx.arc(cx, H / 2, trailW / 2, 0, Math.PI * 2);
      ctx.fill();
      drawIcon(ctx, opts.trailing, cx + (opts.trailing === 'play' ? trailW * 0.04 : 0), H / 2, trailW * 0.5, COLORS.green);
    }
    const fw = w / W;
    this.panel.texture.repeat.set(fw, 1);
    this.panel.texture.offset.set(0, 0);
    this.panel.texture.needsUpdate = true;
    this.fw = fw;
    this.applyScale();
  }

  dispose() {
    this.panel.dispose();
  }
}

// ---------------------------------------------------------------------------
// Pressables: one registry, one system (see main.ts RoomSystem) for hover feedback.
// ---------------------------------------------------------------------------

export interface Pressable {
  entity: Entity;
  object: Object3D;
  onPress: () => void;
  redraw?: (hovered: boolean) => void;
  hovered: boolean;
  enabled: boolean;
}

export const pressables = new Map<number, Pressable>();
let inputGuardUntil = 0;
/** Suppress selections briefly after tracking changes so a re-acquired hand can't trigger anything. */
export function guardInput(ms = 600) {
  inputGuardUntil = performance.now() + ms;
}

export function makePressable(world: World, object: Object3D, parent: Entity, onPress: () => void, redraw?: (hovered: boolean) => void): Pressable {
  const entity = world.createTransformEntity(object, { parent });
  return registerPressable(entity, object, onPress, redraw);
}

/** Make an existing entity selectable. Its object must already contain its meshes: IWSDK indexes them when the tag is added. */
export function registerPressable(entity: Entity, object: Object3D, onPress: () => void, redraw?: (hovered: boolean) => void): Pressable {
  entity.addComponent(RayInteractable);
  entity.addComponent(PokeInteractable);
  const p: Pressable = { entity, object, onPress, redraw, hovered: false, enabled: true };
  pressables.set(entity.index, p);
  let lastFire = 0;
  (object as unknown as { addEventListener: (t: string, f: () => void) => void }).addEventListener('click', () => {
    const now = performance.now();
    if (!p.enabled || now < inputGuardUntil || now - lastFire < 350) return;
    lastFire = now;
    onPress();
  });
  return p;
}

export function isHovered(p: Pressable) {
  return p.entity.hasComponent(Hovered);
}

/** Invisible selection volume: never drawn (no draw call), still hit by rays and fingertips. */
export function hitBox(w: number, h: number, d = 0.02) {
  const m = new Mesh(new PlaneGeometry(w, h), new MeshBasicMaterial({ visible: false }));
  m.position.z = d / 2;
  m.name = 'hit';
  return m;
}

export interface ButtonOptions {
  label: string;
  icon?: string;
  width?: number;
  height?: number;
  primary?: boolean;
  dark?: boolean;
}

export class Button {
  readonly panel: CanvasPanel;
  readonly pressable: Pressable;
  private opts: Required<Omit<ButtonOptions, 'icon'>> & { icon?: string };
  private drawnKey = '';
  active = false;

  constructor(world: World, parent: Entity, opts: ButtonOptions, onPress: () => void) {
    this.opts = { width: 0.11, height: 0.11, primary: false, dark: false, ...opts };
    this.panel = new CanvasPanel(this.opts.width, this.opts.height, { renderOrder: 4 });
    this.pressable = makePressable(world, this.panel.mesh, parent, onPress, (h) => this.render(h));
    this.render(false);
  }

  set(opts: Partial<ButtonOptions>) {
    Object.assign(this.opts, opts);
    this.render(this.pressable.hovered);
  }

  setActive(active: boolean) {
    this.active = active;
    this.render(this.pressable.hovered);
  }

  render(hovered: boolean) {
    const { label, icon, primary, dark } = this.opts;
    const key = `${label}|${icon}|${primary}|${dark}|${this.active}|${hovered}`;
    if (key === this.drawnKey) return;
    this.drawnKey = key;
    this.panel.draw((ctx, W, H) => {
      const r = Math.min(W, H) / 2 - 4;
      if (primary || this.active) {
        roundRect(ctx, 4, 4, W - 8, H - 8, r);
        ctx.fillStyle = hovered ? '#4a805f' : COLORS.green;
        ctx.fill();
        ctx.lineWidth = 3;
        ctx.strokeStyle = 'rgba(255,255,255,0.35)';
        ctx.stroke();
      } else {
        glass(ctx, 4, 4, W - 8, H - 8, r, { tint: dark ? COLORS.glassDark : 'rgba(250,244,236,0.92)', border: dark ? COLORS.hairline : 'rgba(29,33,31,0.12)' });
      }
      if (hovered) {
        ctx.strokeStyle = '#a8e2bb';
        ctx.lineWidth = 6;
        roundRect(ctx, 7, 7, W - 14, H - 14, r - 3);
        ctx.stroke();
      }
      const fg = primary || this.active || dark ? '#ffffff' : COLORS.green;
      const wide = W > H * 1.6;
      const iconSize = Math.min(H * 0.42, 96);
      ctx.fillStyle = fg;
      ctx.textBaseline = 'middle';
      if (icon && wide) {
        drawIcon(ctx, icon, H * 0.55, H / 2, iconSize, fg);
        ctx.font = `600 ${Math.round(H * 0.34)}px ${FONT}`;
        ctx.textAlign = 'left';
        ctx.fillText(label, H * 0.95, H / 2 + 2, W - H * 1.2);
      } else if (icon) {
        drawIcon(ctx, icon, W / 2, H * 0.4, iconSize, fg);
        ctx.font = `600 ${Math.round(H * 0.16)}px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.fillText(label, W / 2, H * 0.78, W - 20);
      } else {
        ctx.font = `600 ${Math.round(H * 0.3)}px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.fillText(label, W / 2, H / 2 + 2, W - 30);
      }
    });
  }
}

/** A text label panel (not interactive). */
export function makeLabel(widthM: number, heightM: number, draw: (ctx: CanvasRenderingContext2D, W: number, H: number) => void): CanvasPanel {
  const panel = new CanvasPanel(widthM, heightM);
  panel.draw(draw);
  return panel;
}

export function pill(ctx: CanvasRenderingContext2D, text: string, W: number, H: number, opts: { bg?: string; fg?: string; dot?: string; size?: number } = {}) {
  const size = opts.size ?? H * 0.42;
  ctx.font = `600 ${size}px ${FONT}`;
  const tw = ctx.measureText(text).width + (opts.dot ? size * 1.2 : 0);
  const w = Math.min(W - 8, tw + size * 1.6);
  const x = (W - w) / 2;
  ctx.fillStyle = opts.bg ?? COLORS.dark;
  roundRect(ctx, x, 4, w, H - 8, (H - 8) / 2);
  ctx.fill();
  let tx = x + size * 0.8;
  if (opts.dot) {
    ctx.fillStyle = opts.dot;
    ctx.beginPath();
    ctx.arc(tx + size * 0.3, H / 2, size * 0.28, 0, Math.PI * 2);
    ctx.fill();
    tx += size * 1.2;
  }
  ctx.fillStyle = opts.fg ?? '#fff';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, tx, H / 2 + 1);
}
