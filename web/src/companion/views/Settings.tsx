import { useEffect, useState } from 'react';
import { AVATAR_OPTIONS, DEFAULT_AVATAR } from '../../shared/avatar';
import type { AvatarDefinition, WeeklyRule } from '../../shared/contracts';
import { SampleSource } from '../../shared/data';
import { COMMON_ZONES, detectZone } from '../../shared/tz';
import { useApp } from '../App';
import { disablePush, enablePush, PUSH_COPY, pushStatus, type PushStatus } from '../push';
import { Avatar, Icon, Notice, Toggle } from '../ui';

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export function Settings() {
  return (
    <>
      <h1 className="page-title" style={{ marginBottom: 20 }}>Settings</h1>
      <div className="settings-grid">
        <Profile />
        <AvatarEditor />
        <Availability />
        <Notifications />
        <Rooms />
        <Session />
      </div>
    </>
  );
}

function Profile() {
  const { ds, me, refresh, toast } = useApp();
  const [name, setName] = useState(me.displayName);
  const [zone, setZone] = useState(me.timeZone);
  const [link, setLink] = useState(me.callLink ?? '');
  const detected = detectZone();
  const save = async () => {
    try {
      await ds.updateMe({ display_name: name, time_zone: zone, call_link: link });
      await refresh();
      toast('Saved. Past moments keep their original times.');
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not save.');
    }
  };
  return (
    <section className="card stack">
      <h2 className="section-title" style={{ margin: 0 }}>You</h2>
      <label className="field"><span>Display name</span><input className="input" value={name} maxLength={60} onChange={(e) => setName(e.target.value)} /></label>
      <label className="field"><span>Current time zone</span>
        <select className="input" value={zone} onChange={(e) => setZone(e.target.value)}>
          {[...new Set([zone, detected, ...COMMON_ZONES])].map((z) => <option key={z} value={z}>{z.replace(/_/g, ' ')}</option>)}
        </select>
        <small className="muted">Travelling? Change it here — no GPS or location sharing. {detected !== zone ? `This device says ${detected.replace(/_/g, ' ')}.` : ''}</small>
      </label>
      {detected !== zone ? <button className="btn btn-quiet" style={{ alignSelf: 'start' }} onClick={() => setZone(detected)}>Use {detected.replace(/_/g, ' ')}</button> : null}
      <label className="field"><span>Your calling link (optional)</span>
        <input className="input" placeholder="https://… or facetime:…" value={link} onChange={(e) => setLink(e.target.value)} />
        <small className="muted">“Connect” hands off to this link. SameSky never starts a call or turns on a microphone by itself.</small></label>
      <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => void save()}>Save</button>
    </section>
  );
}

function Swatches({ values, value, onChange, label }: { values: readonly string[]; value: string; onChange: (v: string) => void; label: string }) {
  return (
    <div className="swatches" role="group" aria-label={label}>
      {values.map((v) => <button key={v} className="swatch" style={{ background: v }} aria-pressed={value === v} aria-label={`${label} ${v}`} onClick={() => onChange(v)} />)}
    </div>
  );
}

function Segments({ values, value, onChange, label }: { values: readonly string[]; value: string; onChange: (v: string) => void; label: string }) {
  return (
    <div className="seg-control" role="group" aria-label={label}>
      {values.map((v) => <button key={v} aria-pressed={value === v} onClick={() => onChange(v)}>{v}</button>)}
    </div>
  );
}

function AvatarEditor() {
  const { ds, me, refresh, toast } = useApp();
  const [def, setDef] = useState<AvatarDefinition>(me.avatar?.definition ?? DEFAULT_AVATAR);
  const set = (k: keyof AvatarDefinition) => (v: string) => setDef((d) => ({ ...d, [k]: v }));
  const save = async () => {
    await ds.saveAvatar(def, false);
    await refresh();
    toast('Avatar saved. It appears beside people at the table.');
  };
  return (
    <section className="card stack">
      <h2 className="section-title" style={{ margin: 0 }}>Your avatar</h2>
      <div className="row"><Avatar def={def} size={150} name={me.displayName} /><p className="muted small">Rendered live from your 3D character — the same one people see across the table. A stylized preset: no photo or face scan needed, and only people in your rooms see it.</p></div>
      <span className="small"><strong>Face</strong></span><Segments values={AVATAR_OPTIONS.face} value={def.face ?? 'soft'} onChange={set('face')} label="Face shape" />
      <span className="small"><strong>Skin tone</strong></span><Swatches values={AVATAR_OPTIONS.skin} value={def.skin} onChange={set('skin')} label="Skin tone" />
      <span className="small"><strong>Hair</strong></span><Segments values={AVATAR_OPTIONS.hair} value={def.hair} onChange={set('hair')} label="Hairstyle" />
      <Swatches values={AVATAR_OPTIONS.hairColor} value={def.hairColor} onChange={set('hairColor')} label="Hair colour" />
      <span className="small"><strong>Clothing</strong></span><Swatches values={AVATAR_OPTIONS.top} value={def.top} onChange={set('top')} label="Top colour" />
      <span className="small"><strong>Accessory</strong></span><Segments values={AVATAR_OPTIONS.accessory} value={def.accessory} onChange={set('accessory')} label="Accessory" />
      <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => void save()}>Save avatar</button>
    </section>
  );
}

function Availability() {
  const { ds, me, room, toast, refresh } = useApp();
  const [rules, setRules] = useState<WeeklyRule[]>([]);
  const [quietStart, setQuietStart] = useState(me.quietStart ?? '');
  const [quietEnd, setQuietEnd] = useState(me.quietEnd ?? '');
  const [hints, setHints] = useState(me.shareAvailabilityHints);
  useEffect(() => {
    if (room) void ds.availability(room.id).then((a) => setRules(a.rules));
  }, [ds, room]);
  if (!room) return null;
  const update = (i: number, patch: Partial<WeeklyRule>) => setRules((list) => list.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const save = async () => {
    try {
      await ds.setRules(room.id, rules);
      await ds.updateMe({ quiet_start: quietStart, quiet_end: quietEnd, share_availability_hints: hints });
      await refresh();
      toast(`Availability saved for ${room.name}.`);
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not save.');
    }
  };
  return (
    <section className="card stack">
      <h2 className="section-title" style={{ margin: 0 }}>When you're usually free</h2>
      <p className="muted small" style={{ margin: 0 }}>For <strong>{room.name}</strong>, in your local time ({me.timeZone.replace(/_/g, ' ')}). An end before the start means overnight. Only overlaps are shown to others — never your schedule.</p>
      {rules.map((r, i) => (
        <div key={i} className="rule-row">
          <select className="input" value={r.weekday} onChange={(e) => update(i, { weekday: Number(e.target.value) })} aria-label="Day">
            {DAYS.map((d, n) => <option key={d} value={n}>{d.slice(0, 3)}</option>)}
          </select>
          <input className="input" type="time" value={r.start} onChange={(e) => update(i, { start: e.target.value })} aria-label="From" />
          <input className="input" type="time" value={r.end} onChange={(e) => update(i, { end: e.target.value })} aria-label="Until" />
          <button className="icon-btn" aria-label="Remove window" onClick={() => setRules((l) => l.filter((_, j) => j !== i))}><Icon name="trash" /></button>
        </div>
      ))}
      <div className="row wrap">
        <button className="btn btn-outline" onClick={() => setRules((l) => [...l, { weekday: 0, start: '18:00', end: '19:00' }])}>Add a window</button>
        <button className="btn btn-quiet" onClick={() => setRules([0, 1, 2, 3, 4].map((weekday) => ({ weekday, start: '18:00', end: '19:00' })))}>Weekday evenings</button>
      </div>
      <div className="row wrap">
        <label className="field"><span>Quiet hours from</span><input className="input" type="time" value={quietStart} onChange={(e) => setQuietStart(e.target.value)} /></label>
        <label className="field"><span>until</span><input className="input" type="time" value={quietEnd} onChange={(e) => setQuietEnd(e.target.value)} /></label>
      </div>
      <div className="row" style={{ justifyContent: 'space-between' }}><span>Let room members see when we overlap</span><Toggle checked={hints} onChange={setHints} label="Share availability hints" /></div>
      <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => void save()}>Save availability</button>
    </section>
  );
}

function Notifications() {
  const { ds, me, refresh, toast } = useApp();
  const [status, setStatus] = useState<PushStatus | null>(null);
  const prefs = me.notificationPrefs ?? {};
  useEffect(() => { void pushStatus().then(setStatus); }, []);
  const setPref = async (key: string, value: boolean) => {
    await ds.updateMe({ notification_prefs: { [key]: value } });
    await refresh();
  };
  return (
    <section className="card stack">
      <h2 className="section-title" style={{ margin: 0 }}>Notifications</h2>
      <p className="muted small" style={{ margin: 0 }}>Your inbox always collects recaps, replies and call opportunities.</p>
      {ds.mode === 'sample' ? <Notice>Sample mode never sends real notifications. Create an account to try push.</Notice> : (
        <>
          <Notice kind={status === 'denied' || status === 'unsupported' ? 'warn' : 'info'}>{status ? PUSH_COPY[status] : 'Checking…'}</Notice>
          {status === 'enabled'
            ? <button className="btn btn-outline" style={{ alignSelf: 'start' }} onClick={() => void disablePush().then(() => pushStatus().then(setStatus))}>Turn off on this browser</button>
            : status === 'default' || status === 'server-disabled'
              ? <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => void enablePush().then((s) => { setStatus(s); toast(PUSH_COPY[s]); }).catch((e: Error) => toast(e.message))}><Icon name="bell" />Enable notifications</button>
              : null}
        </>
      )}
      <div className="row" style={{ justifyContent: 'space-between' }}><span>Call suggestions (max 2 a day)</span><Toggle checked={prefs.call_suggestions !== false} onChange={(v) => void setPref('call_suggestions', v)} label="Call suggestions" /></div>
      <div className="row" style={{ justifyContent: 'space-between' }}><span>Keepsake celebrations</span><Toggle checked={prefs.celebrations !== false} onChange={(v) => void setPref('celebrations', v)} label="Celebrations" /></div>
    </section>
  );
}

function Rooms() {
  const { ds, me, rooms, refresh, toast, setRoomId } = useApp();
  const [name, setName] = useState('');
  const [kind, setKind] = useState<'direct' | 'circle'>('direct');
  const [invite, setInvite] = useState<{ roomId: string; url: string } | null>(null);
  const [code, setCode] = useState('');
  const create = async () => {
    try {
      const r = await ds.createRoom(kind, name || (kind === 'direct' ? 'Our room' : 'Family circle'));
      await refresh();
      setRoomId(r.id);
      toast('Room created. Invite someone next.');
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not create the room.');
    }
  };
  const makeInvite = async (roomId: string) => {
    try {
      const inv = await ds.invite(roomId);
      setInvite({ roomId, url: `${location.origin}/#/join/${inv.token}` });
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not create an invite.');
    }
  };
  const join = () => {
    const token = code.trim().split('/join/').pop();
    if (token) location.hash = `/join/${token}`;
  };
  return (
    <section className="card stack">
      <h2 className="section-title" style={{ margin: 0 }}>Rooms</h2>
      {rooms.map((r) => (
        <div key={r.id} className="card-inner stack" style={{ gap: 8 }}>
          <div className="row"><strong className="grow">{r.name}</strong><span className="chip label">{r.kind === 'direct' ? 'Direct · 2' : `Circle · up to ${r.capacity}`}</span></div>
          <div className="row wrap" style={{ gap: 6 }}>
            {r.members.map((m) => (
              <span key={m.id} className="chip"><Avatar def={m.avatar?.definition} size={24} />{m.displayName}{m.id === me.id ? ' (you)' : ''}
                {r.isOwner && m.id !== me.id && ds.mode === 'live' ? (
                  <button className="btn btn-quiet" style={{ minHeight: 28, padding: '0 6px' }} aria-label={`Remove ${m.displayName}`}
                    onClick={() => window.confirm(`Remove ${m.displayName}? They immediately lose access to everything in ${r.name}.`) && void ds.removeMember(r.id, m.id).then(refresh)}><Icon name="x" /></button>
                ) : null}
              </span>
            ))}
          </div>
          {r.isOwner && r.members.length < r.capacity ? (
            invite?.roomId === r.id ? (
              <div className="stack" style={{ gap: 6 }}>
                <span className="small">Single-use link, valid 72 hours. Send it to one adult you trust:</span>
                <span className="code">{invite.url}</span>
                <div className="row wrap">
                  <button className="btn btn-outline" onClick={() => void navigator.clipboard.writeText(invite.url).then(() => toast('Copied.'))}><Icon name="link" />Copy link</button>
                </div>
              </div>
            ) : <button className="btn btn-outline" style={{ alignSelf: 'start' }} onClick={() => void makeInvite(r.id)}><Icon name="link" />Invite someone</button>
          ) : null}
          {!r.isOwner && ds.mode === 'live' ? <button className="btn btn-danger" style={{ alignSelf: 'start' }} onClick={() => window.confirm(`Leave ${r.name}?`) && void ds.removeMember(r.id, 'me').then(refresh)}>Leave room</button> : null}
        </div>
      ))}
      <div className="stack" style={{ gap: 8 }}>
        <strong>New room</strong>
        <div className="seg-control">
          <button aria-pressed={kind === 'direct'} onClick={() => setKind('direct')}>Direct (2 people)</button>
          <button aria-pressed={kind === 'circle'} onClick={() => setKind('circle')}>Private circle (up to 6)</button>
        </div>
        <div className="row"><input className="input" placeholder={kind === 'direct' ? 'Our room' : 'Family circle'} value={name} onChange={(e) => setName(e.target.value)} aria-label="Room name" />
          <button className="btn btn-primary" onClick={() => void create()}>Create</button></div>
      </div>
      <div className="stack" style={{ gap: 8 }}>
        <strong>Join with an invitation</strong>
        <div className="row"><input className="input" placeholder="Paste the invite link" value={code} onChange={(e) => setCode(e.target.value)} aria-label="Invite link" /><button className="btn btn-outline" onClick={join}>Join</button></div>
      </div>
    </section>
  );
}

function Session() {
  const { ds, signOut, toast } = useApp();
  const [theme, setTheme] = useState(() => document.documentElement.dataset.theme ?? 'system');
  useEffect(() => {
    if (theme === 'system') delete document.documentElement.dataset.theme;
    else document.documentElement.dataset.theme = theme;
    try { localStorage.setItem('samesky.theme', theme); } catch { /* ignore */ }
  }, [theme]);
  return (
    <section className="card stack">
      <h2 className="section-title" style={{ margin: 0 }}>This device</h2>
      <div className="seg-control" role="group" aria-label="Theme">
        {['system', 'light', 'dark'].map((t) => <button key={t} aria-pressed={theme === t} onClick={() => setTheme(t)}>{t}</button>)}
      </div>
      {ds instanceof SampleSource ? (
        <>
          <p className="muted small" style={{ margin: 0 }}>Sample edits (replies, uploads, placements) live only in this browser.</p>
          <button className="btn btn-outline" style={{ alignSelf: 'start' }} onClick={() => { ds.reset(); toast('Sample reset to the original day.'); }}><Icon name="refresh" />Reset the sample</button>
          <button className="btn btn-quiet" style={{ alignSelf: 'start' }} onClick={signOut}>Leave the sample</button>
        </>
      ) : <button className="btn btn-outline" style={{ alignSelf: 'start' }} onClick={signOut}>Sign out</button>}
    </section>
  );
}
