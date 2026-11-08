import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { cueAt, loadCaptions, type Cue } from '../../shared/captions';
import type { AvatarView, Moment, Recap } from '../../shared/contracts';
import { SAMPLE_DAY } from '../../shared/sample';
import { wallParts } from '../../shared/tz';
import { useApp } from '../App';
import { Conversation, MomentDetail } from '../MomentDetail';
import { StoryPlayer } from '../StoryPlayer';
import { Avatar, Icon, kindLabel, localWhen, Notice, SourceIcon, Waveform } from '../ui';
import { InviteCard, NoRoom } from './Setup';

function todayIn(zone: string): string {
  const p = wallParts(Date.now(), zone);
  return `${p.year}-${String(p.month).padStart(2, '0')}-${String(p.day).padStart(2, '0')}`;
}

function longDate(day: string) {
  const d = new Date(`${day}T12:00:00Z`);
  const weekday = new Intl.DateTimeFormat('en-GB', { weekday: 'long', timeZone: 'UTC' }).format(d);
  const rest = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'long', timeZone: 'UTC' }).format(d);
  return `${weekday}, ${rest}`;
}

function mmss(seconds: number) {
  return `${Math.floor(seconds / 60)}:${String(Math.round(seconds % 60)).padStart(2, '0')}`;
}

export function OurDay() {
  const { ds, me, room, toast } = useApp();
  const [recap, setRecap] = useState<Recap | null>(null);
  const [pending, setPending] = useState<Recap[]>([]);
  const [moments, setMoments] = useState<Moment[]>([]);
  const [day, setDay] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [playing, setPlaying] = useState(false);
  const [detail, setDetail] = useState<Moment | null>(null);
  const [showSources, setShowSources] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!room) return;
    setLoading(true);
    try {
      const [archive, pend] = await Promise.all([ds.archive(room.id), ds.pendingRecaps(room.id)]);
      const latest = archive[0] ?? null;
      const date = latest?.roomDate ?? (ds.mode === 'sample' ? SAMPLE_DAY : todayIn(room.reportingZone));
      setRecap(latest);
      setPending(pend);
      setDay(date);
      setMoments(await ds.moments(room.id, date));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load this day.');
    } finally {
      setLoading(false);
    }
  }, [ds, room]);

  useEffect(() => {
    void load();
  }, [load]);

  const shared = moments.filter((m) => m.sharingState === 'shared');
  const waiting = useMemo(() => [...shared].reverse().find((m) => !m.isMine && m.kind === 'voice') ?? [...shared].reverse().find((m) => !m.isMine) ?? null, [shared]);

  if (!room) return <NoRoom />;
  const alone = ds.mode === 'live' && room.members.length < 2;

  const names = recap?.audience?.map((a) => a.displayName) ?? room.members.map((m) => m.displayName);
  const audienceLabel = room.kind === 'direct' ? `Only ${names.join(' + ')}` : `${room.name}: ${names.join(', ')}`;
  const totalSeconds = recap?.plan?.chapters.reduce((s, c) => s + c.durationSeconds, 0) ?? 0;
  const contributors = recap?.plan ? recap.plan.chapters.map((c) => c.contributorId) : [...new Set(shared.map((m) => m.ownerId))];
  const visualContributors = contributors.filter((uid) => shared.some((m) => m.ownerId === uid));

  const requestRecap = async () => {
    try {
      const r = await ds.requestRecap(room.id, day || todayIn(room.reportingZone));
      toast(r.state === 'shared' ? 'This recap is already shared.' : 'Draft requested. Each contributor reviews their part before anyone sees it.');
      await load();
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not request a recap.');
    }
  };

  const sources = showSources ? shared : shared.slice(0, 3);

  return (
    <>
      {alone ? (
        <section className="card waiting-card" aria-label="Waiting for someone to join">
          <InviteCard room={room} title={`Waiting for someone to join ${room.name}`} body="Send them an invite link. They’ll see what you share from the day they join — never anything earlier." />
        </section>
      ) : null}
      {ds.mode === 'live' && me.avatar?.kind !== 'photo_derived' ? <AvatarPrompt /> : null}
      <div className="day-grid">
        <div className="stack" style={{ gap: 18 }}>
          <section className="card" aria-labelledby="day-title">
            <div className="day-header">
              <div>
                <h1 className="page-title" id="day-title">{room.kind === 'direct' ? 'Your day, side by side' : "Our circle's day"}</h1>
                <p className="subtitle">{day ? longDate(day) : ' '}</p>
              </div>
              <span className="chip audience-pill"><Icon name="users" />{audienceLabel}</span>
            </div>
            {error ? <Notice kind="error">{error}</Notice> : null}
            {loading && !shared.length ? <p className="muted">Loading…</p> : null}
            {!loading && !shared.length ? (
              <div className="card-inner stack">
                <strong>Nothing shared for this day yet.</strong>
                <p className="muted" style={{ margin: 0 }}>SameSky never invents a story. Share a moment and it will appear here for the people you choose.</p>
                <a className="btn btn-primary" href="#/moments" style={{ alignSelf: 'start' }}>Share a moment</a>
              </div>
            ) : null}
            {shared.length ? (
              <div className="people-grid">
                {visualContributors.map((uid) => {
                  const person = room.members.find((m) => m.id === uid);
                  const theirs = shared.filter((m) => m.ownerId === uid);
                  const hero = theirs.find((m) => m.kind === 'photo') ?? theirs.find((m) => m.kind === 'clip') ?? theirs[0];
                  const chapter = recap?.plan?.chapters.find((c) => c.contributorId === uid);
                  const bg = hero?.kind === 'photo' ? hero.media?.url : hero?.media?.posterUrl ?? hero?.media?.thumbUrl;
                  return (
                    <button key={uid} className="person-card" onClick={() => hero && setDetail(hero)} aria-label={`Open ${person?.displayName}'s ${hero ? kindLabel(hero.kind, hero.media?.sourceDevice, hero.media?.origin).toLowerCase() : 'moment'}`}>
                      {bg ? <div className="media" style={{ backgroundImage: `url("${bg}")` }} /> : <div className="media text">“{hero?.caption}”</div>}
                      <div className="who">
                        <Avatar avatar={person?.avatar} size={92} name={person?.displayName} />
                        <div style={{ paddingBottom: 4 }}>
                          <h3>{person?.id === me.id ? 'Your day' : `${person?.displayName}'s day`}</h3>
                          <p>{hero?.caption ?? chapter?.segments[0]?.text}</p>
                        </div>
                      </div>
                    </button>
                  );
                })}
              </div>
            ) : null}
            {recap?.plan ? (
              <div className="control-strip">
                <button className="icon-btn" aria-label="Play our day" onClick={() => setPlaying(true)}><Icon name="play" /></button>
                <div className="timeline">
                  <strong>
                    Our day · {mmss(totalSeconds)}{' '}
                    {recap.generator === 'sample' ? <span className="chip label" title={recap.generatorLabel}>Precomputed sample</span> : recap.generator === 'chronological' ? <span className="chip label">No AI</span> : null}
                  </strong>
                  <div className="progress" aria-hidden><i style={{ width: '0%' }} /><b style={{ left: '0%' }} /></div>
                </div>
                <button className="btn btn-primary btn-lg" onClick={() => setPlaying(true)}><Icon name="play" />Play our day</button>
                <span className="divider-v" />
                <button className="btn btn-quiet" aria-expanded={showSources} onClick={() => setShowSources((s) => !s)}>View sources</button>
              </div>
            ) : shared.length ? (
              <div className="control-strip">
                <p className="muted grow" style={{ margin: 0 }}>No shared recap for this day yet.</p>
                <button className="btn btn-primary" onClick={() => void requestRecap()}><Icon name="sparkle" />Draft our day</button>
              </div>
            ) : null}
          </section>

          {pending.map((p) => <PendingReview key={p.id} recap={p} onDone={load} />)}

          {shared.length ? (
            <section className="card" aria-labelledby="sources-title">
              <div className="row" style={{ justifyContent: 'space-between', marginBottom: 12 }}>
                <h2 className="section-title" id="sources-title" style={{ margin: 0 }}>Moments behind the story</h2>
                {shared.length > 3 ? <button className="btn btn-quiet" onClick={() => setShowSources((s) => !s)}>{showSources ? 'Show fewer' : `All ${shared.length}`}</button> : null}
              </div>
              <div className="sources-row">
                {sources.map((m) => (
                  <button key={m.id} className="source-tile" onClick={() => setDetail(m)}>
                    <span className="thumb" style={{ backgroundImage: m.media?.thumbUrl ? `url("${m.media.thumbUrl}")` : m.kind === 'voice' ? 'linear-gradient(135deg,#dfe9dc,#c6d8c4)' : undefined }}>
                      {m.kind === 'clip' ? <span className="play"><Icon name="play" /></span> : m.kind === 'voice' ? <Waveform bars={12} /> : m.kind === 'text' ? <Icon name="text" /> : null}
                    </span>
                    <span className="meta">
                      <strong><SourceIcon kind={m.kind} />{kindLabel(m.kind, m.media?.sourceDevice, m.media?.origin)}</strong>
                      <span className="small muted">{m.ownerName} · {localWhen(m)}</span>
                    </span>
                  </button>
                ))}
              </div>
            </section>
          ) : null}
        </div>

        <aside className="stack" style={{ gap: 18 }}>
          {waiting ? <WaitingForYou moment={waiting} avatar={room.members.find((m) => m.id === waiting.ownerId)?.avatar} /> : null}
          <section className="card stack" style={{ gap: 10 }}>
            <span className="lock-hero"><Icon name="lockCheck" /></span>
            <h2 className="section-title" style={{ margin: 0 }}>Approved moments only</h2>
            <p className="muted" style={{ margin: 0, fontSize: '1.08rem' }}>You choose what becomes part of our day.</p>
            <details>
              <summary className="small" style={{ color: 'var(--green-strong)', fontWeight: 600 }}>How approval works</summary>
              <p className="small muted" style={{ margin: '6px 0 0' }}>Every contributor reviews what a recap says about their own moments before anyone else sees it. Withdrawing a moment removes it from the story too.</p>
            </details>
          </section>
          {room.kind === 'circle' ? <PrivacyCheck /> : null}
        </aside>
      </div>
      {playing && recap?.plan ? <StoryPlayer recap={recap} room={room} onClose={() => setPlaying(false)} onOpenMoment={(m) => { setPlaying(false); setDetail(m); }} /> : null}
      {detail ? <MomentDetail moment={detail} room={room} onClose={() => { setDetail(null); void load(); }} /> : null}
    </>
  );
}

function WaitingForYou({ moment, avatar }: { moment: Moment; avatar?: AvatarView | null }) {
  const [playing, setPlaying] = useState(false);
  const [replying, setReplying] = useState(false);
  const [cues, setCues] = useState<Cue[]>([]);
  const [t, setT] = useState(0);
  const audio = useRef<HTMLAudioElement>(null);
  useEffect(() => {
    if (moment.media?.captionsUrl) void loadCaptions(moment.media.captionsUrl).then(setCues);
  }, [moment.media?.captionsUrl]);
  const duration = moment.media?.durationMs ? Math.round(moment.media.durationMs / 1000) : null;
  return (
    <section className="card waiting" aria-label="Waiting for you">
      <h2 className="section-title" style={{ margin: 0, fontSize: 'clamp(1.5rem, 2vw, 1.95rem)' }}>Waiting for you</h2>
      <div className="row" style={{ gap: 16 }}>
        <Avatar avatar={avatar} size={104} name={moment.ownerName} />
        {moment.kind === 'voice' ? <Waveform playing={playing} /> : null}
      </div>
      <div className="quote">“{moment.caption}”</div>
      <div className="muted" style={{ fontSize: '1.06rem' }}>
        {moment.ownerName} · {kindLabel(moment.kind, moment.media?.sourceDevice, moment.media?.origin)}{duration ? ` · 0:${String(duration).padStart(2, '0')}` : ''}
      </div>
      {moment.kind === 'voice' && moment.media?.url ? (
        <audio ref={audio} src={moment.media.url} onPlay={() => setPlaying(true)} onPause={() => setPlaying(false)} onEnded={() => setPlaying(false)} onTimeUpdate={(e) => setT(e.currentTarget.currentTime)} />
      ) : null}
      {playing ? <div className="captions" aria-live="polite">{cueAt(cues, t, moment.transcript ?? moment.caption)}</div> : null}
      <div className="row" style={{ justifyContent: 'space-between' }}>
        {moment.kind === 'voice' ? (
          <button className="icon-btn" aria-label={playing ? 'Pause' : 'Play voice note'} onClick={() => { const el = audio.current; if (el) { if (el.paused) void el.play(); else el.pause(); } }}>
            <Icon name={playing ? 'pause' : 'play'} />
          </button>
        ) : <span />}
        <button className="btn btn-primary btn-lg" aria-expanded={replying} onClick={() => setReplying((r) => !r)}><Icon name="reply" />Reply</button>
      </div>
      {replying ? <Conversation moment={moment} /> : null}
      <p className="small muted" style={{ margin: 0 }}>Recorded message · {localWhen(moment)}</p>
    </section>
  );
}

function PendingReview({ recap, onDone }: { recap: Recap; onDone: () => void }) {
  const { ds, me, toast } = useApp();
  const mine = recap.contributors.find((c) => c.userId === me.id);
  const approve = async () => {
    try {
      const r = await ds.approveRecap(recap.id);
      toast(r.state === 'shared' ? 'Approved — the recap is now shared with its audience.' : 'Approved. Waiting for the other contributors.');
      onDone();
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not approve.');
      onDone();
    }
  };
  return (
    <section className="card stack" aria-label="Recap waiting for review">
      <div className="row wrap">
        <h2 className="section-title grow" style={{ margin: 0 }}>Review before it's shared</h2>
        <span className="chip label">{recap.state === 'queued' || recap.state === 'generating' ? 'Drafting…' : recap.generatorLabel}</span>
      </div>
      {recap.plan ? (
        <>
          <p className="muted" style={{ margin: 0 }}>Only included contributors can see this preview. Check what it says about your moments.</p>
          {recap.plan.chapters.map((c) => (
            <div key={c.contributorId} className="card-inner stack" style={{ gap: 6 }}>
              <strong>{recap.contributors.find((x) => x.userId === c.contributorId)?.displayName ?? 'Contributor'}{c.contributorId === me.id ? ' (you)' : ''}</strong>
              {c.segments.map((s, i) => <div key={i}><span className="small muted">{s.kind === 'visual_interpretation' ? 'Looks like · ' : ''}</span>{s.text}</div>)}
            </div>
          ))}
          <div className="row wrap">
            {recap.contributors.map((c) => <span key={c.userId} className={`chip label${c.approved ? ' green' : ''}`}>{c.displayName}: {c.approved ? 'approved' : 'reviewing'}</span>)}
          </div>
          {mine && !mine.approved ? <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => void approve()}><Icon name="check" />Approve my part</button> : null}
        </>
      ) : <p className="muted">The draft is being prepared. Shared moments stay viewable meanwhile.</p>}
    </section>
  );
}

function PrivacyCheck() {
  const { ds, rooms, room } = useApp();
  const [count, setCount] = useState<number | null>(null);
  useEffect(() => {
    const direct = rooms.filter((r) => r.kind === 'direct');
    void Promise.all(direct.map((r) => ds.moments(r.id))).then((lists) => setCount(lists.flat().filter((m) => m.sharingState === 'shared').length)).catch(() => setCount(null));
  }, [ds, rooms, room]);
  return (
    <section className="card stack" style={{ gap: 8 }}>
      <h2 className="section-title" style={{ margin: 0 }}>Private stays private</h2>
      <p className="muted" style={{ margin: 0 }}>
        {count ? `${count} moment${count > 1 ? 's' : ''} from your direct room${count > 1 ? 's are' : ' is'} not part of this circle — not in its story, feed, keepsakes or notifications.` : 'Moments from direct rooms never appear in a circle.'}
      </p>
    </section>
  );
}

/** Onboarding: invite people to make their avatar from their own photos (dismissible; the demo stays labelled). */
function AvatarPrompt() {
  const { me } = useApp();
  const key = `samesky.avatarPrompt.${me.id}`;
  const [hidden, setHidden] = useState(() => { try { return localStorage.getItem(key) === 'dismissed'; } catch { return false; } });
  if (hidden) return null;
  const dismiss = () => {
    try { localStorage.setItem(key, 'dismissed'); } catch { /* ignore */ }
    setHidden(true);
  };
  return (
    <section className="card onboarding-avatar" aria-label="Your avatar" style={{ marginBottom: 18 }}>
      <Avatar avatar={me.avatar} size={84} name={me.displayName} demoBadge />
      <div className="stack" style={{ gap: 4 }}>
        <strong style={{ fontSize: '1.1rem' }}>Let people recognise you at the table</strong>
        <span className="muted small">You’re using a labelled demo avatar. Create a 3D avatar from your own photo — only you see it until you approve it.</span>
      </div>
      <div className="row wrap">
        <a className="btn btn-primary" href="#/settings?avatar=create">Create from my photos</a>
        <button className="btn btn-quiet" onClick={dismiss}>Keep the demo avatar</button>
      </div>
    </section>
  );
}
