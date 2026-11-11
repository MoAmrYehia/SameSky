// One DataSource interface, two implementations:
//  * LiveSource   - the real authenticated API (all authorization enforced server-side).
//  * SampleSource - the judge-friendly sample day: no login, no network, no AI calls, no push.
//                   Edits live in isolated local storage (+ IndexedDB for media) and can be reset.
//                   It has no route to real rooms and never sends real notifications.

import type {
  AudienceResolution, Award, AvatarAssetInfo, AvatarConsentInfo, AvatarDefinition, AvatarJobInfo, AvatarScope, AvatarSourceInfo,
  AvatarStudio, AvatarView, InboxItem, MediaInfo, Me, Moment, MomentKind, MyAvailability, OpportunityResult, ReactionKind, Recap, RecapPlan, Reply,
  Room, RoomObject, SourceDevice, WeeklyRule,
} from './contracts';
import { ME, SAMPLE_DAY, SAMPLE_PEOPLE, SAMPLE_ROOMS, SAMPLE_SCHEDULES, SEED_AWARDS, SEED_INBOX, SEED_REPLIES, seedMoments, seedRecaps } from './sample';
import { commonOverlaps, formatTime, isBlockedNow, wallParts, zoneAbbreviation } from './tz';

export interface NewMomentInput {
  roomId: string;
  kind: MomentKind;
  caption: string;
  file?: Blob;
  fileName?: string;
  sourceDevice: SourceDevice;
  /** sample mode only (never sent to the API): how sample media was made */
  origin?: MediaInfo['origin'];
  captureAt?: string;
  aiAllowed: boolean;
  trimStartMs?: number;
  trimEndMs?: number;
}

export interface DataSource {
  readonly mode: 'sample' | 'live';
  me(): Promise<Me>;
  updateMe(patch: Partial<Record<string, unknown>>): Promise<Me>;
  saveAvatar(def: AvatarDefinition, likenessConsent: boolean): Promise<void>;
  // photo-derived avatars (owner only; unavailable in the sample, whose people are fictional)
  avatarStudio(): Promise<AvatarStudio>;
  avatarConsent(provider: 'metaperson' | 'manual_import', processing: boolean, scope: AvatarScope): Promise<AvatarConsentInfo>;
  withdrawAvatarConsent(id: string): Promise<void>;
  uploadAvatarSource(file: Blob, fileName: string, view: 'front' | 'left' | 'right', consentId: string): Promise<AvatarSourceInfo>;
  deleteAvatarSource(id: string): Promise<void>;
  startAvatarJob(sourceIds: string[], consentId: string, options: { body: string; outfit?: string | null; remove_glasses?: boolean }, key: string): Promise<AvatarJobInfo>;
  avatarJob(id: string): Promise<AvatarJobInfo>;
  cancelAvatarJob(id: string): Promise<AvatarJobInfo>;
  retryAvatarJob(id: string): Promise<AvatarJobInfo>;
  importAvatarModel(file: Blob, fileName: string, consentId: string): Promise<AvatarAssetInfo>;
  approveAvatar(id: string, visibility: AvatarScope): Promise<AvatarAssetInfo>;
  rejectAvatar(id: string): Promise<AvatarAssetInfo>;
  activateAvatar(id: string): Promise<AvatarAssetInfo>;
  setAvatarVisibility(id: string, visibility: AvatarScope): Promise<AvatarAssetInfo>;
  deleteAvatar(id: string): Promise<AvatarAssetInfo>;
  useDemoAvatar(): Promise<void>;
  deleteAvatarData(): Promise<{ assets: number; sources: number }>;
  userAvatar(userId: string): Promise<{ userId: string; displayName: string; avatar: AvatarView | null }>;
  rooms(): Promise<Room[]>;
  createRoom(kind: 'direct' | 'circle', name: string): Promise<Room>;
  invite(roomId: string): Promise<{ token: string; code: string; path: string; expiresAt: string }>;
  redeem(token: string): Promise<Room>;
  /** Join with the short code shown beside an invitation (e.g. typed from a headset). */
  redeemCode(code: string): Promise<Room>;
  removeMember(roomId: string, userId: string): Promise<void>;
  setRoom(roomId: string, patch: { muted?: boolean; daily_draft_time?: string }): Promise<Room>;
  moments(roomId: string, date?: string): Promise<Moment[]>;
  createMoment(input: NewMomentInput, onStatus?: (status: string) => void): Promise<Moment>;
  resolveAudience(momentId: string, recipientIds: string[]): Promise<AudienceResolution>;
  share(momentId: string, recipientIds: string[], token: string): Promise<Moment>;
  withdraw(momentId: string): Promise<void>;
  replies(momentId: string): Promise<Reply[]>;
  reply(momentId: string, text: string): Promise<Reply>;
  react(momentId: string, kind: ReactionKind): Promise<void>;
  archive(roomId: string): Promise<Recap[]>;
  pendingRecaps(roomId: string): Promise<Recap[]>;
  requestRecap(roomId: string, date: string, recipientIds?: string[]): Promise<Recap>;
  recap(id: string): Promise<Recap>;
  approveRecap(id: string): Promise<Recap>;
  awards(roomId: string): Promise<Award[]>;
  availability(roomId: string): Promise<MyAvailability>;
  setRules(roomId: string, rules: WeeklyRule[]): Promise<void>;
  setFreeNow(roomId: string, on: boolean): Promise<string | null>;
  opportunities(roomId: string, participantIds: string[]): Promise<OpportunityResult>;
  suggest(roomId: string, participantIds: string[], startUtc: string): Promise<void>;
  respond(oppId: string, action: 'connect' | 'later' | 'not_now' | 'pause'): Promise<{ handoffLink?: string | null; roomId?: string }>;
  inbox(): Promise<{ unread: number; items: InboxItem[] }>;
  markRead(id: string): Promise<void>;
  objects(roomId: string): Promise<RoomObject[]>;
  placeObject(roomId: string, obj: { object_type: string; moment_id?: string; reply_id?: string; x: number; y: number; z: number; yaw: number }): Promise<RoomObject>;
  moveObject(id: string, version: number, t: { x: number; y: number; z: number; yaw: number }): Promise<RoomObject>;
  createSession(roomId: string, momentId: string, participantId: string): Promise<{ id: string }>;
  sessions(roomId: string): Promise<{ id: string; momentId: string; participantIds: string[]; hostId: string }[]>;
  token(): string | null;
}

export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string, public retryable = false) {
    super(message);
  }
}

// ---------------------------------------------------------------------------
// Live API
// ---------------------------------------------------------------------------

const TOKEN_KEY = 'samesky.token';
export const API_BASE: string = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? '';

function safeStorage(): Storage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

export function storedToken(): string | null {
  return safeStorage()?.getItem(TOKEN_KEY) ?? null;
}

export function storeToken(token: string | null) {
  const s = safeStorage();
  if (!s) return;
  if (token) s.setItem(TOKEN_KEY, token);
  else s.removeItem(TOKEN_KEY);
}

export async function apiFetch<T>(path: string, init: RequestInit = {}, token = storedToken()): Promise<T> {
  const headers = new Headers(init.headers);
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  let res: Response;
  try {
    res = await fetch(API_BASE + path, { ...init, headers });
  } catch {
    throw new ApiError(0, 'network', "Can't reach SameSky right now. Check your connection and try again.", true);
  }
  if (!res.ok) {
    let code = 'error';
    let message = `Something went wrong (${res.status}).`;
    let retryable = res.status >= 500;
    try {
      const body = await res.json();
      code = body.error?.code ?? code;
      message = body.error?.message ?? message;
      retryable = body.error?.retryable ?? retryable;
    } catch {
      /* not JSON */
    }
    throw new ApiError(res.status, code, message, retryable);
  }
  return (await res.json()) as T;
}

const json = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) });

export class LiveSource implements DataSource {
  readonly mode = 'live' as const;
  token() {
    return storedToken();
  }
  me() { return apiFetch<Me>('/v1/me'); }
  updateMe(patch: Partial<Record<string, unknown>>) { return apiFetch<Me>('/v1/me', { method: 'PATCH', body: JSON.stringify(patch) }); }
  avatarStudio() { return apiFetch<AvatarStudio>('/v1/me/avatar-studio'); }
  avatarConsent(provider: 'metaperson' | 'manual_import', processing: boolean, scope: AvatarScope) {
    return apiFetch<AvatarConsentInfo>('/v1/me/avatar-consents', json({ provider, processing, likeness_scope: scope }));
  }
  async withdrawAvatarConsent(id: string) { await apiFetch(`/v1/me/avatar-consents/${id}/withdraw`, json({})); }
  uploadAvatarSource(file: Blob, fileName: string, view: 'front' | 'left' | 'right', consentId: string) {
    const form = new FormData();
    form.append('file', file, fileName);
    form.append('view', view);
    form.append('consent_id', consentId);
    return apiFetch<AvatarSourceInfo>('/v1/me/avatar-sources', { method: 'POST', body: form });
  }
  async deleteAvatarSource(id: string) { await apiFetch(`/v1/me/avatar-sources/${id}`, { method: 'DELETE' }); }
  startAvatarJob(sourceIds: string[], consentId: string, options: { body: string; outfit?: string | null; remove_glasses?: boolean }, key: string) {
    return apiFetch<AvatarJobInfo>('/v1/me/avatar-jobs', json({ source_ids: sourceIds, consent_id: consentId, options, idempotency_key: key }));
  }
  avatarJob(id: string) { return apiFetch<AvatarJobInfo>(`/v1/me/avatar-jobs/${id}`); }
  cancelAvatarJob(id: string) { return apiFetch<AvatarJobInfo>(`/v1/me/avatar-jobs/${id}/cancel`, json({})); }
  retryAvatarJob(id: string) { return apiFetch<AvatarJobInfo>(`/v1/me/avatar-jobs/${id}/retry`, json({})); }
  importAvatarModel(file: Blob, fileName: string, consentId: string) {
    const form = new FormData();
    form.append('file', file, fileName);
    form.append('consent_id', consentId);
    form.append('attested', 'true');
    return apiFetch<AvatarAssetInfo>('/v1/me/avatar-assets', { method: 'POST', body: form });
  }
  approveAvatar(id: string, visibility: AvatarScope) { return apiFetch<AvatarAssetInfo>(`/v1/me/avatar-assets/${id}/approve`, json({ visibility, confirmed: true })); }
  rejectAvatar(id: string) { return apiFetch<AvatarAssetInfo>(`/v1/me/avatar-assets/${id}/reject`, json({})); }
  activateAvatar(id: string) { return apiFetch<AvatarAssetInfo>(`/v1/me/avatar-assets/${id}/activate`, json({})); }
  setAvatarVisibility(id: string, visibility: AvatarScope) { return apiFetch<AvatarAssetInfo>(`/v1/me/avatar-assets/${id}`, { method: 'PATCH', body: JSON.stringify({ visibility }) }); }
  deleteAvatar(id: string) { return apiFetch<AvatarAssetInfo>(`/v1/me/avatar-assets/${id}`, { method: 'DELETE' }); }
  async useDemoAvatar() { await apiFetch('/v1/me/avatar/demo', json({})); }
  deleteAvatarData() { return apiFetch<{ assets: number; sources: number }>('/v1/me/avatar-data', { method: 'DELETE' }); }
  userAvatar(userId: string) { return apiFetch<{ userId: string; displayName: string; avatar: AvatarView | null }>(`/v1/users/${userId}/avatar`); }
  async saveAvatar(def: AvatarDefinition, likenessConsent: boolean) {
    await apiFetch('/v1/me/avatar', { method: 'PUT', body: JSON.stringify({ ...def, likeness_consent: likenessConsent }) });
  }
  rooms() { return apiFetch<Room[]>('/v1/rooms'); }
  createRoom(kind: 'direct' | 'circle', name: string) { return apiFetch<Room>('/v1/rooms', json({ kind, name })); }
  invite(roomId: string) { return apiFetch<{ token: string; code: string; path: string; expiresAt: string }>(`/v1/rooms/${roomId}/invites`, { method: 'POST' }); }
  redeem(token: string) { return apiFetch<Room>('/v1/invites/redeem', json({ token })); }
  redeemCode(code: string) { return apiFetch<Room>('/v1/invites/redeem-code', json({ code })); }
  async removeMember(roomId: string, userId: string) { await apiFetch(`/v1/rooms/${roomId}/members/${userId}`, { method: 'DELETE' }); }
  setRoom(roomId: string, patch: { muted?: boolean; daily_draft_time?: string }) { return apiFetch<Room>(`/v1/rooms/${roomId}`, { method: 'PATCH', body: JSON.stringify(patch) }); }
  moments(roomId: string, date?: string) { return apiFetch<Moment[]>(`/v1/rooms/${roomId}/moments${date ? `?room_date=${date}` : ''}`); }
  async createMoment(input: NewMomentInput, onStatus?: (s: string) => void): Promise<Moment> {
    let mediaId: string | undefined;
    if (input.file) {
      const form = new FormData();
      form.append('file', input.file, input.fileName ?? 'upload');
      form.append('kind', input.kind === 'photo' ? 'image' : input.kind === 'clip' ? 'video' : 'audio');
      form.append('source_device', input.sourceDevice);
      if (input.captureAt) form.append('capture_at', input.captureAt);
      if (input.trimStartMs != null) form.append('trim_start_ms', String(Math.round(input.trimStartMs)));
      if (input.trimEndMs != null) form.append('trim_end_ms', String(Math.round(input.trimEndMs)));
      onStatus?.('Uploading…');
      const asset = await apiFetch<{ id: string }>('/v1/media', { method: 'POST', body: form });
      mediaId = asset.id;
      for (let i = 0; i < 120; i++) {
        const status = await apiFetch<{ processingState: string; safeError?: string }>(`/v1/media/${asset.id}`);
        if (status.processingState === 'ready') break;
        if (status.processingState === 'failed') throw new ApiError(422, 'processing_failed', (status.safeError ?? 'Processing failed.').replace(/^[a-z_]+: /, ''));
        onStatus?.(status.processingState === 'processing' ? 'Processing for playback…' : 'Waiting for the processor…');
        await new Promise((r) => setTimeout(r, 1000));
        if (i === 119) throw new ApiError(504, 'processing_timeout', 'Processing is taking unusually long. Your upload is saved; check again shortly.', true);
      }
    }
    onStatus?.('Saving privately…');
    return apiFetch<Moment>(`/v1/rooms/${input.roomId}/moments`, json({
      kind: input.kind, caption: input.caption, media_asset_id: mediaId, capture_at: input.captureAt, ai_allowed: input.aiAllowed,
    }));
  }
  resolveAudience(momentId: string, recipientIds: string[]) { return apiFetch<AudienceResolution>(`/v1/moments/${momentId}/audience/resolve`, json({ recipient_ids: recipientIds })); }
  share(momentId: string, recipientIds: string[], token: string) { return apiFetch<Moment>(`/v1/moments/${momentId}/share`, json({ recipient_ids: recipientIds, confirmation_token: token })); }
  async withdraw(momentId: string) { await apiFetch(`/v1/moments/${momentId}/withdraw`, json({})); }
  replies(momentId: string) { return apiFetch<Reply[]>(`/v1/moments/${momentId}/replies`); }
  reply(momentId: string, text: string) { return apiFetch<Reply>(`/v1/moments/${momentId}/replies`, json({ text, client_key: crypto.randomUUID() })); }
  async react(momentId: string, kind: ReactionKind) { await apiFetch(`/v1/moments/${momentId}/reactions`, json({ kind })); }
  archive(roomId: string) { return apiFetch<Recap[]>(`/v1/rooms/${roomId}/recaps`); }
  pendingRecaps(roomId: string) { return apiFetch<Recap[]>(`/v1/rooms/${roomId}/recaps/pending`); }
  requestRecap(roomId: string, date: string, recipientIds?: string[]) { return apiFetch<Recap>(`/v1/rooms/${roomId}/recaps`, json({ room_date: date, recipient_ids: recipientIds })); }
  recap(id: string) { return apiFetch<Recap>(`/v1/recaps/${id}`); }
  approveRecap(id: string) { return apiFetch<Recap>(`/v1/recaps/${id}/approve`, { method: 'POST' }); }
  awards(roomId: string) { return apiFetch<Award[]>(`/v1/rooms/${roomId}/awards`); }
  availability(roomId: string) { return apiFetch<MyAvailability>(`/v1/rooms/${roomId}/availability`); }
  async setRules(roomId: string, rules: WeeklyRule[]) { await apiFetch(`/v1/rooms/${roomId}/availability/rules`, { method: 'PUT', body: JSON.stringify({ rules }) }); }
  async setFreeNow(roomId: string, on: boolean) {
    const r = await apiFetch<{ freeNowUntil: string | null }>(`/v1/rooms/${roomId}/free-now`, { method: on ? 'POST' : 'DELETE' });
    return r.freeNowUntil;
  }
  opportunities(roomId: string, participantIds: string[]) { return apiFetch<OpportunityResult>(`/v1/rooms/${roomId}/opportunities`, json({ participant_ids: participantIds })); }
  async suggest(roomId: string, participantIds: string[], startUtc: string) { await apiFetch(`/v1/rooms/${roomId}/opportunities/suggest`, json({ participant_ids: participantIds, start_utc: startUtc })); }
  respond(oppId: string, action: 'connect' | 'later' | 'not_now' | 'pause') { return apiFetch<{ handoffLink?: string | null; roomId?: string }>(`/v1/opportunities/${oppId}/respond`, json({ action })); }
  inbox() { return apiFetch<{ unread: number; items: InboxItem[] }>('/v1/notifications'); }
  async markRead(id: string) { await apiFetch(`/v1/notifications/${id}/read`, { method: 'POST' }); }
  objects(roomId: string) { return apiFetch<RoomObject[]>(`/v1/rooms/${roomId}/objects`); }
  placeObject(roomId: string, obj: { object_type: string; moment_id?: string; reply_id?: string; x: number; y: number; z: number; yaw: number }) { return apiFetch<RoomObject>(`/v1/rooms/${roomId}/objects`, json(obj)); }
  moveObject(id: string, version: number, t: { x: number; y: number; z: number; yaw: number }) { return apiFetch<RoomObject>(`/v1/objects/${id}`, { method: 'PATCH', body: JSON.stringify({ expected_version: version, ...t }) }); }
  createSession(roomId: string, momentId: string, participantId: string) { return apiFetch<{ id: string }>(`/v1/rooms/${roomId}/sessions`, json({ moment_id: momentId, participant_ids: [participantId] })); }
  sessions(roomId: string) { return apiFetch<{ id: string; momentId: string; participantIds: string[]; hostId: string }[]>(`/v1/rooms/${roomId}/sessions`); }
}

// ---------------------------------------------------------------------------
// Sample (offline, isolated)
// ---------------------------------------------------------------------------

const SAMPLE_KEY = 'samesky.sample.v1';

interface SampleState {
  replies: Reply[];
  reactions: { momentId: string; kind: ReactionKind }[];
  added: Moment[];
  withdrawn: string[];
  objects: RoomObject[];
  rules: WeeklyRule[] | null;
  freeNowUntil: string | null;
  profile: Partial<Me>;
  avatar: AvatarDefinition | null;
  read: string[];
  recaps: Recap[];
  pausedUntil: string | null;
}

const emptyState = (): SampleState => ({
  replies: [], reactions: [], added: [], withdrawn: [], objects: [], rules: null, freeNowUntil: null, profile: {}, avatar: null, read: [], recaps: [], pausedUntil: null,
});

const DEFAULT_OBJECTS: RoomObject[] = [
  { id: 'obj-cup', objectType: 'cup', momentId: 'm-maya-voice', replyId: null, ownerId: 'sample-maya', transform: { x: -0.16, y: 0, z: 0.06, yaw: 0 }, version: 1 },
  { id: 'obj-frame-park', objectType: 'frame', momentId: 'm-maya-park', replyId: null, ownerId: 'sample-maya', transform: { x: -0.36, y: 0, z: -0.16, yaw: 20 }, version: 1 },
  { id: 'obj-frame-water', objectType: 'frame', momentId: 'm-adam-waterfront', replyId: null, ownerId: 'sample-adam', transform: { x: 0.36, y: 0, z: -0.16, yaw: -20 }, version: 1 },
  { id: 'obj-postcard-ride', objectType: 'postcard', momentId: 'm-adam-ride', replyId: null, ownerId: 'sample-adam', transform: { x: 0.14, y: 0, z: 0.1, yaw: -12 }, version: 1 },
];

const blobUrls = new Map<string, string>();

function idb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open('samesky-sample', 1);
    req.onupgradeneeded = () => req.result.createObjectStore('media');
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function idbPut(key: string, blob: Blob) {
  try {
    const db = await idb();
    await new Promise<void>((res, rej) => {
      const tx = db.transaction('media', 'readwrite');
      tx.objectStore('media').put(blob, key);
      tx.oncomplete = () => res();
      tx.onerror = () => rej(tx.error);
    });
  } catch {
    /* storage unavailable: the media stays in memory for this visit only */
  }
}

async function idbGet(key: string): Promise<Blob | undefined> {
  try {
    const db = await idb();
    return await new Promise((res) => {
      const req = db.transaction('media').objectStore('media').get(key);
      req.onsuccess = () => res(req.result as Blob | undefined);
      req.onerror = () => res(undefined);
    });
  } catch {
    return undefined;
  }
}

export class SampleSource implements DataSource {
  readonly mode = 'sample' as const;
  private state: SampleState;
  private seed = seedMoments();

  constructor() {
    this.state = this.load();
  }

  token() {
    return null;
  }

  private load(): SampleState {
    try {
      const raw = safeStorage()?.getItem(SAMPLE_KEY);
      if (raw) return { ...emptyState(), ...JSON.parse(raw) };
    } catch {
      /* corrupted or blocked storage: start fresh */
    }
    return emptyState();
  }

  private save() {
    try {
      safeStorage()?.setItem(SAMPLE_KEY, JSON.stringify(this.state));
    } catch {
      /* private mode: edits last for this visit */
    }
    window.dispatchEvent(new CustomEvent('samesky:sample-changed'));
  }

  reset() {
    this.state = emptyState();
    try {
      safeStorage()?.removeItem(SAMPLE_KEY);
      indexedDB.deleteDatabase('samesky-sample');
    } catch {
      /* ignore */
    }
    blobUrls.forEach((url) => URL.revokeObjectURL(url));
    blobUrls.clear();
    this.save();
  }

  private allMoments(): Moment[] {
    const reactions = this.state.reactions;
    return [...this.seed, ...this.state.added]
      .filter((m) => !this.state.withdrawn.includes(m.id) || m.isMine)
      .map((m) => ({
        ...m,
        sharingState: this.state.withdrawn.includes(m.id) ? 'withdrawn' : m.sharingState,
        reactions: [...m.reactions, ...reactions.filter((r) => r.momentId === m.id).map((r) => ({ userId: ME, kind: r.kind }))],
      }));
  }

  private async hydrate(m: Moment): Promise<Moment> {
    if (!m.media || !m.id.startsWith('local-')) return m;
    let url = blobUrls.get(m.id);
    if (!url) {
      const blob = await idbGet(m.id);
      if (blob) {
        url = URL.createObjectURL(blob);
        blobUrls.set(m.id, url);
      }
    }
    return url ? { ...m, media: { ...m.media, url, thumbUrl: m.media.kind === 'image' ? url : undefined } } : { ...m, media: { ...m.media, processingState: 'failed', safeError: 'This sample upload is no longer stored in this browser.' } };
  }

  async me(): Promise<Me> {
    const p = SAMPLE_PEOPLE[ME];
    return {
      id: ME, displayName: p.name, timeZone: p.zone, email: 'sample@samesky.invalid',
      avatar: { definition: this.state.avatar ?? p.avatar, version: 1 }, quietStart: '23:00', quietEnd: '07:00', preferredCallMinutes: 15,
      shareAvailabilityHints: true, notificationPrefs: { push: false, call_suggestions: true, celebrations: true }, callLink: null, likenessConsent: false,
      ...this.state.profile,
    };
  }

  async updateMe(patch: Partial<Record<string, unknown>>): Promise<Me> {
    const map: Record<string, keyof Me> = { display_name: 'displayName', time_zone: 'timeZone', quiet_start: 'quietStart', quiet_end: 'quietEnd', call_link: 'callLink' };
    for (const [k, v] of Object.entries(patch)) {
      const key = map[k];
      if (key) (this.state.profile as Record<string, unknown>)[key] = v;
    }
    this.save();
    return this.me();
  }

  async avatarStudio(): Promise<AvatarStudio> {
    return {
      sample: true,
      provider: { name: 'metaperson', label: 'Avatar SDK MetaPerson (itSeez3D)', configured: false, photoViews: ['front'], bodies: ['female', 'male'], retentionDays: 7 },
      consentTextVersion: '2026-11-08',
      active: null,
      activeAssetId: null,
      assets: [],
      jobs: [],
      sources: [],
      consents: [],
    };
  }
  private sampleOnly(): never {
    throw new ApiError(400, 'sample_mode', 'Sign in to create your avatar from your own photos. The sample’s people are fictional and use labelled demo avatars.');
  }
  async avatarConsent(): Promise<AvatarConsentInfo> { return this.sampleOnly(); }
  async withdrawAvatarConsent(): Promise<void> { return this.sampleOnly(); }
  async uploadAvatarSource(): Promise<AvatarSourceInfo> { return this.sampleOnly(); }
  async deleteAvatarSource(): Promise<void> { return this.sampleOnly(); }
  async startAvatarJob(): Promise<AvatarJobInfo> { return this.sampleOnly(); }
  async avatarJob(): Promise<AvatarJobInfo> { return this.sampleOnly(); }
  async cancelAvatarJob(): Promise<AvatarJobInfo> { return this.sampleOnly(); }
  async retryAvatarJob(): Promise<AvatarJobInfo> { return this.sampleOnly(); }
  async importAvatarModel(): Promise<AvatarAssetInfo> { return this.sampleOnly(); }
  async approveAvatar(): Promise<AvatarAssetInfo> { return this.sampleOnly(); }
  async rejectAvatar(): Promise<AvatarAssetInfo> { return this.sampleOnly(); }
  async activateAvatar(): Promise<AvatarAssetInfo> { return this.sampleOnly(); }
  async setAvatarVisibility(): Promise<AvatarAssetInfo> { return this.sampleOnly(); }
  async deleteAvatar(): Promise<AvatarAssetInfo> { return this.sampleOnly(); }
  async useDemoAvatar(): Promise<void> { /* the sample always uses demo avatars */ }
  async deleteAvatarData(): Promise<{ assets: number; sources: number }> { return { assets: 0, sources: 0 }; }
  async userAvatar(userId: string) {
    const person = SAMPLE_PEOPLE[userId];
    return { userId, displayName: person?.name ?? 'Member', avatar: person ? { kind: 'preset_demo' as const, definition: person.avatar, version: 1 } : null };
  }
  async saveAvatar(def: AvatarDefinition) {
    this.state.avatar = def;
    this.save();
  }

  async rooms(): Promise<Room[]> {
    const me = await this.me();
    return SAMPLE_ROOMS.map((r) => ({
      ...r,
      members: r.members.map((m) => (m.id === ME ? { ...m, displayName: me.displayName, timeZone: me.timeZone, avatar: me.avatar } : m)),
    }));
  }

  async createRoom(): Promise<Room> {
    throw new ApiError(400, 'sample_mode', 'Sample mode has fixed rooms. Create an account to make your own.');
  }
  async invite(): Promise<{ token: string; code: string; path: string; expiresAt: string }> {
    throw new ApiError(400, 'sample_mode', 'Invitations need a real account. Sample mode never contacts anyone.');
  }
  async redeem(): Promise<Room> {
    throw new ApiError(400, 'sample_mode', 'Sign in to join a real room.');
  }
  async redeemCode(): Promise<Room> {
    throw new ApiError(400, 'sample_mode', 'Sign in to join a real room.');
  }
  async removeMember() {
    throw new ApiError(400, 'sample_mode', 'Sample members can’t be removed.');
  }
  async setRoom(roomId: string) {
    return (await this.rooms()).find((r) => r.id === roomId)!;
  }

  async moments(roomId: string, date?: string): Promise<Moment[]> {
    const list = this.allMoments().filter((m) => m.roomId === roomId && (!date || m.roomDate === date) && (m.isMine || m.audience.some((a) => a.userId === ME)));
    return Promise.all(list.sort((a, b) => a.captureAt.localeCompare(b.captureAt)).map((m) => this.hydrate(m)));
  }

  async createMoment(input: NewMomentInput, onStatus?: (s: string) => void): Promise<Moment> {
    const id = `local-${crypto.randomUUID()}`;
    const me = await this.me();
    const at = input.captureAt ?? new Date().toISOString();
    let media: Moment['media'] = null;
    if (input.file) {
      onStatus?.('Saving in this browser only…');
      await idbPut(id, input.file);
      const url = URL.createObjectURL(input.file);
      blobUrls.set(id, url);
      const kind = input.kind === 'photo' ? 'image' : input.kind === 'clip' ? 'video' : 'audio';
      media = { id: `asset-${id}`, kind, processingState: 'ready', sourceDevice: input.sourceDevice, origin: input.origin, projection: 'flat', durationMs: null, width: null, height: null, url, thumbUrl: kind === 'image' ? url : undefined };
    }
    const lp = wallParts(Date.parse(at), me.timeZone);
    const moment: Moment = {
      id, roomId: input.roomId, ownerId: ME, ownerName: me.displayName, kind: input.kind, caption: input.caption, captureAt: at,
      captureLocal: { time: formatTime(at, me.timeZone), date: `${lp.year}-${String(lp.month).padStart(2, '0')}-${String(lp.day).padStart(2, '0')}`, zone: me.timeZone, abbreviation: zoneAbbreviation(Date.parse(at), me.timeZone) },
      roomDate: SAMPLE_DAY, sharingState: 'private', sourceVersion: 1, sharingVersion: 0, audience: [], isMine: true, aiAllowed: input.aiAllowed, reactions: [], media,
    };
    this.state.added.push({ ...moment, media: media ? { ...media, url: undefined, thumbUrl: undefined } : null });
    this.save();
    return moment;
  }

  async resolveAudience(momentId: string, recipientIds: string[]): Promise<AudienceResolution> {
    const m = this.allMoments().find((x) => x.id === momentId);
    const room = SAMPLE_ROOMS.find((r) => r.id === m?.roomId);
    if (!m || !room) throw new ApiError(404, 'not_found', 'That moment isn’t available.');
    const recipients = recipientIds.filter((id) => room.members.some((x) => x.id === id) && id !== ME).map((id) => ({ userId: id, displayName: SAMPLE_PEOPLE[id].name }));
    if (!recipients.length) throw new ApiError(422, 'empty_audience', 'Choose at least one person to share with.');
    const label = recipients.length === 1 ? `Only ${recipients[0].displayName}` : `${room.name}: ${recipients.map((r) => r.displayName).join(', ')}`;
    return { recipients, label, confirmationToken: `sample:${momentId}:${recipients.map((r) => r.userId).sort().join(',')}` };
  }

  async share(momentId: string, recipientIds: string[], token: string): Promise<Moment> {
    const expected = `sample:${momentId}:${[...recipientIds].filter((i) => i !== ME).sort().join(',')}`;
    if (token !== expected) throw new ApiError(409, 'confirmation_required', 'Confirm the exact recipients before sharing.');
    const m = this.state.added.find((x) => x.id === momentId);
    if (!m) throw new ApiError(404, 'not_found', 'Only your own sample uploads can be shared.');
    m.sharingState = 'shared';
    m.sharingVersion += 1;
    m.audience = recipientIds.filter((i) => i !== ME).map((id) => ({ userId: id, displayName: SAMPLE_PEOPLE[id].name }));
    this.save();
    return this.hydrate(m);
  }

  async withdraw(momentId: string) {
    if (!this.state.withdrawn.includes(momentId)) this.state.withdrawn.push(momentId);
    this.state.recaps = this.state.recaps.map((r) => (r.plan?.chapters.some((c) => c.segments.some((s) => s.sources.some((x) => x.momentId === momentId))) ? { ...r, state: 'invalidated', plan: null, playback: [], invalidationReason: 'source_withdrawn' } : r));
    this.save();
  }

  async replies(momentId: string): Promise<Reply[]> {
    return [...SEED_REPLIES, ...this.state.replies].filter((r) => r.momentId === momentId);
  }

  async reply(momentId: string, text: string): Promise<Reply> {
    const me = await this.me();
    const reply: Reply = { id: `local-reply-${crypto.randomUUID()}`, momentId, authorId: ME, authorName: me.displayName, text, createdAt: new Date().toISOString(), label: 'Recorded message' };
    this.state.replies.push(reply);
    this.save();
    return reply;
  }

  async react(momentId: string, kind: ReactionKind) {
    if (!this.state.reactions.some((r) => r.momentId === momentId && r.kind === kind)) this.state.reactions.push({ momentId, kind });
    this.save();
  }

  private recaps(): Recap[] {
    const moments = this.allMoments();
    const withdrawn = new Set(this.state.withdrawn);
    const seeds = seedRecaps(moments).map((r) =>
      r.plan?.chapters.some((c) => c.segments.some((s) => s.sources.some((x) => withdrawn.has(x.momentId))))
        ? { ...r, state: 'invalidated' as const, plan: null, playback: [], invalidationReason: 'source_withdrawn' }
        : r,
    );
    return [...seeds, ...this.state.recaps];
  }

  async archive(roomId: string) {
    return this.recaps().filter((r) => r.roomId === roomId && r.state === 'shared').sort((a, b) => b.roomDate.localeCompare(a.roomDate) || b.version - a.version);
  }

  async pendingRecaps(roomId: string) {
    return this.recaps().filter((r) => r.roomId === roomId && (r.state === 'awaiting_approval' || r.state === 'private_preview'));
  }

  async requestRecap(roomId: string, date: string): Promise<Recap> {
    // Sample mode never calls a model: it builds the clearly-labelled chronological recap locally.
    const room = SAMPLE_ROOMS.find((r) => r.id === roomId)!;
    const everyone = room.members.map((m) => m.id);
    const eligible = (await this.moments(roomId, date)).filter((m) => m.sharingState === 'shared' && m.aiAllowed !== false && everyone.every((id) => id === m.ownerId || m.audience.some((a) => a.userId === id)));
    if (!eligible.length) throw new ApiError(409, 'nothing_shared', 'Nothing was shared with everyone in this room for that day yet.');
    const version = this.recaps().filter((r) => r.roomId === roomId && r.roomDate === date).length + 1;
    const id = `local-recap-${crypto.randomUUID()}`;
    const owners = [...new Set(eligible.map((m) => m.ownerId))];
    const plan: RecapPlan = {
      schemaVersion: 2, recapId: id, roomId, audienceSnapshotId: `sample-${roomId}`, roomDate: date, roomTimeZone: room.reportingZone,
      title: `Our day · version ${version}`, closingLine: "That's everything that was shared for this day.", fallback: 'chronological',
      chapters: owners.map((owner) => {
        const mine = eligible.filter((m) => m.ownerId === owner).slice(0, 3);
        return {
          contributorId: owner, objectType: mine[0].kind === 'voice' ? 'cup' : mine[0].kind === 'clip' ? 'postcard' : 'frame', durationSeconds: 20,
          playbackMomentIds: mine.filter((m) => m.kind !== 'text').map((m) => m.id),
          segments: mine.map((m) => ({ text: `${m.ownerName} shared ${m.kind === 'text' ? 'a note' : `a ${m.kind}`} at ${m.captureLocal.time}${m.caption ? `: “${m.caption}”` : '.'}`, kind: 'stated' as const, sources: [{ momentId: m.id, sourceVersion: m.sourceVersion, sharingVersion: m.sharingVersion }] })),
        };
      }),
    };
    const recap: Recap = {
      id, roomId, roomDate: date, version, state: 'awaiting_approval', generator: 'chronological', generatorLabel: 'Chronological recap (sample, no AI)', omittedCount: 0,
      invalidationReason: null, publishedAt: null, plan,
      contributors: owners.map((o) => ({ userId: o, displayName: SAMPLE_PEOPLE[o].name, approved: o !== ME })), playback: eligible.filter((m) => m.kind !== 'text'),
      audience: everyone.map((u) => ({ userId: u, displayName: SAMPLE_PEOPLE[u].name })),
    };
    this.state.recaps.push(recap);
    this.save();
    return recap;
  }

  async recap(id: string) {
    const r = this.recaps().find((x) => x.id === id);
    if (!r) throw new ApiError(404, 'not_found', 'That recap isn’t available.');
    return r;
  }

  async approveRecap(id: string) {
    const r = this.state.recaps.find((x) => x.id === id);
    if (!r) throw new ApiError(404, 'not_found', 'That recap isn’t available.');
    r.contributors = r.contributors.map((c) => (c.userId === ME ? { ...c, approved: true } : c));
    if (r.contributors.every((c) => c.approved)) {
      r.state = 'shared';
      r.publishedAt = new Date().toISOString();
    }
    this.save();
    return r;
  }

  async awards(roomId: string) {
    return SEED_AWARDS[roomId] ?? [];
  }

  async availability(): Promise<MyAvailability> {
    const me = await this.me();
    return { timeZone: me.timeZone, rules: this.state.rules ?? SAMPLE_SCHEDULES[ME].rules, exceptions: [], freeNowUntil: this.state.freeNowUntil && Date.parse(this.state.freeNowUntil) > Date.now() ? this.state.freeNowUntil : null };
  }

  async setRules(_roomId: string, rules: WeeklyRule[]) {
    this.state.rules = rules;
    this.save();
  }

  async setFreeNow(_roomId: string, on: boolean) {
    this.state.freeNowUntil = on ? new Date(Date.now() + 30 * 60_000).toISOString() : null;
    this.save();
    return this.state.freeNowUntil;
  }

  async opportunities(roomId: string, participantIds: string[]): Promise<OpportunityResult> {
    const me = await this.me();
    const room = SAMPLE_ROOMS.find((r) => r.id === roomId)!;
    const ids = [...new Set([ME, ...participantIds])].filter((id) => room.members.some((m) => m.id === id));
    const now = Date.now();
    const schedules = ids.map((id) => ({
      userId: id,
      zone: id === ME ? me.timeZone : SAMPLE_PEOPLE[id].zone,
      rules: id === ME ? this.state.rules ?? SAMPLE_SCHEDULES[id].rules : SAMPLE_SCHEDULES[id].rules,
      quiet: SAMPLE_SCHEDULES[id].quiet,
    }));
    const overlaps = commonOverlaps(schedules, now, 7);
    const myFree = !!this.state.freeNowUntil && Date.parse(this.state.freeNowUntil) > now;
    // Other people's Free now signals are SIMULATED in sample mode (labelled in the UI).
    const freeNow = (id: string) => (id === ME ? myFree : true);
    const all = ids.every(freeNow) && !schedules.some((s) => isBlockedNow(s, now));
    const others = ids.filter((i) => i !== ME).map((i) => SAMPLE_PEOPLE[i].name);
    const name = (id: string) => (id === ME ? me.displayName : SAMPLE_PEOPLE[id].name);
    return {
      simulated: true,
      participants: ids.map((id) => ({ userId: id, displayName: name(id), timeZone: schedules.find((s) => s.userId === id)!.zone, freeNow: freeNow(id) })),
      participantLabel: `${others.join(' and ')} and you`,
      allMarkedFreeNow: all,
      statusMessage: all ? (ids.length === 2 ? "You're both marked free now. Want to connect?" : `${others.join(' and ')} and you are all marked free now. Want to connect?`) : null,
      quietHoursRespected: true,
      candidates: overlaps.slice(0, 6).map(([s, e]) => ({
        startUtc: new Date(s).toISOString(), endUtc: new Date(e).toISOString(), minutes: Math.round((e - s) / 60_000),
        message: `Your chosen schedules overlap for ${Math.round((e - s) / 60_000)} minutes.`,
        local: schedules.map((sch) => {
          const p = wallParts(s, sch.zone);
          return {
            userId: sch.userId, displayName: name(sch.userId), zone: sch.zone, local: new Date(s).toISOString(), date: `${p.year}-${p.month}-${p.day}`,
            time: formatTime(s, sch.zone), endTime: formatTime(e, sch.zone), weekday: new Intl.DateTimeFormat('en-GB', { timeZone: sch.zone, weekday: 'long' }).format(s), abbreviation: zoneAbbreviation(s, sch.zone),
          };
        }),
      })),
    };
  }

  async suggest() {
    /* Sample mode never notifies anyone. */
  }

  async respond(_oppId: string, action: 'connect' | 'later' | 'not_now' | 'pause') {
    if (action === 'pause') {
      this.state.pausedUntil = new Date(Date.now() + 7 * 86_400_000).toISOString();
      this.save();
    }
    return { roomId: 'sample-direct', handoffLink: null };
  }

  async inbox() {
    const items = SEED_INBOX.map((n) => ({ ...n, read: n.read || this.state.read.includes(n.id) }));
    return { unread: items.filter((i) => !i.read).length, items };
  }

  async markRead(id: string) {
    if (!this.state.read.includes(id)) this.state.read.push(id);
    this.save();
  }

  async objects(roomId: string): Promise<RoomObject[]> {
    if (roomId !== 'sample-direct') return [];
    const withdrawn = new Set(this.state.withdrawn);
    const merged = DEFAULT_OBJECTS.map((d) => this.state.objects.find((o) => o.id === d.id) ?? d);
    const extra = this.state.objects.filter((o) => !DEFAULT_OBJECTS.some((d) => d.id === o.id));
    return [...merged, ...extra].filter((o) => !o.momentId || !withdrawn.has(o.momentId));
  }

  async placeObject(_roomId: string, obj: { object_type: string; moment_id?: string; reply_id?: string; x: number; y: number; z: number; yaw: number }) {
    const o: RoomObject = { id: `local-obj-${crypto.randomUUID()}`, objectType: obj.object_type as RoomObject['objectType'], momentId: obj.moment_id ?? null, replyId: obj.reply_id ?? null, ownerId: ME, transform: { x: obj.x, y: obj.y, z: obj.z, yaw: obj.yaw }, version: 1 };
    this.state.objects.push(o);
    this.save();
    return o;
  }

  async moveObject(id: string, version: number, t: { x: number; y: number; z: number; yaw: number }) {
    const current = (await this.objects('sample-direct')).find((o) => o.id === id);
    if (!current) throw new ApiError(404, 'not_found', 'That object isn’t here any more.');
    if (current.version !== version) throw new ApiError(409, 'version_conflict', 'This moved elsewhere; the latest position was loaded.');
    const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v));
    const next: RoomObject = { ...current, transform: { x: clamp(t.x, -0.55, 0.55), y: clamp(t.y, 0, 0.25), z: clamp(t.z, -0.35, 0.35), yaw: ((t.yaw % 360) + 360) % 360 }, version: version + 1 };
    this.state.objects = [...this.state.objects.filter((o) => o.id !== id), next];
    this.save();
    return next;
  }

  resetObjects() {
    this.state.objects = this.state.objects.filter((o) => !DEFAULT_OBJECTS.some((d) => d.id === o.id) && !!o.replyId);
    this.save();
  }

  async createSession(): Promise<{ id: string }> {
    throw new ApiError(400, 'sample_mode', 'Live sessions need two signed-in people. Sample mode shows a labelled simulation instead.');
  }
  async sessions() {
    return [];
  }
}

// ---------------------------------------------------------------------------
// Mode selection
// ---------------------------------------------------------------------------

const MODE_KEY = 'samesky.mode';

export function currentMode(): 'sample' | 'live' | null {
  const param = new URLSearchParams(location.search).get('mode');
  if (param === 'sample' || param === 'live') return param;
  const stored = safeStorage()?.getItem(MODE_KEY);
  if (stored === 'sample') return 'sample';
  if (stored === 'live' && storedToken()) return 'live';
  return null;
}

export function setMode(mode: 'sample' | 'live' | null) {
  const s = safeStorage();
  if (!s) return;
  if (mode) s.setItem(MODE_KEY, mode);
  else s.removeItem(MODE_KEY);
}

let singleton: DataSource | null = null;
export function dataSource(mode: 'sample' | 'live'): DataSource {
  if (!singleton || singleton.mode !== mode) singleton = mode === 'sample' ? new SampleSource() : new LiveSource();
  return singleton;
}
