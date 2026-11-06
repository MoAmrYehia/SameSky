import { useCallback, useEffect, useState } from 'react';
import type { Award, Moment, OpportunityResult } from '../../shared/contracts';
import { cityOf, formatDate, formatTime } from '../../shared/tz';
import { useApp } from '../App';
import { Avatar, Checkbox, Icon, KeepsakeArt, Notice } from '../ui';

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
        <h1 className="page-title">Find our next moment</h1>
        <span className="chip"><Icon name="users" />{result?.participantLabel ?? room.name}</span>
      </div>
      {room.kind === 'circle' ? (
        <div className="card row wrap" style={{ marginBottom: 16 }}>
          <strong>Who's this for?</strong>
          {others.map((o) => (
            <label key={o.id} className="row" style={{ gap: 8 }}>
              <Checkbox checked={selected.includes(o.id)} label={`Include ${o.displayName}`} onChange={(v) => setSelected((s) => (v ? [...s, o.id] : s.filter((x) => x !== o.id)))} />
              {o.displayName}
            </label>
          ))}
          <span className="muted small">Only the people you select are matched — and only they are told.</span>
        </div>
      ) : null}
      {error ? <Notice kind="error">{error}</Notice> : null}
      <section className="card stack" style={{ gap: 0 }}>
        <div className="clock-grid">
          {people.map((p) => {
            const member = room.members.find((m) => m.id === p.userId);
            return (
              <div key={p.userId} className="clock-card">
                <Avatar def={member?.avatar?.definition} size={110} name={p.displayName} />
                <div className="stack" style={{ gap: 4 }}>
                  <strong style={{ fontSize: '1.5rem' }}>{p.userId === me.id ? `${p.displayName} (you)` : p.displayName}</strong>
                  <span className="row muted" style={{ gap: 6 }}><Icon name="pin" />{member?.city ?? cityOf(p.timeZone)}</span>
                  <span className="time" aria-label={`Local time ${formatTime(now, p.timeZone)}`}>{formatTime(now, p.timeZone)}</span>
                  <span className="muted">{formatDate(now, p.timeZone)}</span>
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
                <strong style={{ fontSize: '1.25rem' }}>{next.message}</strong>
                <span>{next.local.map((l) => `${l.time}–${l.endTime} ${cityOf(l.zone)}`).join(' · ')}{next.local[0] ? ` · ${next.local[0].weekday}` : ''}</span>
              </>
            ) : <strong>No shared window in the next 7 days. Adjust your availability in Settings.</strong>}
          </div>
          {result?.allMarkedFreeNow ? <span className="chip green"><Icon name="checkCircle" />{people.length === 2 ? 'Both marked free now' : 'All marked free now'}</span> : null}
          <span className="row muted" style={{ gap: 8 }}><Icon name="moon" />Quiet hours respected</span>
        </div>
        {result?.statusMessage && !dismissed ? <Notice>{result.statusMessage}{result.simulated ? ' (Other people’s Free now signals are simulated in the sample.)' : ''}</Notice> : null}
        <div className="row wrap" style={{ justifyContent: 'center', marginTop: 18, gap: 18 }}>
          <a className="btn btn-primary btn-lg" href={`/room.html?room=${room.id}`}><Icon name="users" />Open our room</a>
          <button className="btn btn-quiet btn-lg" onClick={() => { setDismissed(true); toast('Okay — no suggestion for now.'); }}>Not now</button>
          <button className="btn btn-outline" aria-pressed={!!freeUntil} onClick={() => void toggleFree()}>
            <Icon name="sun" />{freeUntil ? `I'm free until ${formatTime(freeUntil, me.timeZone)}` : "I'm free now (30 min)"}
          </button>
        </div>
        {result?.simulated ? <p className="small muted" style={{ textAlign: 'center', margin: '12px 0 0' }}>Sample mode: schedules are fictional and nobody is notified.</p> : null}
        {result && result.candidates.length > 1 ? (
          <details style={{ marginTop: 16 }}>
            <summary className="btn btn-quiet">More openings this week</summary>
            <div className="stack" style={{ marginTop: 10 }}>
              {result.candidates.slice(1).map((c) => (
                <div key={c.startUtc} className="card-inner row wrap">
                  <span className="grow">{c.local.map((l) => `${l.weekday.slice(0, 3)} ${l.time}–${l.endTime} ${cityOf(l.zone)}`).join(' · ')}</span>
                  <span className="chip label">{c.minutes} min</span>
                  {ds.mode === 'live' ? <button className="btn btn-outline" onClick={() => void ds.suggest(room.id, selected, c.startUtc).then(() => toast('Suggested to the selected people only.')).catch((e: Error) => toast(e.message))}>Suggest</button> : null}
                </div>
              ))}
            </div>
          </details>
        ) : null}
        {next && ds.mode === 'live' ? (
          <div className="row wrap" style={{ justifyContent: 'center', marginTop: 12 }}>
            <button className="btn btn-outline" onClick={() => void ds.suggest(room.id, selected, next.startUtc).then(() => toast('Suggested. They’ll see it in their inbox (and push, if they enabled it).')).catch((e: Error) => toast(e.message))}>Suggest this time to {result?.participantLabel.replace(' and you', '')}</button>
          </div>
        ) : null}
      </section>

      <WatchTogether />

      <h2 className="section-title" style={{ margin: '28px 0 14px' }}>Our shared keepsakes</h2>
      <div className="keepsake-row">
        {awards.map((a) => (
          <a key={a.milestone} className="keepsake" href="#/keepsakes">
            <div className="art"><KeepsakeArt kind={a.keepsake} progress={a.progress} target={a.target} /></div>
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

function WatchTogether() {
  const { ds, me, room, toast } = useApp();
  const [moments, setMoments] = useState<Moment[]>([]);
  const [momentId, setMomentId] = useState('');
  const [partner, setPartner] = useState('');
  const [sessions, setSessions] = useState<{ id: string; momentId: string; participantIds: string[]; hostId: string }[]>([]);
  useEffect(() => {
    if (!room) return;
    void ds.moments(room.id).then((list) => {
      const playable = list.filter((m) => m.sharingState === 'shared' && (m.kind === 'clip' || m.kind === 'photo'));
      setMoments(playable);
      setMomentId(playable.find((m) => m.kind === 'clip')?.id ?? playable[0]?.id ?? '');
    });
    void ds.sessions(room.id).then(setSessions).catch(() => setSessions([]));
    setPartner(room.members.find((m) => m.id !== me.id)?.id ?? '');
  }, [ds, room, me.id]);
  if (!room) return null;
  const start = async () => {
    try {
      const s = await ds.createSession(room.id, momentId, partner);
      location.href = `/room.html?room=${room.id}&session=${s.id}`;
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not start a session.');
    }
  };
  return (
    <section className="card stack" style={{ marginTop: 24 }}>
      <h2 className="section-title" style={{ margin: 0 }}>Watch a moment together</h2>
      {ds.mode === 'sample' ? (
        <>
          <p className="muted" style={{ margin: 0 }}>In the sample, Maya's avatar is a labelled simulation sitting beside you. Live sync needs two signed-in people.</p>
          <a className="btn btn-primary" style={{ alignSelf: 'start' }} href={`/room.html?mode=sample&room=${room.id}&watch=1`}><Icon name="headset" />Watch in our room (simulated)</a>
        </>
      ) : (
        <>
          <p className="muted" style={{ margin: 0 }}>Both of you sit at the table and see the same playback. Only someone who can already see the moment can join.</p>
          <div className="row wrap">
            <select className="input" style={{ width: 'auto' }} value={momentId} onChange={(e) => setMomentId(e.target.value)} aria-label="Moment to watch">
              {moments.map((m) => <option key={m.id} value={m.id}>{m.ownerName}: {m.caption || m.kind} ({m.captureLocal.time})</option>)}
            </select>
            <select className="input" style={{ width: 'auto' }} value={partner} onChange={(e) => setPartner(e.target.value)} aria-label="Watch with">
              {room.members.filter((m) => m.id !== me.id).map((m) => <option key={m.id} value={m.id}>with {m.displayName}</option>)}
            </select>
            <button className="btn btn-primary" disabled={!momentId || !partner} onClick={() => void start()}><Icon name="play" />Start watching</button>
          </div>
          {sessions.length ? (
            <div className="stack">
              {sessions.map((s) => (
                <a key={s.id} className="btn btn-outline" style={{ alignSelf: 'start' }} href={`/room.html?room=${room.id}&session=${s.id}`}>
                  Join {room.members.find((m) => m.id === s.hostId)?.displayName ?? 'the'}'s viewing session
                </a>
              ))}
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}
