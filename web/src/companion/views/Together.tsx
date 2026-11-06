import { useCallback, useEffect, useState } from 'react';
import type { Award, Moment, OpportunityResult } from '../../shared/contracts';
import { cityOf, formatTime } from '../../shared/tz';
import { useApp } from '../App';
import { Avatar, Checkbox, Icon, KeepsakeRender, Notice } from '../ui';

function longDate(instant: number, zone: string) {
  const weekday = new Intl.DateTimeFormat('en-GB', { weekday: 'long', timeZone: zone }).format(instant);
  const rest = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'long', timeZone: zone }).format(instant);
  return `${weekday}, ${rest}`;
}

export function Together() {
  const { ds, me, room, toast } = useApp();
  const others = room?.members.filter((m) => m.id !== me.id) ?? [];
  const [selected, setSelected] = useState<string[]>([]);
  const [result, setResult] = useState<OpportunityResult | null>(null);
  const [freeUntil, setFreeUntil] = useState<string | null>(null);
  const [awards, setAwards] = useState<Award[]>([]);
  const [now, setNow] = useState(Date.now());
  const [error, setError] = useState<string | null>(null);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    setSelected(room?.kind === 'direct' ? others.map((o) => o.id) : others.slice(0, 1).map((o) => o.id));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [room?.id]);

  const load = useCallback(async () => {
    if (!room || !selected.length) return;
    try {
      const [res, avail, aw] = await Promise.all([ds.opportunities(room.id, selected), ds.availability(room.id), ds.awards(room.id)]);
      setResult(res);
      setFreeUntil(avail.freeNowUntil);
      setAwards(aw);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not compute opportunities.');
    }
  }, [ds, room, selected]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), 15_000);
    return () => window.clearInterval(t);
  }, []);

  if (!room) return <Notice>Create or join a room first.</Notice>;
  const people = result?.participants ?? [];
  const next = result?.candidates[0];
  const toggleFree = async () => {
    const until = await ds.setFreeNow(room.id, !freeUntil);
    setFreeUntil(until);
    toast(until ? `Marked free until ${formatTime(until, me.timeZone)} in ${room.name}. It expires on its own.` : 'Free now cleared.');
    void load();
  };

  return (
    <>
      <div className="day-header">
        <h1 className="page-title on-backdrop">Find our next moment</h1>
        <span className="chip glass audience-pill"><Icon name="users" />{people.length ? people.map((p) => (p.userId === me.id ? p.displayName : p.displayName)).join(' + ') : room.name}</span>
      </div>
      {room.kind === 'circle' ? (
        <div className="row wrap chip glass" style={{ marginBottom: 14, padding: '8px 16px', borderRadius: 16, whiteSpace: 'normal' }}>
          <strong>Match with</strong>
          {others.map((o) => (
            <label key={o.id} className="row" style={{ gap: 8 }}>
              <Checkbox checked={selected.includes(o.id)} label={`Include ${o.displayName}`} onChange={(v) => setSelected((s) => (v ? [...s, o.id] : s.filter((x) => x !== o.id)))} />
              {o.displayName}
            </label>
          ))}
          <span className="small" style={{ opacity: 0.8 }}>Only the people you select are matched and told.</span>
        </div>
      ) : null}
      {error ? <Notice kind="error">{error}</Notice> : null}
      <section className="card" aria-label="Next shared moment">
        <div className="clock-grid">
          {people.map((p) => {
            const member = room.members.find((m) => m.id === p.userId);
            return (
              <div key={p.userId} className="clock-card">
                <Avatar def={member?.avatar?.definition} size={118} name={p.displayName} />
                <div className="stack" style={{ gap: 2 }}>
                  <span className="name">{p.displayName}{p.userId === me.id ? <span className="muted small" style={{ fontWeight: 500 }}> (you)</span> : null}</span>
                  <span className="row muted" style={{ gap: 6, fontSize: '1.08rem' }}><Icon name="pin" />{member?.city ?? cityOf(p.timeZone)}</span>
                  <span className="time" aria-label={`Local time ${formatTime(now, p.timeZone)}`}>{formatTime(now, p.timeZone)}</span>
                  <span className="muted" style={{ fontSize: '1.08rem' }}>{longDate(now, p.timeZone)}</span>
                </div>
              </div>
            );
          })}
        </div>
        <div className="overlap-strip">
          <Icon name="calendar" />
          <div className="grow stack" style={{ gap: 2 }}>
            {next ? (
              <>
                <strong style={{ fontSize: '1.2rem' }}>Your chosen schedules overlap</strong>
                <span>{next.local.map((l) => `${l.time}–${l.endTime} ${cityOf(l.zone)}`).join(' · ')} · {next.minutes} min{next.local[0] ? ` · ${next.local[0].weekday}` : ''}</span>
              </>
            ) : <strong>No shared window in the next 7 days. Adjust your availability in Settings.</strong>}
          </div>
          <span className="divider-v" />
          {result?.allMarkedFreeNow ? <span className="chip green"><Icon name="checkCircle" />{people.length === 2 ? 'Both marked free now' : 'All marked free now'}</span> : null}
          <span className="row muted" style={{ gap: 8 }}><Icon name="moon" />Quiet hours respected</span>
          {result?.simulated ? <span className="chip label" title="Schedules are fictional and nobody is notified.">Sample schedules</span> : null}
        </div>
        {result?.statusMessage && !dismissed ? <div style={{ marginTop: 12 }}><Notice>{result.statusMessage}{result.simulated ? ' Other people’s Free now is simulated in the sample.' : ''}</Notice></div> : null}
        <div className="connect-row">
          <a className="btn btn-primary btn-lg" href={`/room.html?room=${room.id}`} style={{ minWidth: 250 }}><Icon name="users" />Open our room</a>
          <button className="btn btn-quiet btn-lg" onClick={() => { setDismissed(true); toast('Okay — no suggestion for now.'); }}>Not now</button>
          <span className="divider-v" />
          <button className="btn btn-outline" aria-pressed={!!freeUntil} onClick={() => void toggleFree()}>
            <Icon name="sun" />{freeUntil ? `I'm free until ${formatTime(freeUntil, me.timeZone)}` : "I'm free now · 30 min"}
          </button>
          <WatchTogether />
          {next && ds.mode === 'live' ? (
            <button className="btn btn-outline" onClick={() => void ds.suggest(room.id, selected, next.startUtc).then(() => toast('Suggested to the selected people only.')).catch((e: Error) => toast(e.message))}>
              <Icon name="send" />Suggest this time
            </button>
          ) : null}
          {result && result.candidates.length > 1 ? (
            <details>
              <summary className="btn btn-quiet">More openings</summary>
              <div className="stack card-inner" style={{ marginTop: 8, gap: 6 }}>
                {result.candidates.slice(1).map((c) => (
                  <span key={c.startUtc} className="small">{c.local.map((l) => `${l.weekday.slice(0, 3)} ${l.time}–${l.endTime} ${cityOf(l.zone)}`).join(' · ')} · {c.minutes} min</span>
                ))}
              </div>
            </details>
          ) : null}
        </div>
      </section>

      <h2 className="section-title on-backdrop" style={{ margin: '22px 0 12px' }}>Our shared keepsakes</h2>
      <div className="keepsake-row">
        {awards.map((a) => (
          <a key={a.milestone} className="keepsake" href="#/keepsakes">
            <div className="art"><KeepsakeRender kind={a.keepsake} progress={a.progress} target={a.target} earned={a.earned} title={a.title} /></div>
            <div className="info">
              <strong>{a.title}</strong>
              {a.earned ? <span className="chip green label" style={{ alignSelf: 'start' }}><Icon name="checkCircle" />Earned</span> : (
                <span className="row"><span className="meter"><i style={{ width: `${(a.progress / a.target) * 100}%` }} /></span><span className="small muted">{a.progress} / {a.target} shared days</span></span>
              )}
            </div>
          </a>
        ))}
      </div>
    </>
  );
}

/** Compact entry point to a synchronized viewing session (live) or the labelled simulation (sample). */
function WatchTogether() {
  const { ds, me, room, toast } = useApp();
  const [moments, setMoments] = useState<Moment[]>([]);
  const [momentId, setMomentId] = useState('');
  const [sessions, setSessions] = useState<{ id: string; momentId: string; participantIds: string[]; hostId: string }[]>([]);
  useEffect(() => {
    if (!room || ds.mode === 'sample') return;
    void ds.moments(room.id).then((list) => {
      const playable = list.filter((m) => m.sharingState === 'shared' && (m.kind === 'clip' || m.kind === 'photo'));
      setMoments(playable);
      setMomentId(playable.find((m) => m.kind === 'clip')?.id ?? playable[0]?.id ?? '');
    });
    void ds.sessions(room.id).then(setSessions).catch(() => setSessions([]));
  }, [ds, room]);
  if (!room) return null;
  if (ds.mode === 'sample') {
    return <a className="btn btn-outline" href={`/room.html?mode=sample&room=${room.id}&watch=1`} title="Maya's avatar is a labelled simulation in the sample"><Icon name="play" />Watch together (simulated)</a>;
  }
  const partner = room.members.find((m) => m.id !== me.id)?.id ?? '';
  const start = async () => {
    try {
      const s = await ds.createSession(room.id, momentId, partner);
      location.href = `/room.html?room=${room.id}&session=${s.id}`;
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not start a session.');
    }
  };
  return (
    <details>
      <summary className="btn btn-outline"><Icon name="play" />Watch together</summary>
      <div className="card-inner stack" style={{ marginTop: 8, gap: 8, minWidth: 280 }}>
        {sessions.map((s) => (
          <a key={s.id} className="btn btn-primary" href={`/room.html?room=${room.id}&session=${s.id}`}>Join {room.members.find((m) => m.id === s.hostId)?.displayName ?? 'the'}'s session</a>
        ))}
        <select className="input" value={momentId} onChange={(e) => setMomentId(e.target.value)} aria-label="Moment to watch">
          {moments.map((m) => <option key={m.id} value={m.id}>{m.ownerName}: {m.caption || m.kind}</option>)}
        </select>
        <button className="btn btn-primary" disabled={!momentId || !partner} onClick={() => void start()}>Start watching</button>
        <span className="small muted">Only someone who can already see the moment can join.</span>
      </div>
    </details>
  );
}
