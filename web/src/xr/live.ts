// Client for the app-owned realtime channel (api/samesky/realtime.py).
// Poses: ephemeral, 15 Hz, table-relative. Playback: server-authorized commands + clock-offset estimate.

import { API_BASE } from '../shared/data';
import type { AvatarDefinition } from '../shared/contracts';

export interface PlaybackState {
  sessionId: string;
  momentId: string;
  version: number;
  playheadMs: number;
  paused: boolean;
  stateAt: number; // server epoch ms
  serverTime: number;
  by?: string;
}

export interface Participant {
  userId: string;
  displayName: string;
  avatar: { definition: AvatarDefinition; version: number } | null;
  online: boolean;
}

export interface PoseMessage {
  tracked: boolean;
  source: 'xr' | 'pointer' | 'animation';
  head: { p: number[]; q: number[] };
  left?: { p: number[]; q: number[] };
  right?: { p: number[]; q: number[] };
}

export interface LiveHandlers {
  onWelcome(me: string, state: PlaybackState, participants: Participant[]): void;
  onState(state: PlaybackState): void;
  onPose(userId: string, pose: PoseMessage): void;
  onPeerJoined(userId: string, name: string | undefined, avatar: Participant['avatar']): void;
  onPeerLeft(userId: string, reason: string): void;
  onReaction(userId: string, kind: string): void;
  onClosed(reason: string, final: boolean): void;
}

export class LiveSession {
  private ws: WebSocket | null = null;
  private offsets: { offset: number; rtt: number }[] = [];
  private pingTimer = 0;
  private retry = 0;
  private closedForGood = false;
  state: PlaybackState | null = null;
  clockOffset = 0;
  rtt = 0;

  constructor(private sessionId: string, private token: string, private h: LiveHandlers) {}

  connect() {
    const base = (API_BASE || location.origin).replace(/^http/, 'ws');
    const ws = new WebSocket(`${base}/v1/realtime`);
    this.ws = ws;
    ws.onopen = () => {
      ws.send(JSON.stringify({ type: 'auth', token: this.token }));
      ws.send(JSON.stringify({ type: 'join', sessionId: this.sessionId }));
      this.retry = 0;
      this.ping();
      this.pingTimer = window.setInterval(() => this.ping(), 4000);
    };
    ws.onmessage = (ev) => this.handle(JSON.parse(ev.data));
    ws.onclose = (ev) => {
      window.clearInterval(this.pingTimer);
      const final = this.closedForGood || [4401, 4403, 4404, 4410].includes(ev.code);
      this.h.onClosed(final ? closeReason(ev.code) : 'reconnecting', final);
      if (!final) {
        // Reconnect with backoff; the server re-sends authoritative state on join.
        const delay = Math.min(10_000, 500 * 2 ** this.retry++);
        window.setTimeout(() => this.connect(), delay);
      }
    };
  }

  private ping() {
    this.send({ type: 'ping', t: Date.now() });
  }

  private handle(msg: Record<string, unknown>) {
    switch (msg.type) {
      case 'welcome': {
        const state = msg.state as PlaybackState;
        this.state = state;
        this.h.onWelcome(msg.userId as string, state, msg.participants as Participant[]);
        break;
      }
      case 'state':
        this.state = msg as unknown as PlaybackState;
        this.h.onState(this.state);
        break;
      case 'pose':
        this.h.onPose(msg.userId as string, msg.pose as PoseMessage);
        break;
      case 'peer_joined':
        this.h.onPeerJoined(msg.userId as string, msg.displayName as string | undefined, msg.avatar as Participant['avatar']);
        break;
      case 'peer_left':
        this.h.onPeerLeft(msg.userId as string, msg.reason as string);
        break;
      case 'reaction':
        this.h.onReaction(msg.userId as string, msg.kind as string);
        break;
      case 'pong': {
        const now = Date.now();
        const sent = msg.t as number;
        const rtt = now - sent;
        const offset = (msg.serverTime as number) - (sent + rtt / 2);
        this.offsets.push({ offset, rtt });
        if (this.offsets.length > 8) this.offsets.shift();
        const best = [...this.offsets].sort((a, b) => a.rtt - b.rtt)[0];
        this.clockOffset = best.offset;
        this.rtt = best.rtt;
        break;
      }
      case 'removed':
      case 'session_ended':
      case 'error':
        this.closedForGood = true;
        this.h.onClosed(String(msg.reason ?? msg.code ?? msg.type), true);
        break;
      default:
        break;
    }
  }

  send(obj: unknown) {
    if (this.ws?.readyState === WebSocket.OPEN) this.ws.send(JSON.stringify(obj));
  }

  serverNow() {
    return Date.now() + this.clockOffset;
  }

  /** Where the shared playhead should be right now, in ms. */
  expectedPositionMs(): number | null {
    const s = this.state;
    if (!s) return null;
    return s.paused ? s.playheadMs : s.playheadMs + (this.serverNow() - s.stateAt);
  }

  command(action: 'play' | 'pause' | 'seek', positionMs?: number) {
    this.send({ type: 'playback', action, positionMs, baseVersion: this.state?.version });
  }

  pose(p: PoseMessage) {
    this.send({ type: 'pose', pose: p });
  }

  react(kind: string) {
    this.send({ type: 'reaction', kind });
  }

  leave() {
    this.closedForGood = true;
    this.send({ type: 'leave' });
    this.ws?.close();
  }
}

function closeReason(code: number) {
  return ({ 4401: 'Sign in again to join.', 4403: 'You no longer have access to this room.', 4404: "This session isn't available to you.", 4410: 'The viewing session ended.' } as Record<number, string>)[code] ?? 'Disconnected.';
}
