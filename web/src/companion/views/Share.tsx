import { useEffect, useMemo, useRef, useState } from 'react';
import type { AudienceResolution, Moment, MomentKind, SourceDevice } from '../../shared/contracts';
import { useApp } from '../App';
import { MomentMedia } from '../MomentDetail';
import { localWhen, Avatar, Checkbox, deviceLabel, Icon, kindLabel, Notice, Toggle } from '../ui';

type Tab = 'upload' | 'camera' | 'voice' | 'text';
const DEVICES: SourceDevice[] = ['phone', 'camera', 'action_camera', 'drone', 'glasses', 'webcam', 'laptop', 'tablet', 'other'];
const ACCEPT = 'image/jpeg,image/png,video/mp4,video/quicktime,video/webm';
const IMAGE_LIMIT = 10 * 1024 * 1024;
const VIDEO_LIMIT = 100 * 1024 * 1024;

interface Draft {
  blob: Blob;
  name: string;
  kind: MomentKind;
  url: string;
  durationMs?: number;
}

async function downscaleImage(file: File): Promise<Blob> {
  // Resize before upload where practical. Drawing to a canvas applies EXIF orientation and drops metadata (incl. GPS).
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    const scale = Math.min(1, 2560 / Math.max(bitmap.width, bitmap.height));
    if (scale === 1 && file.size < 4 * 1024 * 1024) return file;
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    return await new Promise<Blob>((res) => canvas.toBlob((b) => res(b ?? file), 'image/jpeg', 0.88));
  } catch {
    return file;
  }
}

function toLocalInput(ms: number) {
  const d = new Date(ms - new Date(ms).getTimezoneOffset() * 60_000);
  return d.toISOString().slice(0, 16);
}

export function Share() {
  const { ds, me, rooms, room: currentRoom, toast } = useApp();
  const [tab, setTab] = useState<Tab>('upload');
  const [draft, setDraft] = useState<Draft | null>(null);
  const [text, setText] = useState('');
  const [caption, setCaption] = useState('');
  const [device, setDevice] = useState<SourceDevice>('phone');
  const [captureLocal, setCaptureLocal] = useState(toLocalInput(Date.now()));
  const [trim, setTrim] = useState<[number, number]>([0, 15_000]);
  const [mode, setMode] = useState<'one' | 'circle'>(currentRoom?.kind === 'circle' ? 'circle' : 'one');
  const [roomId, setRoomId] = useState<string | undefined>(currentRoom?.id);
  const [recipients, setRecipients] = useState<string[]>([]);
  const [aiAllowed, setAiAllowed] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<{ moment: Moment; resolution: AudienceResolution } | null>(null);
  const [mine, setMine] = useState<Moment[]>([]);
  const [drag, setDrag] = useState(false);

  const candidates = rooms.filter((r) => (mode === 'one' ? r.kind === 'direct' : r.kind === 'circle'));
  const room = candidates.find((r) => r.id === roomId) ?? candidates[0];
  const others = room?.members.filter((m) => m.id !== me.id) ?? [];

  useEffect(() => {
    setRecipients(mode === 'one' ? others.slice(0, 1).map((m) => m.id) : others.map((m) => m.id));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [room?.id, mode]);

  const loadMine = async () => {
    const lists = await Promise.all(rooms.map((r) => ds.moments(r.id).catch(() => [] as Moment[])));
    setMine(lists.flat().filter((m) => m.isMine).sort((a, b) => b.captureAt.localeCompare(a.captureAt)).slice(0, 12));
  };
  useEffect(() => { void loadMine(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [ds, rooms]);

  const pickFile = async (file: File | undefined) => {
    setError(null);
    if (!file) return;
    const isImage = /^image\/(jpeg|png)$/.test(file.type);
    const isVideo = /^video\/(mp4|quicktime|webm)$/.test(file.type) || /\.(mp4|mov|m4v|webm)$/i.test(file.name);
    if (!isImage && !isVideo) return setError('That file type isn’t supported yet. Use JPEG or PNG photos, or MP4, MOV or WebM clips. (HEIC photos: export as JPEG first.)');
    if (isImage && file.size > IMAGE_LIMIT) return setError('Photos can be up to 10 MB.');
    if (isVideo && file.size > VIDEO_LIMIT) return setError('Source clips can be up to 100 MB and 60 seconds.');
    const blob = isImage ? await downscaleImage(file) : file;
    const url = URL.createObjectURL(blob);
    setCaptureLocal(toLocalInput(file.lastModified || Date.now()));
    if (isVideo) {
      const v = document.createElement('video');
      v.preload = 'metadata';
      v.src = url;
      v.onloadedmetadata = () => {
        const d = Math.round(v.duration * 1000);
        if (d > 60_500) setError('This clip is over 60 seconds. Trim it below, or export a shorter section.');
        setTrim([0, Math.min(d, 15_000)]);
        setDraft({ blob, name: file.name, kind: 'clip', url, durationMs: d });
      };
      v.onerror = () => setError('This browser can’t preview that clip, but you can still upload it — the server checks the actual codecs.');
      setDraft({ blob, name: file.name, kind: 'clip', url });
    } else {
      setDraft({ blob, name: file.name.replace(/\.(png|jpe?g)$/i, '') + '.jpg', kind: 'photo', url });
    }
  };

  const kind: MomentKind = tab === 'text' ? 'text' : draft?.kind ?? (tab === 'voice' ? 'voice' : 'photo');
  const ready = tab === 'text' ? text.trim().length > 0 : !!draft;
  const audienceText = recipients.length === 1 ? `Share with ${others.find((o) => o.id === recipients[0])?.displayName ?? 'them'}` : room?.kind === 'circle' && recipients.length === others.length ? `Share with ${room.name.replace(/ circle$/i, '').toLowerCase() === 'family' ? 'family' : room.name}` : `Share with ${recipients.length} people`;

  const create = async () => {
    if (!room) throw new Error('Choose a room first.');
    return ds.createMoment(
      {
        roomId: room.id, kind, caption: tab === 'text' ? text.trim() : caption.trim(), file: draft?.blob, fileName: draft?.name, sourceDevice: tab === 'camera' ? 'webcam' : device,
        captureAt: new Date(captureLocal).toISOString(), aiAllowed,
        trimStartMs: kind === 'clip' ? trim[0] : undefined, trimEndMs: kind === 'clip' ? trim[1] : undefined,
      },
      (s) => setBusy(s),
    );
  };

  const reset = () => {
    setDraft(null);
    setText('');
    setCaption('');
  };

  const saveOnly = async () => {
    setError(null);
    try {
      await create();
      toast('Saved privately. Only you can see it until you choose to share.');
      reset();
      void loadMine();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not save.');
    } finally {
      setBusy(null);
    }
  };

  const startShare = async () => {
    setError(null);
    if (!recipients.length) return setError('Choose at least one person.');
    try {
      const moment = await create();
      setBusy('Checking who will see it…');
      const resolution = await ds.resolveAudience(moment.id, recipients);
      setConfirm({ moment, resolution });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not share.');
    } finally {
      setBusy(null);
    }
  };

  const confirmShare = async () => {
    if (!confirm) return;
    try {
      setBusy('Sharing…');
      await ds.share(confirm.moment.id, confirm.resolution.recipients.map((r) => r.userId), confirm.resolution.confirmationToken);
      toast(`Shared — ${confirm.resolution.label}.`);
      setConfirm(null);
      reset();
      void loadMine();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not share.');
      setConfirm(null);
      void loadMine();
    } finally {
      setBusy(null);
    }
  };

  const withdraw = async (m: Moment) => {
    if (!window.confirm(`Withdraw this moment? ${m.audience.map((a) => a.displayName).join(', ') || 'Nobody'} will no longer be able to open it, and any recap using it is removed. Copies people already downloaded can't be recalled.`)) return;
    await ds.withdraw(m.id);
    toast('Withdrawn.');
    void loadMine();
  };

  return (
    <>
      <h1 className="page-title" style={{ marginBottom: 20 }}>Share a moment</h1>
      <div className="share-grid">
        <section className="stack" style={{ gap: 16 }}>
          <div className="tabs" role="tablist" aria-label="How to add a moment">
            {([['upload', 'upload', 'Upload'], ['camera', 'camera', 'Camera'], ['voice', 'mic', 'Voice note'], ['text', 'text', 'Text']] as const).map(([t, icon, label]) => (
              <button key={t} className="tab" role="tab" aria-selected={tab === t} onClick={() => { setTab(t); reset(); setError(null); }}><Icon name={icon} />{label}</button>
            ))}
          </div>
          <div className="card stack">
            {tab === 'upload' && !draft ? (
              <label className={`dropzone${drag ? ' drag' : ''}`} onDragOver={(e) => { e.preventDefault(); setDrag(true); }} onDragLeave={() => setDrag(false)}
                onDrop={(e) => { e.preventDefault(); setDrag(false); void pickFile(e.dataTransfer.files[0]); }}>
                <input type="file" accept={ACCEPT} className="sr-only" onChange={(e) => void pickFile(e.target.files?.[0])} />
                <span className="stack" style={{ alignItems: 'center' }}>
                  <span className="icon-btn solid"><Icon name="upload" /></span>
                  <strong style={{ fontSize: '1.2rem' }}>Choose a photo or clip</strong>
                  <span className="muted">From a phone, camera, action camera, drone or AI-glasses export. JPEG/PNG up to 10 MB · MP4/MOV/WebM up to 60 s and 100 MB.</span>
                </span>
              </label>
            ) : null}
            {tab === 'camera' && !draft ? <CameraCapture onDone={(d) => setDraft(d)} onError={setError} /> : null}
            {tab === 'voice' && !draft ? <VoiceCapture onDone={(d) => setDraft(d)} onError={setError} /> : null}
            {tab === 'text' ? (
              <label className="field"><span>What happened?</span><textarea className="input" maxLength={500} value={text} onChange={(e) => setText(e.target.value)} placeholder="Made dumplings for the whole office…" /></label>
            ) : null}
            {draft ? (
              <div className="stack">
                <div className="row"><Icon name={draft.kind === 'photo' ? 'image' : draft.kind === 'clip' ? 'video' : 'mic'} /><strong className="grow">{draft.name}</strong>
                  <button className="btn btn-quiet" onClick={() => setDraft(null)}>Choose another</button></div>
                <div className="preview">
                  {draft.kind === 'photo' ? <img src={draft.url} alt="Preview" /> : draft.kind === 'clip' ? <video src={draft.url} controls playsInline /> : <audio src={draft.url} controls style={{ width: '100%' }} />}
                </div>
                {draft.kind === 'clip' && draft.durationMs ? (
                  <div className="card-inner stack">
                    <strong>Trim the highlight (up to 15 seconds)</strong>
                    <div className="trim">
                      <span className="small">Start</span>
                      <input type="range" min={0} max={draft.durationMs} step={100} value={trim[0]} aria-label="Trim start"
                        onChange={(e) => { const s = Number(e.target.value); setTrim([s, Math.min(draft.durationMs!, Math.max(s + 1000, Math.min(trim[1], s + 15_000)))]); }} />
                      <span className="small">{(trim[0] / 1000).toFixed(1)}s</span>
                      <span className="small">End</span>
                      <input type="range" min={0} max={draft.durationMs} step={100} value={trim[1]} aria-label="Trim end"
                        onChange={(e) => { const end = Number(e.target.value); setTrim([Math.max(0, Math.min(trim[0], end - 1000), end - 15_000), end]); }} />
                      <span className="small">{(trim[1] / 1000).toFixed(1)}s</span>
                    </div>
                    <span className="muted small">Highlight: {((trim[1] - trim[0]) / 1000).toFixed(1)} s. The server converts it to a Quest-friendly MP4 and removes location metadata.</span>
                  </div>
                ) : null}
              </div>
            ) : null}
            {tab !== 'text' ? (
              <label className="field"><span>Caption {kind === 'voice' ? '(shown as captions for everyone)' : ''}</span>
                <input className="input" maxLength={500} value={caption} onChange={(e) => setCaption(e.target.value)} placeholder="A little piece of my day." /></label>
            ) : null}
            <div className="row wrap" style={{ gap: 16 }}>
              {tab === 'upload' ? (
                <label className="field" style={{ minWidth: 200 }}><span>Where it came from</span>
                  <select className="input" value={device} onChange={(e) => setDevice(e.target.value as SourceDevice)}>
                    {DEVICES.map((d) => <option key={d} value={d}>{deviceLabel(d)}</option>)}
                  </select></label>
              ) : null}
              <label className="field" style={{ minWidth: 220 }}><span>When it happened ({me.timeZone.replace(/_/g, ' ')})</span>
                <input className="input" type="datetime-local" value={captureLocal} onChange={(e) => setCaptureLocal(e.target.value)} /></label>
            </div>
          </div>
        </section>

        <aside className="card stack" aria-label="Who can see this?">
          <h2 className="section-title" style={{ margin: 0 }}>Who can see this?</h2>
          <div className="option-cards">
            <button className="option-card" aria-pressed={mode === 'one'} onClick={() => setMode('one')}><Icon name="user" /><span>One person · 1:1</span></button>
            <button className="option-card" aria-pressed={mode === 'circle'} onClick={() => setMode('circle')}><Icon name="users" /><span>Private circle · 1:many</span></button>
          </div>
          {candidates.length ? (
            <>
              <label className="field"><span className="sr-only">Room</span>
                <select className="input" value={room?.id} onChange={(e) => setRoomId(e.target.value)}>
                  {candidates.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                </select></label>
              <div className="muted small">{others.length} {others.length === 1 ? 'recipient' : 'recipients'}</div>
              <div>
                {others.map((m) => (
                  <div key={m.id} className="recipient">
                    <Avatar def={m.avatar?.definition} size={44} name={m.displayName} />
                    <span className="grow" style={{ fontSize: '1.1rem' }}>{m.displayName}</span>
                    <Checkbox label={`Share with ${m.displayName}`} checked={recipients.includes(m.id)}
                      onChange={(v) => setRecipients((list) => (mode === 'one' ? (v ? [m.id] : []) : v ? [...list, m.id] : list.filter((x) => x !== m.id)))} />
                  </div>
                ))}
              </div>
            </>
          ) : <Notice kind="warn">You don't have a {mode === 'one' ? 'direct room' : 'circle'} yet. Create one in Settings.</Notice>}
          <div className="row small muted"><Icon name="lock" />Only selected people can view this moment.</div>
          <div className="row" style={{ justifyContent: 'space-between' }}>
            <span className="row"><Icon name="sparkle" />Include in AI recap</span>
            <Toggle checked={aiAllowed} onChange={setAiAllowed} label="Include in AI recap" />
          </div>
          <div className="row"><Checkbox checked onChange={() => undefined} label="Review recap before sharing (always on)" /><span>Review recap before sharing <span className="muted small">(always on)</span></span></div>
          {error ? <Notice kind="error">{error}</Notice> : null}
          {busy ? <Notice>{busy}</Notice> : null}
          <button className="btn btn-primary btn-lg" disabled={!ready || !room || !!busy || !recipients.length} onClick={() => void startShare()}><Icon name="send" />{audienceText}</button>
          <button className="btn btn-outline btn-lg" disabled={!ready || !room || !!busy} onClick={() => void saveOnly()}><Icon name="bookmark" />Save privately</button>
        </aside>
      </div>

      <section className="card" style={{ marginTop: 24 }}>
        <h2 className="section-title">Your recent moments</h2>
        {mine.length ? (
          <div className="my-moments">
            {mine.map((m) => (
              <div key={m.id} className="card-inner stack" style={{ gap: 8 }}>
                <div className="row"><strong className="grow">{kindLabel(m.kind, m.media?.sourceDevice)}</strong>
                  <span className={`chip label${m.sharingState === 'shared' ? ' green' : ''}`}>{m.sharingState === 'shared' ? 'Shared' : m.sharingState === 'withdrawn' ? 'Withdrawn' : 'Private'}</span></div>
                {m.media ? <MomentMedia moment={m} /> : <p style={{ margin: 0 }}>{m.caption}</p>}
                <span className="small muted">{m.audience.length ? `Seen by ${m.audience.map((a) => a.displayName).join(', ')}` : 'Only you'} · {localWhen(m)}</span>
                {m.sharingState === 'shared' ? <button className="btn btn-danger" onClick={() => void withdraw(m)}>Withdraw</button> : null}
              </div>
            ))}
          </div>
        ) : <p className="muted">Nothing yet. What you save stays private until you share it.</p>}
      </section>

      {confirm ? (
        <div className="overlay" role="dialog" aria-modal="true" aria-label="Confirm recipients">
          <div className="card stack" style={{ width: 'min(520px, 100%)' }}>
            <h2 className="section-title" style={{ margin: 0 }}>Confirm who can see this</h2>
            <div className="card-inner" style={{ fontSize: '1.4rem', fontFamily: 'var(--display)', fontWeight: 650 }}>{confirm.resolution.label}</div>
            <div className="row wrap">{confirm.resolution.recipients.map((r) => <span key={r.userId} className="chip">{r.displayName}</span>)}</div>
            <p className="muted small" style={{ margin: 0 }}>
              {aiAllowed ? 'You allowed AI to help draft a recap; you will review it before anyone sees it.' : 'AI won’t use this moment.'} Changing who sees it later means a new, separate share.
            </p>
            <div className="row wrap">
              <button className="btn btn-primary" disabled={!!busy} onClick={() => void confirmShare()}><Icon name="check" />Share now</button>
              <button className="btn btn-outline" onClick={() => { setConfirm(null); toast('Not shared. It’s saved privately.'); void loadMine(); reset(); }}>Keep it private</button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}

function useMediaStream(constraints: MediaStreamConstraints, onError: (m: string) => void) {
  const [stream, setStream] = useState<MediaStream | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const start = async () => {
    try {
      const s = await navigator.mediaDevices.getUserMedia(constraints);
      streamRef.current = s;
      setStream(s);
    } catch (e) {
      const name = (e as DOMException).name;
      onError(name === 'NotAllowedError' ? 'Permission was blocked. Allow the camera or microphone for this site in your browser settings, or upload a file instead.'
        : name === 'NotFoundError' ? 'No camera or microphone was found on this device. You can upload a file instead.'
        : 'The camera or microphone could not start. You can upload a file instead.');
    }
  };
  const stop = () => {
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    setStream(null);
  };
  // Always release the camera/microphone when leaving this screen.
  useEffect(() => () => streamRef.current?.getTracks().forEach((t) => t.stop()), []);
  return { stream, start, stop };
}

function useRecorder(stream: MediaStream | null, maxMs: number, mime: string[], onDone: (blob: Blob, ms: number) => void) {
  const [recording, setRecording] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const rec = useRef<MediaRecorder | null>(null);
  const supported = useMemo(() => mime.find((m) => typeof MediaRecorder !== 'undefined' && MediaRecorder.isTypeSupported(m)), [mime]);
  const start = () => {
    if (!stream) return;
    const chunks: BlobPart[] = [];
    const r = new MediaRecorder(stream, supported ? { mimeType: supported } : undefined);
    const began = Date.now();
    r.ondataavailable = (e) => e.data.size && chunks.push(e.data);
    r.onstop = () => onDone(new Blob(chunks, { type: r.mimeType }), Date.now() - began);
    r.start(250);
    rec.current = r;
    setRecording(true);
    setElapsed(0);
  };
  const stop = () => {
    rec.current?.state === 'recording' && rec.current.stop();
    setRecording(false);
  };
  useEffect(() => {
    if (!recording) return;
    const t = window.setInterval(() => setElapsed((e) => { if (e + 200 >= maxMs) stop(); return e + 200; }), 200);
    return () => window.clearInterval(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recording]);
  return { recording, elapsed, start, stop };
}

function CameraCapture({ onDone, onError }: { onDone: (d: Draft) => void; onError: (m: string) => void }) {
  const { stream, start, stop } = useMediaStream({ video: { facingMode: 'environment' }, audio: true }, onError);
  const video = useRef<HTMLVideoElement>(null);
  useEffect(() => { if (video.current && stream) video.current.srcObject = stream; }, [stream]);
  const recorder = useRecorder(stream, 15_000, ['video/mp4', 'video/webm;codecs=vp9,opus', 'video/webm'], (blob, ms) => {
    stop();
    onDone({ blob, name: blob.type.includes('mp4') ? 'camera.mp4' : 'camera.webm', kind: 'clip', url: URL.createObjectURL(blob), durationMs: ms });
  });
  const snap = () => {
    const v = video.current;
    if (!v) return;
    const c = document.createElement('canvas');
    c.width = v.videoWidth;
    c.height = v.videoHeight;
    c.getContext('2d')!.drawImage(v, 0, 0);
    c.toBlob((b) => { if (b) { stop(); onDone({ blob: b, name: 'camera.jpg', kind: 'photo', url: URL.createObjectURL(b) }); } }, 'image/jpeg', 0.9);
  };
  if (!stream) {
    return (
      <div className="dropzone">
        <span className="stack" style={{ alignItems: 'center' }}>
          <strong>Use this device's camera</strong>
          <span className="muted">SameSky asks for permission only when you press the button, and stops the camera as soon as you're done.</span>
          <button className="btn btn-primary" onClick={() => void start()}><Icon name="camera" />Start camera</button>
        </span>
      </div>
    );
  }
  return (
    <div className="stack">
      <div className="preview"><video ref={video} autoPlay muted playsInline /></div>
      <div className="row wrap">
        <span className="chip label warn">● Camera on</span>
        <button className="btn btn-primary" onClick={snap}><Icon name="camera" />Take photo</button>
        {recorder.recording
          ? <button className="btn btn-danger" onClick={recorder.stop}>Stop ({(recorder.elapsed / 1000).toFixed(0)}s / 15s)</button>
          : <button className="btn btn-outline" onClick={recorder.start}><Icon name="video" />Record clip</button>}
        <button className="btn btn-quiet" onClick={stop}>Turn camera off</button>
      </div>
    </div>
  );
}

function VoiceCapture({ onDone, onError }: { onDone: (d: Draft) => void; onError: (m: string) => void }) {
  const { stream, start, stop } = useMediaStream({ audio: true }, onError);
  const recorder = useRecorder(stream, 30_000, ['audio/mp4', 'audio/webm;codecs=opus', 'audio/webm', 'audio/ogg'], (blob, ms) => {
    stop();
    onDone({ blob, name: blob.type.includes('mp4') ? 'voice.m4a' : 'voice.webm', kind: 'voice', url: URL.createObjectURL(blob), durationMs: ms });
  });
  useEffect(() => {
    if (stream && !recorder.recording) recorder.start();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stream]);
  return (
    <div className="dropzone">
      <span className="stack" style={{ alignItems: 'center' }}>
        {recorder.recording ? (
          <>
            <span className="chip label warn">● Recording {(recorder.elapsed / 1000).toFixed(0)}s / 30s</span>
            <button className="btn btn-primary" onClick={recorder.stop}>Stop</button>
          </>
        ) : (
          <>
            <strong>Record a voice note (up to 30 seconds)</strong>
            <span className="muted">Add a caption so everyone can read it too.</span>
            <button className="btn btn-primary" onClick={() => void start()}><Icon name="mic" />Start recording</button>
          </>
        )}
      </span>
    </div>
  );
}
