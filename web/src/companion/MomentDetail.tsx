import { useEffect, useRef, useState } from 'react';
import { cueAt, loadCaptions, type Cue } from '../shared/captions';
import type { Moment, ReactionKind, Reply, Room } from '../shared/contracts';
import { useApp } from './App';
import { localWhen, Avatar, Icon, kindLabel, Notice, Waveform } from './ui';

const REACTIONS: { kind: ReactionKind; emoji: string; label: string }[] = [
  { kind: 'heart', emoji: '❤️', label: 'Love' },
  { kind: 'smile', emoji: '😊', label: 'Smile' },
  { kind: 'hug', emoji: '🤗', label: 'Hug' },
  { kind: 'sun', emoji: '🌅', label: 'Same sky' },
];

export function MomentDetail({ moment, room, onClose }: { moment: Moment; room: Room; onClose: () => void }) {
  return (
    <div className="overlay" role="dialog" aria-modal="true" aria-label={`${moment.ownerName}'s moment`} onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="card stack" style={{ width: 'min(860px, 100%)', maxHeight: '100%', overflow: 'auto' }}>
        <div className="row">
          <Avatar def={room.members.find((m) => m.id === moment.ownerId)?.avatar?.definition} size={52} name={moment.ownerName} />
          <div className="grow">
            <strong>{moment.ownerName}</strong>
            <div className="muted small">{kindLabel(moment.kind, moment.media?.sourceDevice)} · {localWhen(moment)}</div>
          </div>
          <button className="icon-btn" aria-label="Close" onClick={onClose}><Icon name="x" /></button>
        </div>
        <MomentMedia moment={moment} />
        {moment.caption ? <p style={{ fontSize: '1.2rem', margin: 0 }}>{moment.caption}</p> : null}
        <p className="small muted" style={{ margin: 0 }}>
          <Icon name="lock" /> Shared with {moment.audience.map((a) => a.displayName).join(', ') || 'only you'}
        </p>
        <Conversation moment={moment} />
      </div>
    </div>
  );
}

export function MomentMedia({ moment, autoPlay = false }: { moment: Moment; autoPlay?: boolean }) {
  const [cues, setCues] = useState<Cue[]>([]);
  const [t, setT] = useState(0);
  const [playing, setPlaying] = useState(false);
  const audio = useRef<HTMLAudioElement>(null);
  useEffect(() => {
    if (moment.media?.captionsUrl) void loadCaptions(moment.media.captionsUrl).then(setCues);
  }, [moment.media?.captionsUrl]);
  const media = moment.media;
  if (!media) return <div className="card-inner" style={{ fontSize: '1.3rem', fontFamily: 'var(--display)' }}>{moment.caption}</div>;
  if (media.processingState !== 'ready' || !media.url) {
    return <Notice kind={media.processingState === 'failed' ? 'error' : 'info'}>{media.processingState === 'failed' ? media.safeError ?? 'This media failed to process.' : 'Still processing for playback…'}</Notice>;
  }
  if (media.kind === 'image') return <div className="preview"><img src={media.url} alt={moment.caption || `${moment.ownerName}'s photo`} /></div>;
  if (media.kind === 'video') {
    return (
      <div className="preview">
        <video src={media.url} controls playsInline autoPlay={autoPlay} poster={media.thumbUrl} onTimeUpdate={(e) => setT(e.currentTarget.currentTime)} />
      </div>
    );
  }
  const caption = cueAt(cues, t, moment.transcript ?? moment.caption);
  return (
    <div className="card-inner stack">
      <div className="row">
        <button className="icon-btn solid" aria-label={playing ? 'Pause voice note' : 'Play voice note'} onClick={() => {
          const el = audio.current;
          if (!el) return;
          if (el.paused) void el.play(); else el.pause();
        }}><Icon name={playing ? 'pause' : 'play'} /></button>
        <Waveform playing={playing} />
      </div>
      <audio ref={audio} src={media.url} onPlay={() => setPlaying(true)} onPause={() => setPlaying(false)} onEnded={() => setPlaying(false)} onTimeUpdate={(e) => setT(e.currentTarget.currentTime)} />
      {playing || t > 0 ? <div className="captions" aria-live="polite">{caption || ' '}</div> : <p className="small muted" style={{ margin: 0 }}>Captions appear while it plays.</p>}
    </div>
  );
}

export function Conversation({ moment }: { moment: Moment }) {
  const { ds, me, toast } = useApp();
  const [replies, setReplies] = useState<Reply[]>([]);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    void ds.replies(moment.id).then(setReplies).catch(() => setReplies([]));
  }, [ds, moment.id]);
  const send = async () => {
    if (!text.trim()) return;
    setBusy(true);
    try {
      const r = await ds.reply(moment.id, text.trim());
      setReplies((list) => [...list, r]);
      setText('');
      toast('Reply left. It will be waiting for them.');
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Could not send.');
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="stack">
      <div className="row wrap" style={{ gap: 6 }}>
        {REACTIONS.map((r) => {
          const mine = moment.reactions.some((x) => x.kind === r.kind && x.userId === me.id);
          return (
            <button key={r.kind} className={`chip${mine ? ' green' : ''}`} style={{ border: 0, cursor: 'pointer' }} aria-pressed={mine}
              onClick={() => void ds.react(moment.id, r.kind).then(() => toast(`Reacted ${r.emoji}`))}>
              <span aria-hidden>{r.emoji}</span>{r.label}
            </button>
          );
        })}
      </div>
      {replies.length ? (
        <div className="reply-list" aria-label="Replies">
          {replies.map((r) => (
            <div key={r.id} className="reply">
              <div className="label">{r.authorName} · {r.label} · {new Date(r.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}</div>
              <div>{r.text}</div>
            </div>
          ))}
        </div>
      ) : null}
      <div className="row">
        <label className="grow"><span className="sr-only">Reply</span>
          <input className="input" placeholder={`Reply to ${moment.isMine ? 'this moment' : moment.ownerName}…`} value={text} maxLength={1000} onChange={(e) => setText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && void send()} />
        </label>
        <button className="btn btn-primary" disabled={busy || !text.trim()} onClick={() => void send()}><Icon name="reply" />Reply</button>
      </div>
      <p className="small muted" style={{ margin: 0 }}>Replies stay with the same people who can see this moment. To talk to one person, use their direct room.</p>
    </div>
  );
}
