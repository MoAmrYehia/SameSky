import { useEffect, useState } from 'react';
import type { Award, Moment, Recap } from '../../shared/contracts';
import { formatDate } from '../../shared/tz';
import { useApp } from '../App';
import { MomentDetail } from '../MomentDetail';
import { StoryPlayer } from '../StoryPlayer';
import { Icon, KeepsakeRender, Notice } from '../ui';

export function Keepsakes() {
  const { ds, room } = useApp();
  const [awards, setAwards] = useState<Award[]>([]);
  const [archive, setArchive] = useState<Recap[]>([]);
  const [open, setOpen] = useState<Award | null>(null);
  const [playing, setPlaying] = useState<Recap | null>(null);
  const [detail, setDetail] = useState<Moment | null>(null);

  useEffect(() => {
    if (!room) return;
    void Promise.all([ds.awards(room.id), ds.archive(room.id)]).then(([a, r]) => { setAwards(a); setArchive(r); });
  }, [ds, room]);

  if (!room) return <Notice>Create or join a room first.</Notice>;
  return (
    <>
      <h1 className="page-title on-backdrop">Keepsakes</h1>
      <p className="subtitle on-backdrop" style={{ marginBottom: 20, opacity: 0.85 }}>Milestones you reach together in {room.name}. No streaks, no scores — just shared history.</p>
      <div className="keepsake-row">
        {awards.map((a) => (
          <button key={a.milestone} className="keepsake" onClick={() => setOpen(a)} aria-label={`${a.title}: ${a.earned ? 'earned' : `${a.progress} of ${a.target}`}`}>
            <div className="art"><KeepsakeRender kind={a.keepsake} progress={a.progress} target={a.target} earned={a.earned} title={a.title} /></div>
            <div className="info">
              <strong>{a.title}</strong>
              {a.earned ? <span className="chip green label" style={{ alignSelf: 'start' }}><Icon name="checkCircle" />Earned {a.earnedAt ? formatDate(a.earnedAt, room.reportingZone, { day: 'numeric', month: 'short' }) : ''}</span>
                : <span className="row"><span className="meter"><i style={{ width: `${(a.progress / a.target) * 100}%` }} /></span><span className="small muted">{a.progress} / {a.target}</span></span>}
            </div>
          </button>
        ))}
      </div>

      <section className="card" style={{ marginTop: 28 }}>
        <h2 className="section-title">Daily archive</h2>
        {archive.length ? (
          <div className="stack">
            {archive.map((r) => (
              <div key={r.id} className="card-inner row wrap">
                <div className="grow">
                  <strong>{r.plan?.title}</strong>
                  <div className="small muted">{formatDate(`${r.roomDate}T12:00:00Z`, 'UTC')} · version {r.version} · {r.generatorLabel}</div>
                </div>
                <button className="btn btn-primary" onClick={() => setPlaying(r)}><Icon name="play" />Play</button>
              </div>
            ))}
          </div>
        ) : <p className="muted">Shared daily recaps will collect here.</p>}
      </section>

      {open ? (
        <div className="overlay" role="dialog" aria-modal="true" aria-label={open.title} onClick={(e) => e.target === e.currentTarget && setOpen(null)}>
          <div className="card stack" style={{ width: 'min(560px, 100%)' }}>
            <div className="keepsake" style={{ boxShadow: 'none', cursor: 'default' }}><div className="art" style={{ borderRadius: 18 }}><KeepsakeRender kind={open.keepsake} progress={open.progress} target={open.target} earned={open.earned} title={open.title} /></div></div>
            <h2 className="section-title" style={{ margin: 0 }}>{open.title}</h2>
            <p className="muted" style={{ margin: 0 }}>{open.trigger}</p>
            <p style={{ margin: 0 }}>{open.earned ? `Earned together${open.earnedAt ? ` on ${formatDate(open.earnedAt, room.reportingZone)}` : ''}.` : `${open.progress} of ${open.target} so far. Missed days never count against you.`}</p>
            {open.sourceRecapId ? (
              <button className="btn btn-outline" style={{ alignSelf: 'start' }} onClick={() => { const r = archive.find((x) => x.id === open.sourceRecapId); setOpen(null); if (r) setPlaying(r); }}>
                <Icon name="play" />Open the day it happened
              </button>
            ) : null}
            <button className="btn btn-primary" style={{ alignSelf: 'start' }} onClick={() => setOpen(null)}>Close</button>
          </div>
        </div>
      ) : null}
      {playing?.plan ? <StoryPlayer recap={playing} room={room} onClose={() => setPlaying(null)} onOpenMoment={(m) => { setPlaying(null); setDetail(m); }} /> : null}
      {detail ? <MomentDetail moment={detail} room={room} onClose={() => setDetail(null)} /> : null}
    </>
  );
}
