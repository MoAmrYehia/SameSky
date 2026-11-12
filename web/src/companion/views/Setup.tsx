// Getting set up: the first-run flow for a signed-in person without a room, and the pieces it shares with the
// rest of the app (inviting someone, saying when you're usually free). Every step can be skipped and nothing is
// shared or sent until the person chooses it; an invite link goes only where they send it themselves.

import { useEffect, useState } from 'react';
import type { Room, WeeklyRule } from '../../shared/contracts';
import { COMMON_ZONES, detectZone } from '../../shared/tz';
import { useApp } from '../App';
import { Avatar, Icon, Notice } from '../ui';

// ---------------------------------------------------------------------------
// Inviting someone
// ---------------------------------------------------------------------------

/** Create a single-use invite link, then copy it or hand it to the system share sheet. */
export function InviteCard({ room, title, body }: { room: Room; title?: string; body?: string }) {
  const { ds, toast } = useApp();
  const [url, setUrl] = useState<string | null>(null);
  const [code, setCode] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const canShare = typeof navigator !== 'undefined' && typeof navigator.share === 'function';
  const make = async () => {
    setBusy(true);
    try {
      const inv = await ds.invite(room.id);
      setUrl(`${location.origin}/#/join/${inv.token}`);
      setCode(inv.code ?? null);
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not create an invite.');
    } finally {
      setBusy(false);
    }
  };
  const copy = async () => {
    if (!url) return;
    try {
      await navigator.clipboard.writeText(url);
      toast('Link copied. Send it to them however you usually talk.');
    } catch {
      toast('Copy didn’t work here — select the link and copy it.');
    }
  };
  const share = async () => {
    if (!url) return;
    try {
      await navigator.share({ title: `Join ${room.name} on SameSky`, text: 'A private room for the two of us — come sit at the table.', url });
    } catch {
      /* the person closed the share sheet */
    }
  };
  if (ds.mode === 'sample') {
    return <Notice>In the sample the room is already full of fictional people. Create an account to invite someone real.</Notice>;
  }
  return (
    <div className="invite-card">
      <div className="stack" style={{ gap: 4 }}>
        <strong>{title ?? (room.kind === 'direct' ? 'Invite the person you’re making this room for' : 'Invite people to your circle')}</strong>
        <span className="muted small">{body ?? 'Each link works once, for one person, for 72 hours. Nobody can find your room any other way.'}</span>
      </div>
      {url ? (
        <>
          <span className="code invite-url" aria-label="Invite link">{url}</span>
          {code ? (
            <span className="muted small invite-code-line">
              In a headset? They can type this code instead: <strong className="code invite-code" aria-label="Invite code">{code}</strong>
            </span>
          ) : null}
          <div className="row wrap">
            {canShare ? <button className="btn btn-primary" onClick={() => void share()}><Icon name="share" />Share link</button> : null}
            <button className={`btn ${canShare ? 'btn-outline' : 'btn-primary'}`} onClick={() => void copy()}><Icon name="link" />Copy link</button>
            <button className="btn btn-quiet" onClick={() => void make()} disabled={busy}>New link</button>
          </div>
        </>
      ) : (
        <button className="btn btn-primary" style={{ alignSelf: 'start' }} disabled={busy} onClick={() => void make()}><Icon name="link" />{busy ? 'Creating…' : 'Create invite link'}</button>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// When you're usually free: quick presets (the full editor lives in Settings)
// ---------------------------------------------------------------------------

const WEEKDAYS = [0, 1, 2, 3, 4];
const WEEKEND = [5, 6];
export const TIME_PRESETS: { id: string; label: string; rules: WeeklyRule[] }[] = [
  { id: 'weekday-evenings', label: 'Weekday evenings · 19:00–22:00', rules: WEEKDAYS.map((weekday) => ({ weekday, start: '19:00', end: '22:00' })) },
  { id: 'weekday-mornings', label: 'Weekday mornings · 07:00–08:30', rules: WEEKDAYS.map((weekday) => ({ weekday, start: '07:00', end: '08:30' })) },
  { id: 'weekday-lunch', label: 'Weekday lunch · 12:00–13:00', rules: WEEKDAYS.map((weekday) => ({ weekday, start: '12:00', end: '13:00' })) },
  { id: 'weekend-mornings', label: 'Weekend mornings · 09:00–12:00', rules: WEEKEND.map((weekday) => ({ weekday, start: '09:00', end: '12:00' })) },
  { id: 'weekend-evenings', label: 'Weekend evenings · 18:00–22:00', rules: WEEKEND.map((weekday) => ({ weekday, start: '18:00', end: '22:00' })) },
];

/** Pick a few usual windows (in your own time zone); only overlaps are ever shown to others. */
export function QuickTimes({ room, onSaved, compact }: { room: Room; onSaved?: () => void; compact?: boolean }) {
  const { ds, me, toast } = useApp();
  const [picked, setPicked] = useState<string[]>(['weekday-evenings']);
  const [busy, setBusy] = useState(false);
  const save = async () => {
    setBusy(true);
    try {
      const rules = TIME_PRESETS.filter((p) => picked.includes(p.id)).flatMap((p) => p.rules);
      await ds.setRules(room.id, rules);
      toast('Saved. Only overlaps are shown to others — never your schedule.');
      onSaved?.();
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not save.');
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="stack" style={{ gap: 10 }}>
      {compact ? null : <span className="muted small">In your time zone ({me.timeZone.replace(/_/g, ' ')}). You can fine-tune days and times in Settings.</span>}
      <div className="preset-row" role="group" aria-label="Usual free times">
        {TIME_PRESETS.map((p) => (
          <button key={p.id} className="chip preset" aria-pressed={picked.includes(p.id)} onClick={() => setPicked((x) => (x.includes(p.id) ? x.filter((y) => y !== p.id) : [...x, p.id]))}>
            {picked.includes(p.id) ? <Icon name="check" /> : null}{p.label}
          </button>
        ))}
      </div>
      <button className="btn btn-primary" style={{ alignSelf: 'start' }} disabled={busy || !picked.length} onClick={() => void save()}>{busy ? 'Saving…' : 'Save my usual times'}</button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// First run
// ---------------------------------------------------------------------------

type Step = 'about' | 'room' | 'invite' | 'avatar' | 'times' | 'done';
const STEPS: { id: Step; label: string }[] = [
  { id: 'about', label: 'You' },
  { id: 'room', label: 'Your room' },
  { id: 'invite', label: 'Invite' },
  { id: 'avatar', label: 'Avatar' },
  { id: 'times', label: 'Usual times' },
];

const SETUP_KEY = 'samesky.setup';
/** Setup stays on screen across refreshes until the person finishes or skips it (this tab only). */
export function setupOpen(): boolean {
  try { return sessionStorage.getItem(SETUP_KEY) === 'open'; } catch { return false; }
}
function setSetupOpen(open: boolean) {
  try { if (open) sessionStorage.setItem(SETUP_KEY, 'open'); else sessionStorage.removeItem(SETUP_KEY); } catch { /* ignore */ }
}

export function GetStarted() {
  const { ds, me, rooms, refresh, toast, setRoomId, navigate } = useApp();
  // resuming (a reload, or back from avatar setup): the room already exists, so carry on from inviting
  const existing = rooms[0] ?? null;
  const [step, setStep] = useState<Step>(existing ? 'invite' : 'about');
  useEffect(() => { setSetupOpen(true); }, []);
  const [name, setName] = useState(me.displayName);
  const detected = detectZone();
  const [zone, setZone] = useState(me.timeZone || detected);
  const [kind, setKind] = useState<'direct' | 'circle'>('direct');
  const [roomName, setRoomName] = useState('');
  const [room, setRoom] = useState<Room | null>(existing);
  const [joinCode, setJoinCode] = useState('');
  const [joining, setJoining] = useState(false);
  const [busy, setBusy] = useState(false);
  const index = STEPS.findIndex((s) => s.id === step);

  const saveAbout = async () => {
    setBusy(true);
    try {
      if (name.trim() !== me.displayName || zone !== me.timeZone) await ds.updateMe({ display_name: name.trim() || me.displayName, time_zone: zone });
      setStep('room');
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not save.');
    } finally {
      setBusy(false);
    }
  };
  const createRoom = async () => {
    setBusy(true);
    try {
      const r = await ds.createRoom(kind, roomName.trim() || (kind === 'direct' ? `${name.trim() || me.displayName}’s room` : 'Our circle'));
      setRoom(r);
      setRoomId(r.id);
      setStep('invite');
      await refresh(); // the rest of the app knows the room now; setup stays open until finished
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not create the room.');
    } finally {
      setBusy(false);
    }
  };
  const join = async () => {
    const raw = joinCode.trim();
    if (!raw) return;
    // A short code (8 letters/digits, dashes optional) joins directly; anything else is treated as a link.
    const compact = raw.toUpperCase().replace(/[\s_-]/g, '');
    if (/^[A-Z0-9]{8}$/.test(compact) && !raw.includes('/')) {
      setBusy(true);
      try {
        const r = await ds.redeemCode(compact);
        setSetupOpen(false);
        await refresh();
        setRoomId(r.id);
        navigate('day');
      } catch (e) {
        toast(e instanceof Error ? e.message : 'That code didn’t work.');
      } finally {
        setBusy(false);
      }
      return;
    }
    const token = raw.split('/join/').pop()?.replace(/^#?\/?/, '');
    if (!token) return;
    setSetupOpen(false);
    location.hash = `/join/${token}`;
  };
  const finish = async (to: 'day' | 'moments') => {
    setSetupOpen(false);
    await refresh();
    navigate(to);
  };

  return (
    <div className="setup">
      <div className="setup-head">
        <div className="row" style={{ justifyContent: 'space-between', alignItems: 'baseline', flexWrap: 'wrap' }}>
          <h1 className="page-title">{step === 'done' ? 'You’re all set' : `Welcome, ${name.trim() || me.displayName}`}</h1>
          {room && step !== 'done' ? <button className="btn btn-quiet" onClick={() => void finish('day')}>Finish later</button> : null}
        </div>
        {step !== 'done' ? (
          <ol className="setup-steps" aria-label="Setup steps">
            {STEPS.map((s, i) => (
              <li key={s.id} aria-current={s.id === step ? 'step' : undefined} className={i < index ? 'done' : undefined}>
                <span className="step-dot" aria-hidden="true">{i < index ? <Icon name="check" /> : i + 1}</span>
                <span className="step-label">{s.label}</span>
              </li>
            ))}
          </ol>
        ) : null}
      </div>

      <section className="card stack setup-card" aria-live="polite">
        {step === 'about' ? (
          <>
            <ul className="setup-promises">
              <li><Icon name="image" /><span><strong>Share a few chosen moments</strong> — photos, clips, voice notes — with exactly the people you pick.</span></li>
              <li><Icon name="calendar" /><span><strong>Find time that fits both time zones</strong> without anyone seeing your whole schedule.</span></li>
              <li><Icon name="headset" /><span><strong>Sit together in VR</strong> at one table, or just open the room on your phone.</span></li>
            </ul>
            <label className="field"><span>What should people call you?</span><input className="input" value={name} maxLength={60} autoComplete="nickname" onChange={(e) => setName(e.target.value)} /></label>
            <label className="field"><span>Your time zone</span>
              <select className="input" value={zone} onChange={(e) => setZone(e.target.value)}>
                {[...new Set([zone, detected, ...COMMON_ZONES])].map((z) => <option key={z} value={z}>{z.replace(/_/g, ' ')}</option>)}
              </select>
              <small className="muted">Used to show your local times and find overlaps. Change it whenever you travel — no GPS.</small>
            </label>
            <div className="row wrap setup-actions">
              <button className="btn btn-primary btn-lg" disabled={busy} onClick={() => void saveAbout()}>Continue</button>
            </div>
          </>
        ) : null}

        {step === 'room' ? (
          <>
            <h2 className="section-title" style={{ margin: 0 }}>Who is this room for?</h2>
            <div className="option-cards setup-options">
              <button className="option-card" aria-pressed={kind === 'direct'} onClick={() => setKind('direct')}>
                <Icon name="user" /><span><strong>Just the two of us</strong><small>A private room for you and one other person.</small></span>
              </button>
              <button className="option-card" aria-pressed={kind === 'circle'} onClick={() => setKind('circle')}>
                <Icon name="users" /><span><strong>A small circle</strong><small>Family or close friends, up to six people.</small></span>
              </button>
            </div>
            <label className="field"><span>Room name (optional)</span>
              <input className="input" value={roomName} maxLength={80} placeholder={kind === 'direct' ? 'e.g. Nora + Sam' : 'e.g. The Okafor family'} onChange={(e) => setRoomName(e.target.value)} /></label>
            <div className="row wrap setup-actions">
              <button className="btn btn-primary btn-lg" disabled={busy} onClick={() => void createRoom()}>{busy ? 'Creating…' : 'Create the room'}</button>
              <button className="btn btn-quiet" onClick={() => setJoining((j) => !j)} aria-expanded={joining}>I have an invite link or code</button>
            </div>
            {joining ? (
              <div className="row wrap" style={{ gap: 8 }}>
                <input className="input grow" aria-label="Invite link or code" placeholder="Paste the link, or type the code (e.g. K7PQ-4XMR)" value={joinCode} onChange={(e) => setJoinCode(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') void join(); }} />
                <button className="btn btn-outline" disabled={!joinCode.trim() || busy} onClick={() => void join()}>Join</button>
              </div>
            ) : null}
            <button className="btn btn-quiet setup-back" onClick={() => setStep('about')}>Back</button>
          </>
        ) : null}

        {step === 'invite' && room ? (
          <>
            <h2 className="section-title" style={{ margin: 0 }}>{room.name} is ready</h2>
            <InviteCard room={room} />
            <p className="muted small" style={{ margin: 0 }}>You can do this later from Our day or Settings. They’ll see only what’s shared after they join — never your earlier moments.</p>
            <div className="row wrap setup-actions">
              <button className="btn btn-primary btn-lg" onClick={() => setStep('avatar')}>Next</button>
            </div>
          </>
        ) : null}

        {step === 'avatar' ? (
          <>
            <h2 className="section-title" style={{ margin: 0 }}>Let people recognise you</h2>
            <div className="row setup-avatar">
              <Avatar avatar={me.avatar} size={96} name={me.displayName} demoBadge />
              <p className="muted" style={{ margin: 0 }}>You start with a labelled demo avatar. You can create a 3D avatar from your own photo — only you see it until you approve it, and you choose who can see it.</p>
            </div>
            <div className="row wrap setup-actions">
              <a className="btn btn-primary btn-lg" href="#/settings?avatar=create" onClick={() => void refresh()}><Icon name="camera" />Create from my photos</a>
              <button className="btn btn-outline" onClick={() => setStep('times')}>Keep the demo avatar for now</button>
            </div>
          </>
        ) : null}

        {step === 'times' && room ? (
          <>
            <h2 className="section-title" style={{ margin: 0 }}>When are you usually free?</h2>
            <p className="muted" style={{ margin: 0 }}>SameSky looks for times that fit everyone’s day and suggests them. Others only ever see the overlap.</p>
            <QuickTimes room={room} onSaved={() => setStep('done')} />
            <button className="btn btn-quiet setup-back" onClick={() => setStep('done')}>Skip for now</button>
          </>
        ) : null}

        {step === 'done' ? (
          <>
            <p style={{ margin: 0, fontSize: '1.1rem' }}>Your room is ready. When someone joins, what you choose to share appears on <strong>Our day</strong> for them — and you can sit at the table together in VR.</p>
            <div className="row wrap setup-actions">
              <button className="btn btn-primary btn-lg" onClick={() => void finish('moments')}><Icon name="upload" />Share a first moment</button>
              <button className="btn btn-outline" onClick={() => void finish('day')}>Go to Our day</button>
            </div>
          </>
        ) : null}
      </section>
    </div>
  );
}

/** For views that need a room: a clear way back to setup instead of a dead end. */
export function NoRoom() {
  const { navigate } = useApp();
  return (
    <section className="card stack" style={{ maxWidth: 560 }}>
      <h1 className="section-title" style={{ margin: 0 }}>No room yet</h1>
      <p className="muted" style={{ margin: 0 }}>Create a private room for two, or a small circle, then invite someone. It takes a minute.</p>
      <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => navigate('day')}>Set up my room</button>
    </section>
  );
}
