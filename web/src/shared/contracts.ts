// Shared JSON contracts between the API (api/samesky/serializers.py), the companion and the XR room.
// Keep in sync with docs/API.md and contracts/recap-plan.schema.json.

export type RoomKind = 'direct' | 'circle';
export type MomentKind = 'photo' | 'clip' | 'voice' | 'text';
export type SourceDevice = 'phone' | 'tablet' | 'webcam' | 'laptop' | 'camera' | 'action_camera' | 'drone' | 'glasses' | 'other';
export type ObjectType = 'frame' | 'cup' | 'postcard' | 'audioStone' | 'note';
export type ReactionKind = 'heart' | 'smile' | 'wow' | 'hug' | 'sun';

export interface AvatarDefinition {
  skin: string;
  hair: string;
  hairColor: string;
  top: string;
  accessory: string;
  /** Sculpted face preset. Optional so definitions saved before it existed still render ("soft"). */
  face?: 'soft' | 'defined';
}

export interface UserPublic {
  id: string;
  displayName: string;
  timeZone: string;
  avatar: { definition: AvatarDefinition; version: number } | null;
}

export interface Me extends UserPublic {
  email: string;
  quietStart: string | null;
  quietEnd: string | null;
  preferredCallMinutes: number;
  shareAvailabilityHints: boolean;
  notificationPrefs: Record<string, boolean>;
  callLink: string | null;
  likenessConsent: boolean;
}

export interface Member extends UserPublic {
  role: 'owner' | 'member';
  label: string | null;
  city?: string;
}

export interface Room {
  id: string;
  kind: RoomKind;
  name: string;
  capacity: number;
  reportingZone: string;
  dailyDraftTime: string | null;
  isOwner: boolean;
  muted: boolean;
  members: Member[];
}

export interface MediaInfo {
  id: string;
  kind: 'image' | 'video' | 'audio';
  processingState: 'pending' | 'processing' | 'ready' | 'failed';
  safeError?: string | null;
  sourceDevice: SourceDevice;
  projection: 'flat' | 'equirect360' | 'stereo_sbs';
  durationMs: number | null;
  width: number | null;
  height: number | null;
  rotation?: number;
  captureAt?: string | null;
  needsCaptureTime?: boolean;
  url?: string;
  thumbUrl?: string;
  posterUrl?: string;
  captionsUrl?: string;
}

export interface Moment {
  id: string;
  roomId: string;
  ownerId: string;
  ownerName: string;
  kind: MomentKind;
  caption: string;
  captureAt: string;
  captureLocal: { time: string; date: string; zone: string; abbreviation: string };
  roomDate: string;
  sharingState: 'private' | 'shared' | 'withdrawn';
  sourceVersion: number;
  sharingVersion: number;
  audience: { userId: string; displayName: string }[];
  isMine: boolean;
  aiAllowed?: boolean;
  transcript?: string;
  recordedGesture?: string | null;
  reactions: { userId: string; kind: ReactionKind }[];
  media: MediaInfo | null;
}

export interface Reply {
  id: string;
  momentId: string;
  authorId: string;
  authorName: string;
  text: string;
  createdAt: string;
  label: 'Recorded message';
}

export interface SourceRef {
  momentId: string;
  sourceVersion: number;
  sharingVersion: number;
  transcriptStartMs?: number;
  transcriptEndMs?: number;
}

export interface RecapSegment {
  text: string;
  kind: 'stated' | 'visual_interpretation' | 'quote';
  sources: SourceRef[];
}

export interface RecapChapter {
  contributorId: string;
  segments: RecapSegment[];
  playbackMomentIds: string[];
  objectType: 'frame' | 'cup' | 'postcard' | 'audioStone';
  durationSeconds: number;
}

/** RecapPlan schemaVersion 2 (see brief §11). The server fills every identity/version field. */
export interface RecapPlan {
  schemaVersion: 2;
  recapId: string;
  roomId: string;
  audienceSnapshotId: string;
  roomDate: string;
  roomTimeZone: string;
  title: string;
  closingLine?: string;
  omittedCount?: number;
  fallback?: 'chronological';
  chapters: RecapChapter[];
}

export type RecapState = 'queued' | 'generating' | 'private_preview' | 'awaiting_approval' | 'shared' | 'invalidated' | 'failed';

export interface Recap {
  id: string;
  roomId: string;
  roomDate: string;
  version: number;
  state: RecapState;
  generator: 'ai' | 'chronological' | 'pending' | 'sample';
  generatorLabel: string;
  omittedCount: number;
  invalidationReason: string | null;
  contributors: { userId: string; displayName: string; approved: boolean }[];
  publishedAt: string | null;
  plan: RecapPlan | null;
  playback: Moment[];
  audience?: { userId: string; displayName: string }[];
}

export interface Award {
  milestone: 'first_shared_day' | 'conversation_continued' | 'seven_days';
  title: string;
  trigger: string;
  keepsake: string;
  earned: boolean;
  earnedAt: string | null;
  progress: number;
  target: number;
  sourceRecapId: string | null;
}

export interface WeeklyRule {
  weekday: number; // 0 = Monday
  start: string;
  end: string;
}

export interface MyAvailability {
  timeZone: string;
  rules: WeeklyRule[];
  exceptions: { id: string; kind: string; start: string; end: string; roomId: string | null }[];
  freeNowUntil: string | null;
}

export interface LocalLabel {
  userId: string;
  displayName: string;
  zone: string;
  local: string;
  date: string;
  time: string;
  endTime: string;
  weekday: string;
  abbreviation: string;
}

export interface OpportunityResult {
  participants: { userId: string; displayName: string; timeZone: string; freeNow: boolean }[];
  participantLabel: string;
  allMarkedFreeNow: boolean;
  statusMessage: string | null;
  quietHoursRespected: boolean;
  candidates: { startUtc: string; endUtc: string; minutes: number; message: string; local: LocalLabel[] }[];
  simulated?: boolean;
}

export interface InboxItem {
  id: string;
  kind: 'recap_ready' | 'reply_received' | 'call_opportunity';
  roomId: string;
  resourceType: string;
  resourceId: string;
  text: string;
  createdAt: string;
  read: boolean;
  pushState?: string;
}

export interface RoomObject {
  id: string;
  objectType: ObjectType;
  momentId: string | null;
  replyId: string | null;
  ownerId: string;
  transform: { x: number; y: number; z: number; yaw: number };
  version: number;
}

export interface AudienceResolution {
  recipients: { userId: string; displayName: string }[];
  label: string;
  confirmationToken: string;
}

export interface ApiErrorBody {
  error: { code: string; message: string; retryable: boolean };
}
