// Spatial UI built from canvas textures on planes: crisp, legible text at seated distances, cheap to
// render on Quest (one draw call per panel), and every button is both poke-able (direct touch with a
// fingertip) and ray-selectable (pinch from a distance) through IWSDK's interaction tags.

import { type Entity, Hovered, PokeInteractable, RayInteractable, type World } from '@iwsdk/core';
import { CanvasTexture, Mesh, MeshBasicMaterial, type Object3D, PlaneGeometry, SRGBColorSpace } from 'three';

export const PX_PER_M = 1600;
export const FONT = '"Segoe UI", "Roboto", "Helvetica Neue", system-ui, sans-serif';
export const COLORS = {
  cream: '#f7f2ea',
  cream2: '#ece3d4',
  ink: '#1d211f',
  muted: '#5e625c',
  green: '#3d6b4f',
  greenSoft: '#d9e8da',
  dark: 'rgba(22, 28, 26, 0.88)',
  gold: '#e3b766',
};

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
};

export function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
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

export function drawIcon(ctx: CanvasRenderingContext2D, name: string, cx: number, cy: number, size: number, color: string, fill = false) {
  const path = ICONS[name];
  if (!path) return;
  ctx.save();
  ctx.translate(cx - size / 2, cy - size / 2);
  ctx.scale(size / 24, size / 24);
  const p = new Path2D(path);
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

  constructor(readonly widthM: number, readonly heightM: number, opts: { transparent?: boolean; renderOrder?: number } = {}) {
    const scale = Math.min(1, 2048 / Math.max(widthM, heightM) / PX_PER_M);
    this.W = Math.round(widthM * PX_PER_M * scale);
    this.H = Math.round(heightM * PX_PER_M * scale);
    this.canvas = document.createElement('canvas');
    this.canvas.width = this.W;
    this.canvas.height = this.H;
    this.ctx = this.canvas.getContext('2d')!;
    this.texture = new CanvasTexture(this.canvas);
    this.texture.colorSpace = SRGBColorSpace;
    this.texture.anisotropy = 4;
    const material = new MeshBasicMaterial({ map: this.texture, transparent: opts.transparent ?? true, toneMapped: false, depthWrite: !(opts.transparent ?? true) });
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

// ---------------------------------------------------------------------------
// Pressables: one registry, one system (see systems.ts) for hover feedback.
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
  active = false;

  constructor(world: World, parent: Entity, opts: ButtonOptions, onPress: () => void) {
    this.opts = { width: 0.11, height: 0.11, primary: false, dark: false, ...opts };
    this.panel = new CanvasPanel(this.opts.width, this.opts.height);
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
    this.panel.draw((ctx, W, H) => {
      const r = Math.min(W, H) * 0.28;
      const bg = primary || this.active ? COLORS.green : dark ? 'rgba(30,38,36,0.92)' : COLORS.cream;
      const fg = primary || this.active || dark ? '#ffffff' : COLORS.green;
      ctx.fillStyle = bg;
      roundRect(ctx, 6, 6, W - 12, H - 12, r);
      ctx.fill();
      if (hovered) {
        ctx.strokeStyle = '#9fd3ae';
        ctx.lineWidth = 10;
        roundRect(ctx, 8, 8, W - 16, H - 16, r);
        ctx.stroke();
      }
      const wide = W > H * 1.6;
      const iconSize = Math.min(H * 0.42, 96);
      ctx.fillStyle = fg;
      ctx.textBaseline = 'middle';
      if (icon && wide) {
        drawIcon(ctx, icon, H * 0.5, H / 2, iconSize, fg);
        ctx.font = `600 ${Math.round(H * 0.3)}px ${FONT}`;
        ctx.textAlign = 'left';
        ctx.fillText(label, H * 0.85, H / 2 + 2, W - H);
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
