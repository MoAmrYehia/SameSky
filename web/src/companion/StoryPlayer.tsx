import { useEffect, useMemo, useRef, useState } from 'react';
import { cueAt, loadCaptions, type Cue } from '../shared/captions';
import type { Moment, Recap, Room } from '../shared/contracts';
import { localWhen, Avatar, Icon, Waveform } from './ui';

interface Step {
  chapterIndex: number;
  moment: Moment | null;
  seconds: number;
}

const KIND_LABEL: Record<string, string> = {
  stated: 'From their caption',
  quote: 'In their words',
  visual_interpretation: 'Looks like · AI interpretation',
};

export function StoryPlayer({ recap, room, onClose, onOpenMoment }: { recap: Recap; room: Room; onClose: () => void; onOpenMoment: (m: Moment) => void }) {
  const plan = recap.plan!;
  const byId = useMemo(() => new Map(recap.playback.map((m) => [m.id, m])), [recap]);
  const steps = useMemo<Step[]>(() => {
    const out: Step[] = [];
    plan.chapters.forEach((ch, chapterIndex) => {
      const media = ch.playbackMomentIds.map((id) => byId.get(id)).filter((m): m is Moment => !!m);
      if (!media.length) out.push({ chapterIndex, moment: null, seconds: Math.min(ch.durationSeconds, 12) });
      media.forEach((m) => out.push({ chapterIndex, moment: m, seconds: m.media?.durationMs ? Math.min(15, m.media.durationMs / 1000 + 1) : Math.max(6, ch.durationSeconds / media.length / 2) }));
    });
    out.push({ chapterIndex: -1, moment: null, seconds: 8 });
    return out;
  }, [plan, byId]);

  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [captionsOn, setCaptionsOn] = useState(true);
  const [cues, setCues] = useState<Cue[]>([]);
  const mediaRef = useRef<HTMLVideoElement | HTMLAudioElement | null>(null);
  const step = steps[index];
  const chapter = step.chapterIndex >= 0 ? plan.chapters[step.chapterIndex] : null;
  const member = (id: string) => room.members.find((m) => m.id === id);
  const timed = !step.moment || step.moment.kind === 'photo' || step.moment.kind === 'text';

  useEffect(() => {
    setElapsed(0);
    setCues([]);
    if (step.moment?.media?.captionsUrl) void loadCaptions(step.moment.media.captionsUrl).then(setCues);
  }, [index, step.moment]);

  useEffect(() => {
    if (paused || !timed) return;
    const t = window.setInterval(() => setElapsed((e) => e + 0.1), 100);
    return () => window.clearInterval(t);
  }, [paused, timed, index]);

  useEffect(() => {
    if (timed && elapsed >= step.seconds) setIndex((i) => Math.min(steps.length - 1, i + 1));
  }, [elapsed, step.seconds, steps.length, timed]);

  useEffect(() => {
    const el = mediaRef.current;
    if (!el) return;
    if (paused) el.pause();
    else void el.play().catch(() => setPaused(true)); // autoplay blocked: wait for an explicit tap
  }, [paused, index]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
      if (e.key === ' ') { e.preventDefault(); setPaused((p) => !p); }
      if (e.key === 'ArrowRight') setIndex((i) => Math.min(steps.length - 1, i + 1));
      if (e.key === 'ArrowLeft') setIndex((i) => Math.max(0, i - 1));
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose, steps.length]);

  const m = step.moment;
  const progressFor = (ci: number) => {
    const chapterSteps = steps.map((s, i) => ({ s, i })).filter(({ s }) => s.chapterIndex === ci);
    const done = chapterSteps.filter(({ i }) => i < index).length;
    const current = chapterSteps.some(({ i }) => i === index) ? Math.min(1, elapsed / step.seconds) : 0;
    return chapterSteps.length ? ((done + current) / chapterSteps.length) * 100 : 0;
  };
  const captionText = m?.kind === 'voice' || m?.kind === 'clip' ? cueAt(cues, elapsed, m.transcript ?? (m.kind === 'voice' ? m.caption : '')) : '';

  return (
    <div className="overlay" role="dialog" aria-modal="true" aria-label={`Recap: ${plan.title}`}>
      <div className="story">
        <div className="story-stage">
          {m?.kind === 'photo' && m.media?.url ? <img className="kenburns" src={m.media.url} alt={m.caption || `${m.ownerName}'s photo`} key={m.id} /> : null}
          {m?.kind === 'clip' && m.media?.url ? (
            <video key={m.id} ref={(el) => { mediaRef.current = el; }} src={m.media.url} playsInline muted={false}
              onTimeUpdate={(e) => setElapsed(e.currentTarget.currentTime)} onEnded={() => setIndex((i) => Math.min(steps.length - 1, i + 1))} />
          ) : null}
          {m?.kind === 'voice' ? (
            <div className="voice-stage">
              <div className="stack" style={{ alignItems: 'center', gap: 18, width: '70%' }}>
                <Avatar avatar={member(m.ownerId)?.avatar} size={120} name={m.ownerName} />
                <Waveform playing={!paused} />
                <span className="chip">Recorded message · {m.ownerName} · {localWhen(m)}</span>
              </div>
              {m.media?.url ? <audio key={m.id} ref={(el) => { mediaRef.current = el; }} src={m.media.url} onTimeUpdate={(e) => setElapsed(e.currentTarget.currentTime)} onEnded={() => setIndex((i) => Math.min(steps.length - 1, i + 1))} /> : null}
            </div>
          ) : null}
          {!m && chapter ? (
            <div className="voice-stage" style={{ padding: 32 }}>
              <div className="stack" style={{ alignItems: 'center', textAlign: 'center', maxWidth: 640 }}>
                <Avatar avatar={member(chapter.contributorId)?.avatar} size={110} />
                <p style={{ fontSize: '1.6rem', fontFamily: 'var(--display)' }}>{chapter.segments[0]?.text}</p>
              </div>
            </div>
          ) : null}
          {!m && !chapter ? (
            <div className="voice-stage" style={{ padding: 32 }}>
              <div className="stack" style={{ alignItems: 'center', textAlign: 'center' }}>
                <div className="row">{plan.chapters.map((c) => <Avatar key={c.contributorId} avatar={member(c.contributorId)?.avatar} size={84} />)}</div>
                <h2 style={{ fontSize: '2rem' }}>{plan.closingLine || plan.title}</h2>
                {recap.omittedCount ? <p className="small" style={{ color: '#cfd6cf' }}>{recap.omittedCount} more shared moment{recap.omittedCount > 1 ? 's were' : ' was'} not included in this short story.</p> : null}
              </div>
            </div>
          ) : null}
          {captionsOn && captionText ? <div className="caption-line"><div className="captions">{captionText}</div></div> : null}
        </div>
        <div className="chapters" aria-hidden style={{ paddingTop: 12 }}>
          {plan.chapters.map((_, ci) => <i key={ci}><b style={{ width: `${progressFor(ci)}%` }} /></i>)}
        </div>
        <div className="story-body">
          <div className="stack" style={{ gap: 10 }}>
            <div className="row wrap" style={{ gap: 8 }}>
              <span className="chip label">{recap.generatorLabel}</span>
              {chapter ? <span className="chip label">{member(chapter.contributorId)?.displayName}'s chapter</span> : <span className="chip label">Together</span>}
            </div>
            <h2 style={{ fontSize: '1.5rem' }}>{plan.title}</h2>
            {chapter
              ? chapter.segments.map((seg, i) => (
                  <div key={i}>
                    <div className="seg-kind">{KIND_LABEL[seg.kind]}</div>
                    <div className="seg">{seg.text}</div>
                    <div className="row wrap" style={{ gap: 6, marginTop: 6 }}>
                      {seg.sources.map((s) => {
                        const src = byId.get(s.momentId);
                        return src ? (
                          <button key={s.momentId} className="chip label" style={{ border: 0, cursor: 'pointer' }} onClick={() => onOpenMoment(src)}>
                            Source: {src.ownerName}'s {src.kind === 'voice' ? 'voice note' : src.kind} · {src.captureLocal.time}
                          </button>
                        ) : <span key={s.momentId} className="chip label">Source: shared note</span>;
                      })}
                    </div>
                  </div>
                ))
              : <p className="muted">That's the end of this day's story.</p>}
          </div>
          <div className="story-controls" style={{ alignSelf: 'start' }}>
            <button className="icon-btn" aria-label="Previous" onClick={() => setIndex((i) => Math.max(0, i - 1))}><Icon name="prev" /></button>
            <button className="icon-btn solid" aria-label={paused ? 'Play' : 'Pause'} onClick={() => setPaused((p) => !p)}><Icon name={paused ? 'play' : 'pause'} /></button>
            <button className="icon-btn" aria-label="Next" onClick={() => setIndex((i) => Math.min(steps.length - 1, i + 1))}><Icon name="next" /></button>
            <button className="icon-btn" aria-label={captionsOn ? 'Hide captions' : 'Show captions'} aria-pressed={captionsOn} onClick={() => setCaptionsOn((c) => !c)}><Icon name="cc" /></button>
            <button className="btn btn-outline" onClick={onClose}>Close</button>
          </div>
        </div>
      </div>
    </div>
  );
}
