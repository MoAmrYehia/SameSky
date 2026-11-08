// "Your avatar": create a 3D avatar from your own photos (owner-only), review it, approve it, share it, or
// remove it — with an honest demo-avatar alternative. Every step is owned by the person whose likeness it is.
//
// Stages: explain + consent -> photo (file or camera) -> options -> generation status -> likeness review ->
// "This looks like me — use this avatar". Generation runs on the server through the configured provider;
// without provider credentials the UI says so and offers manual import of a model made elsewhere.

import { useCallback, useEffect, useRef, useState } from 'react';
import { AVATAR_OPTIONS, DEFAULT_AVATAR } from '../../shared/avatar';
import type { AvatarAssetInfo, AvatarDefinition, AvatarJobInfo, AvatarScope, AvatarSourceInfo, AvatarStudio, AvatarView } from '../../shared/contracts';
import { useApp } from '../App';
import { Avatar, Icon, Notice } from '../ui';

const portrait = () => import('../../shared/portrait');

type Step = 'overview' | 'consent' | 'photo' | 'options' | 'job' | 'review' | 'import';

const SCOPES: { value: AvatarScope; label: string; help: string }[] = [
  { value: 'rooms', label: 'People in my rooms', help: 'Everyone who shares a room with you, while they do.' },
  { value: 'direct', label: 'Only my one-to-one rooms', help: 'Not shown in circles.' },
  { value: 'private', label: 'Only me (for now)', help: 'Others keep seeing your demo avatar.' },
];

const JOB_TEXT: Record<string, string> = {
  validated: 'Getting ready…',
  queued: 'Waiting for the avatar service…',
  processing: 'Building your 3D avatar…',
  validating_asset: 'Checking the model before you see it…',
  awaiting_owner_approval: 'Ready for your review.',
  completed: 'In use.',
  failed: 'This didn’t work.',
  cancelled: 'Stopped.',
  superseded: 'Replaced.',
};

export function assetView(asset: AvatarAssetInfo): AvatarView {
  return { kind: 'photo_derived', definition: DEFAULT_AVATAR, version: asset.version, assetId: asset.id, assetVersion: asset.version, modelUrl: asset.modelUrl ?? undefined, rig: asset.rig };
}

function fmtDate(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' }) : '';
}

function ScopePicker({ value, onChange }: { value: AvatarScope; onChange: (v: AvatarScope) => void }) {
  return (
    <fieldset className="scope-picker">
      <legend>Who can see your approved avatar</legend>
      {SCOPES.map((s) => (
        <label key={s.value} className="scope-option">
          <input type="radio" name="avatar-scope" value={s.value} checked={value === s.value} onChange={() => onChange(s.value)} />
          <span><strong>{s.label}</strong><span className="muted small">{s.help}</span></span>
        </label>
      ))}
    </fieldset>
  );
}

export function AvatarStudioCard({ startAt }: { startAt?: Step }) {
  const { ds, me, refresh, toast } = useApp();
  const [studio, setStudio] = useState<AvatarStudio | null>(null);
  const [step, setStep] = useState<Step>(startAt ?? 'overview');
  const [error, setError] = useState<string | null>(null);
  const [scope, setScope] = useState<AvatarScope>('rooms');
  const [consentId, setConsentId] = useState<string | null>(null);
  const [agreed, setAgreed] = useState(false);
  const [source, setSource] = useState<AvatarSourceInfo | null>(null);
  const [body, setBody] = useState<string | null>(null);
  const [removeGlasses, setRemoveGlasses] = useState(true);
  const [job, setJob] = useState<AvatarJobInfo | null>(null);
  const [review, setReview] = useState<AvatarAssetInfo | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const s = await ds.avatarStudio();
      setStudio(s);
      const pendingAsset = s.assets.find((a) => a.state === 'awaiting_owner_approval');
      const running = s.jobs.find((j) => ['validated', 'queued', 'processing', 'validating_asset'].includes(j.state));
      return { s, pendingAsset, running };
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load your avatar settings.');
      return null;
    }
  }, [ds]);

  useEffect(() => { void load(); }, [load]);

  // arriving from the onboarding prompt (#/settings?avatar=create): bring the card into view once, then drop
  // the query so a reload doesn't restart the flow
  const card = useRef<HTMLElement>(null);
  const loaded = studio !== null;
  useEffect(() => {
    if (!startAt || !loaded) return;
    card.current?.scrollIntoView({ block: 'start' });
    try { history.replaceState(history.state, '', '#/settings'); } catch { /* ignore */ }
  }, [startAt, loaded]);

  // poll the active job while it runs (the server polls the provider; this only reads our own state)
  useEffect(() => {
    if (step !== 'job' || !job || !['validated', 'queued', 'processing', 'validating_asset'].includes(job.state)) return;
    const t = window.setInterval(async () => {
      try {
        const next = await ds.avatarJob(job.id);
        setJob(next);
        if (next.state === 'awaiting_owner_approval' && next.assetId) {
          const r = await load();
          const asset = r?.s.assets.find((a) => a.id === next.assetId);
          if (asset) {
            setReview(asset);
            setStep('review');
          }
        }
      } catch {
        /* keep polling; a transient error isn't a job failure */
      }
    }, 3000);
    return () => window.clearInterval(t);
  }, [ds, job, step, load]);

  const after = async (message: string) => {
    (await portrait()).purgeAvatarCaches();
    await refresh();
    await load();
    toast(message);
  };

  if (!studio) {
    return (
      <section className="card stack avatar-studio" aria-busy="true">
        <h2 className="section-title" style={{ margin: 0 }}>Your avatar</h2>
        {error ? <Notice kind="error">{error}</Notice> : <span className="muted">Loading…</span>}
      </section>
    );
  }

  const active = studio.active?.kind === 'photo_derived' ? studio.active : null;
  const activeAsset = studio.assets.find((a) => a.id === studio.activeAssetId) ?? null;
  const pending = studio.assets.find((a) => a.state === 'awaiting_owner_approval') ?? null;
  const running = studio.jobs.find((j) => ['validated', 'queued', 'processing', 'validating_asset'].includes(j.state)) ?? null;
  const previous = studio.assets.filter((a) => a.state === 'superseded' && a.approvedAt);
  const provider = studio.provider;

  const begin = () => {
    setError(null);
    setAgreed(false);
    setSource(null);
    setBody(null);
    setStep('consent');
  };

  const giveConsent = async () => {
    setError(null);
    setBusy(true);
    try {
      const c = await ds.avatarConsent('metaperson', true, scope);
      setConsentId(c.id);
      setStep('photo');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not record your choice.');
    } finally {
      setBusy(false);
    }
  };

  const generate = async () => {
    if (!consentId || !source || !body) return;
    setError(null);
    setBusy(true);
    try {
      const j = await ds.startAvatarJob([source.id], consentId, { body, remove_glasses: removeGlasses }, `${source.id}:${body}:${removeGlasses ? 1 : 0}`);
      setJob(j);
      setStep('job');
      void load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not start.');
    } finally {
      setBusy(false);
    }
  };

  const approve = async (asset: AvatarAssetInfo) => {
    setBusy(true);
    try {
      await ds.approveAvatar(asset.id, scope);
      setReview(null);
      setStep('overview');
      await after('Your avatar is in use. People you chose will see it at the table and on their screens.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not approve.');
    } finally {
      setBusy(false);
    }
  };

  const discard = async (asset: AvatarAssetInfo) => {
    setBusy(true);
    try {
      await ds.rejectAvatar(asset.id);
      setReview(null);
      setStep('overview');
      await after('Discarded. Your current avatar is unchanged.');
    } finally {
      setBusy(false);
    }
  };

  const del = async (asset: AvatarAssetInfo) => {
    if (!window.confirm('Delete this avatar? People stop seeing it right away and your demo avatar is shown instead. We also ask the avatar service to delete its copy. Copies someone already downloaded can’t be recalled.')) return;
    const res = await ds.deleteAvatar(asset.id);
    await after(res.state === 'deleted' ? 'Avatar deleted.' : 'Deletion requested.');
  };

  const deleteAll = async () => {
    if (!window.confirm('Delete all your avatar models and source photos? This can’t be undone. Your demo avatar is shown instead.')) return;
    const res = await ds.deleteAvatarData();
    await after(`Deleted ${res.assets} model${res.assets === 1 ? '' : 's'} and ${res.sources} photo${res.sources === 1 ? '' : 's'}.`);
  };

  // ------------------------------------------------------------------ render

  return (
    <section ref={card} className="card stack avatar-studio" aria-labelledby="avatar-studio-title">
      <h2 id="avatar-studio-title" className="section-title" style={{ margin: 0 }}>Your avatar</h2>

      {step === 'overview' ? (
        <>
          <div className="studio-current">
            <Avatar avatar={me.avatar} size={132} name={me.displayName} demoBadge />
            <div className="stack" style={{ gap: 6 }}>
              {active && activeAsset ? (
                <>
                  <strong>Made from your photos</strong>
                  <span className="muted small">Version {activeAsset.version} · approved {fmtDate(activeAsset.approvedAt)} · {activeAsset.provider === 'manual_import' ? 'imported model' : provider.label}</span>
                  <span className="small">Seen by: <strong>{SCOPES.find((s) => s.value === activeAsset.visibility)?.label}</strong></span>
                </>
              ) : (
                <>
                  <strong>Demo avatar</strong>
                  <span className="muted small">{studio.sample ? 'The sample’s people are fictional and use labelled demo avatars.' : 'A labelled stand-in, not your likeness. Create one from your own photos so people recognise you.'}</span>
                </>
              )}
            </div>
          </div>
          {studio.sample ? (
            <Notice>Sign in to create your avatar from your own photos. Nothing in the sample uses real people’s faces.</Notice>
          ) : null}
          {running ? (
            <div className="studio-status" role="status">
              <span className="spinner" aria-hidden="true" />
              <span className="grow">{JOB_TEXT[running.state]}{running.state === 'processing' && running.progress ? ` ${running.progress}%` : ''}</span>
              <button className="btn btn-quiet btn-sm" onClick={() => { setJob(running); setStep('job'); }}>Details</button>
            </div>
          ) : null}
          {pending ? (
            <div className="studio-status ready">
              <Icon name="checkCircle" />
              <span className="grow">A new avatar is ready for your review. Your current one stays until you approve it.</span>
              <button className="btn btn-primary btn-sm" onClick={() => { setReview(pending); setStep('review'); }}>Review</button>
            </div>
          ) : null}
          <div className="row wrap">
            <button className="btn btn-primary" disabled={!!studio.sample || !!running} onClick={begin}><Icon name="camera" />Create from my photos</button>
            <button className="btn btn-outline" disabled={!!studio.sample} onClick={() => { setError(null); setAgreed(false); setStep('import'); }}><Icon name="upload" />Import a model</button>
            {active ? <button className="btn btn-quiet" onClick={() => void ds.useDemoAvatar().then(() => after('Using your demo avatar. Your photo avatar is kept; you can use it again.'))}>Use a demo avatar</button> : null}
          </div>
          {previous.length ? (
            <details className="studio-more">
              <summary>Earlier approved versions ({previous.length})</summary>
              {previous.map((a) => (
                <div key={a.id} className="row" style={{ justifyContent: 'space-between' }}>
                  <span className="small">Version {a.version} · approved {fmtDate(a.approvedAt)}</span>
                  <span className="row" style={{ gap: 6 }}>
                    <button className="btn btn-quiet btn-sm" onClick={() => void ds.activateAvatar(a.id).then(() => after('Using that version again.'))}>Use again</button>
                    <button className="btn btn-quiet btn-sm" onClick={() => void del(a)}>Delete</button>
                  </span>
                </div>
              ))}
            </details>
          ) : null}
          {activeAsset ? (
            <details className="studio-more">
              <summary>Sharing and removal</summary>
              <label className="field"><span>Who can see it</span>
                <select className="input" value={activeAsset.visibility} onChange={(e) => void ds.setAvatarVisibility(activeAsset.id, e.target.value as AvatarScope).then(() => after(e.target.value === 'private' ? 'Stopped sharing. Others now see your demo avatar.' : 'Sharing updated.'))}>
                  {SCOPES.map((s) => <option key={s.value} value={s.value}>{s.value === 'private' ? 'Only me (stop sharing)' : s.label}</option>)}
                </select></label>
              <div className="row wrap">
                <button className="btn btn-danger btn-sm" onClick={() => void del(activeAsset)}>Delete this avatar</button>
                {studio.sources.length ? <button className="btn btn-quiet btn-sm" onClick={() => void Promise.all(studio.sources.map((s) => ds.deleteAvatarSource(s.id))).then(() => after('Your source photos are deleted. Your avatar is unaffected.'))}>Delete my source photos ({studio.sources.length})</button> : null}
                <button className="btn btn-quiet btn-sm" onClick={() => void deleteAll()}>Delete all avatar data</button>
              </div>
            </details>
          ) : studio.sources.length || studio.assets.length ? (
            <details className="studio-more">
              <summary>Your photos and models</summary>
              <p className="muted small" style={{ margin: 0 }}>Source photos are deleted automatically {provider.retentionDays} days after upload.</p>
              <button className="btn btn-quiet btn-sm" onClick={() => void deleteAll()}>Delete all avatar data</button>
            </details>
          ) : null}
          <DemoEditor />
        </>
      ) : null}

      {step === 'consent' ? (
        <div className="studio-step stack">
          <StepHeader n={1} title="Before you start" onBack={() => setStep('overview')} />
          <ul className="studio-facts">
            <li><strong>What we make:</strong> a 3D avatar of you (head, hair, body and an outfit) that sits at the table and appears in portraits.</li>
            <li><strong>Who processes your photo:</strong> {provider.label}, a third-party service SameSky’s server sends it to. It is not Meta’s avatar system.</li>
            <li><strong>Your photo stays private:</strong> never shared as a moment, recap or notification. We delete it {provider.retentionDays} days after upload, or sooner if you ask.</li>
            <li><strong>You decide:</strong> only you see the result until you approve it. You can stop sharing or delete it any time; deleting also asks the service to delete its copy.</li>
            <li><strong>One photo has limits:</strong> parts the camera didn’t see are estimated. Approve only if it looks like you.</li>
          </ul>
          <ScopePicker value={scope} onChange={setScope} />
          {!provider.configured ? (
            <Notice kind="warn">Photo-to-avatar generation isn’t set up on this server yet (no {provider.label} credentials). You can import a model you made elsewhere, or keep the demo avatar.</Notice>
          ) : null}
          <label className="row consent-check">
            <input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} />
            <span>I agree that {provider.label} processes my photo to create my avatar.</span>
          </label>
          {error ? <Notice kind="error">{error}</Notice> : null}
          <div className="row wrap">
            <button className="btn btn-primary" disabled={!agreed || !provider.configured || busy} onClick={() => void giveConsent()}>Continue</button>
            <button className="btn btn-quiet" onClick={() => setStep('import')}>Import a model instead</button>
          </div>
        </div>
      ) : null}

      {step === 'photo' && consentId ? (
        <PhotoStep consentId={consentId} source={source} onBack={() => setStep('consent')} onDone={(s) => { setSource(s); setStep('options'); }} />
      ) : null}

      {step === 'options' && source ? (
        <div className="studio-step stack">
          <StepHeader n={3} title="A few choices a photo can’t make" onBack={() => setStep('photo')} />
          <fieldset className="scope-picker">
            <legend>Body model</legend>
            <p className="muted small" style={{ margin: '0 0 6px' }}>{provider.label} offers two base body models. This only picks the base body shape and outfit set; we never infer anything about you from your photo.</p>
            {provider.bodies.map((b) => (
              <label key={b} className="scope-option">
                <input type="radio" name="avatar-body" value={b} checked={body === b} onChange={() => setBody(b)} />
                <span><strong>{b === 'female' ? 'Female body model' : b === 'male' ? 'Male body model' : b}</strong></span>
              </label>
            ))}
          </fieldset>
          <label className="row consent-check">
            <input type="checkbox" checked={removeGlasses} onChange={(e) => setRemoveGlasses(e.target.checked)} />
            <span>Remove glasses from the face (the service recommends it)</span>
          </label>
          <p className="muted small" style={{ margin: 0 }}>The service’s default outfit for this body model is used. You can generate again with different choices.</p>
          {error ? <Notice kind="error">{error}</Notice> : null}
          <button className="btn btn-primary" style={{ alignSelf: 'start' }} disabled={!body || busy} onClick={() => void generate()}><Icon name="sparkle" />Create my avatar</button>
        </div>
      ) : null}

      {step === 'job' && job ? (
        <div className="studio-step stack">
          <StepHeader n={4} title="Creating your avatar" onBack={() => setStep('overview')} backLabel="Overview" />
          <div className="studio-status" role="status" aria-live="polite">
            {['validated', 'queued', 'processing', 'validating_asset'].includes(job.state) ? <span className="spinner" aria-hidden="true" /> : <Icon name={job.state === 'failed' ? 'x' : 'checkCircle'} />}
            <span className="grow">{JOB_TEXT[job.state]}{job.state === 'processing' && job.progress ? ` ${job.progress}%` : ''}</span>
          </div>
          {job.state === 'processing' ? <div className="meter"><i style={{ width: `${Math.max(4, job.progress)}%` }} /></div> : null}
          {job.safeError && job.state !== 'processing' ? <Notice kind={job.state === 'failed' ? 'error' : undefined}>{job.safeError}</Notice> : null}
          <p className="muted small" style={{ margin: 0 }}>You can leave this page; we keep working and tell you here when it’s ready. Your current avatar stays in use until you approve a new one.</p>
          <div className="row wrap">
            {['validated', 'queued', 'processing', 'validating_asset'].includes(job.state) ? <button className="btn btn-outline" onClick={() => void ds.cancelAvatarJob(job.id).then((j) => { setJob(j); void load(); })}>Cancel</button> : null}
            {job.state === 'failed' || job.state === 'cancelled' ? (
              <>
                <button className="btn btn-primary" onClick={() => void ds.retryAvatarJob(job.id).then((j) => { setJob(j); void load(); }).catch((e: Error) => setError(e.message))}>Try again</button>
                <button className="btn btn-outline" onClick={() => setStep('photo')}>Use a different photo</button>
              </>
            ) : null}
          </div>
          {error ? <Notice kind="error">{error}</Notice> : null}
        </div>
      ) : null}

      {step === 'review' && review ? (
        <ReviewStep
          asset={review}
          source={studio.sources.find((s) => s.id === studio.jobs.find((j) => j.id === review.jobId)?.sourceIds[0]) ?? null}
          scope={scope}
          setScope={setScope}
          busy={busy}
          onApprove={() => void approve(review)}
          onDiscard={() => void discard(review)}
          onRetake={() => { setStep(consentId ? 'photo' : 'consent'); }}
          onBack={() => setStep('overview')}
          error={error}
        />
      ) : null}

      {step === 'import' ? (
        <ImportStep scope={scope} setScope={setScope} onBack={() => setStep('overview')} onImported={async (asset) => { await load(); setReview(asset); setStep('review'); }} />
      ) : null}
    </section>
  );
}

function StepHeader({ n, title, onBack, backLabel = 'Back' }: { n: number; title: string; onBack: () => void; backLabel?: string }) {
  return (
    <div className="row" style={{ justifyContent: 'space-between' }}>
      <strong className="studio-step-title"><span className="step-dot" aria-hidden="true">{n}</span>{title}</strong>
      <button className="btn btn-quiet btn-sm" onClick={onBack}>{backLabel}</button>
    </div>
  );
}

/** A front-facing photo from a file or the camera. Camera permission is separate and the camera always stops. */
function PhotoStep({ consentId, source, onBack, onDone }: { consentId: string; source: AvatarSourceInfo | null; onBack: () => void; onDone: (s: AvatarSourceInfo) => void }) {
  const { ds } = useApp();
  const [file, setFile] = useState<{ blob: Blob; name: string; url: string } | null>(null);
  const [camera, setCamera] = useState<MediaStream | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const video = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const stopCamera = () => {
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    setCamera(null);
  };
  useEffect(() => () => { streamRef.current?.getTracks().forEach((t) => t.stop()); }, []);
  useEffect(() => { if (video.current && camera) video.current.srcObject = camera; }, [camera]);
  useEffect(() => () => { if (file) URL.revokeObjectURL(file.url); }, [file]);

  const startCamera = async () => {
    setError(null);
    try {
      const s = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: { ideal: 1600 }, height: { ideal: 1600 } }, audio: false });
      streamRef.current = s;
      setCamera(s);
    } catch (e) {
      const name = (e as DOMException).name;
      setError(name === 'NotAllowedError' ? 'Camera permission was blocked. Allow it in your browser settings, or choose a photo file instead.' : 'The camera couldn’t start. Choose a photo file instead.');
    }
  };
  const snap = () => {
    const v = video.current;
    if (!v) return;
    const c = document.createElement('canvas');
    c.width = v.videoWidth;
    c.height = v.videoHeight;
    c.getContext('2d')!.drawImage(v, 0, 0);
    c.toBlob((b) => { if (b) { setFile({ blob: b, name: 'camera.jpg', url: URL.createObjectURL(b) }); stopCamera(); } }, 'image/jpeg', 0.95);
  };
  const upload = async () => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const s = await ds.uploadAvatarSource(file.blob, file.name, 'front', consentId);
      onDone(s);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That photo couldn’t be used.');
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="studio-step stack">
      <StepHeader n={2} title="A front-facing photo" onBack={() => { stopCamera(); onBack(); }} />
      <ul className="studio-tips">
        <li>Sharp and evenly lit: no strong shadows or glare</li>
        <li>Looking straight at the camera, neutral expression, mouth closed</li>
        <li>Hair off your forehead and the sides of your face; glasses off if you can</li>
        <li>Just you in the photo. JPEG or PNG, at least 512 pixels on the short side</li>
      </ul>
      <div className="photo-stage">
        {camera ? <video ref={video} autoPlay muted playsInline aria-label="Camera preview" /> : file ? <img src={file.url} alt="The photo you chose (private)" /> : source?.imageUrl ? <img src={source.imageUrl} alt="Your uploaded photo (private)" /> : <span className="muted">Your photo appears here, only for you.</span>}
        {camera ? <span className="chip label warn photo-live">● Camera on</span> : null}
      </div>
      <div className="row wrap">
        {camera ? (
          <>
            <button className="btn btn-primary" onClick={snap}><Icon name="camera" />Take photo</button>
            <button className="btn btn-quiet" onClick={stopCamera}>Turn camera off</button>
          </>
        ) : (
          <>
            <label className="btn btn-outline">
              <Icon name="image" />{file ? 'Choose another photo' : 'Choose a photo'}
              <input type="file" accept="image/jpeg,image/png" className="sr-only" onChange={(e) => { const f = e.target.files?.[0]; if (f) setFile({ blob: f, name: f.name, url: URL.createObjectURL(f) }); e.target.value = ''; }} />
            </label>
            <button className="btn btn-quiet" onClick={() => void startCamera()}><Icon name="camera" />Use my camera</button>
            {file ? <button className="btn btn-primary" disabled={busy} onClick={() => void upload()}>{busy ? 'Checking…' : 'Use this photo'}</button> : null}
            {!file && source ? <button className="btn btn-primary" onClick={() => onDone(source)}>Keep this photo</button> : null}
          </>
        )}
      </div>
      {error ? <Notice kind="error">{error} You can retake it.</Notice> : null}
    </div>
  );
}

/** Likeness review: neutral-light views beside the private source photo, and a model you can turn. */
function ReviewStep({ asset, source, scope, setScope, busy, onApprove, onDiscard, onRetake, onBack, error }: {
  asset: AvatarAssetInfo; source: AvatarSourceInfo | null; scope: AvatarScope; setScope: (s: AvatarScope) => void; busy: boolean;
  onApprove: () => void; onDiscard: () => void; onRetake: () => void; onBack: () => void; error: string | null;
}) {
  const [views, setViews] = useState<{ front: string; threeQuarter: string; side: string; seated: string } | null>(null);
  const [failed, setFailed] = useState(false);
  const canvas = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    let alive = true;
    let dispose: (() => void) | null = null;
    portrait()
      .then(async (m) => {
        const v = await m.renderReviewViews(assetView(asset), 360);
        if (alive) setViews(v);
        if (alive && canvas.current) dispose = await m.mountAvatarViewer(canvas.current, assetView(asset));
        if (!alive) dispose?.();
      })
      .catch(() => alive && setFailed(true));
    return () => {
      alive = false;
      dispose?.();
    };
  }, [asset]);
  const rig = asset.rig;
  return (
    <div className="studio-step stack">
      <StepHeader n={5} title="Does this look like you?" onBack={onBack} backLabel="Later" />
      <p className="muted small" style={{ margin: 0 }}>Neutral light, no beautification. Only you can see this until you approve it.</p>
      {failed ? <Notice kind="error">This model couldn’t be displayed. Discard it and try again.</Notice> : null}
      <div className="review-grid">
        <figure className="review-photo">
          {source?.imageUrl ? <img src={source.imageUrl} alt="Your source photo (private)" /> : <span className="review-placeholder muted small">{asset.provider === 'manual_import' ? 'Imported model: no photo is stored here. Compare it with a mirror or a recent photo of yourself.' : 'Source photo deleted'}</span>}
          <figcaption>{source?.imageUrl ? 'Your photo · private' : asset.provider === 'manual_import' ? 'Imported model' : 'Your photo'}</figcaption>
        </figure>
        {(['front', 'threeQuarter', 'side', 'seated'] as const).map((k) => (
          <figure key={k} className="review-view">
            {views ? <img src={views[k]} alt={`Your avatar, ${k === 'threeQuarter' ? 'three-quarter' : k} view`} /> : <span className="avatar-loading" />}
            <figcaption>{k === 'threeQuarter' ? 'Three-quarter' : k[0].toUpperCase() + k.slice(1)}</figcaption>
          </figure>
        ))}
      </div>
      <div className="review-turn">
        <canvas ref={canvas} tabIndex={0} aria-label="Your avatar. Drag or use the arrow keys to turn it; use + and - to zoom." />
        <div className="stack small muted" style={{ gap: 4 }}>
          <span>Drag to turn · arrow keys · + / − to zoom</span>
          <span>{rig.triangles?.toLocaleString()} triangles · {rig.blink ? 'blinks' : 'static face'} · {asset.provider === 'manual_import' ? 'imported model' : 'generated'}</span>
          <span>Expressions aren’t tracked from your face; blinks are animation.</span>
        </div>
      </div>
      <ScopePicker value={scope} onChange={setScope} />
      {error ? <Notice kind="error">{error}</Notice> : null}
      <div className="row wrap review-actions">
        <button className="btn btn-primary btn-lg" disabled={busy || failed} onClick={onApprove}><Icon name="check" />This looks like me — use this avatar</button>
        <button className="btn btn-outline" onClick={onRetake}>Try another photo</button>
        <button className="btn btn-quiet" onClick={onDiscard}>Discard</button>
      </div>
    </div>
  );
}

/** Import a GLB made elsewhere from the person's own photos (validated on the server). */
function ImportStep({ scope, setScope, onBack, onImported }: { scope: AvatarScope; setScope: (s: AvatarScope) => void; onBack: () => void; onImported: (a: AvatarAssetInfo) => void }) {
  const { ds } = useApp();
  const [attested, setAttested] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const pick = async (f: File | undefined) => {
    if (!f) return;
    setBusy(true);
    setError(null);
    try {
      const consent = await ds.avatarConsent('manual_import', true, scope);
      const asset = await ds.importAvatarModel(f, f.name, consent.id);
      onImported(asset);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That model couldn’t be imported.');
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="studio-step stack">
      <StepHeader n={1} title="Import a model made from your photos" onBack={onBack} />
      <p className="muted small" style={{ margin: 0 }}>If you made an avatar of yourself with an avatar service’s own app, import its rigged, self-contained GLB here. We check it (size, skeleton, no external files) before you review it; nothing is shown to anyone until you approve it.</p>
      <ScopePicker value={scope} onChange={setScope} />
      <label className="row consent-check">
        <input type="checkbox" checked={attested} onChange={(e) => setAttested(e.target.checked)} />
        <span>This model was made from photos of me, with my consent.</span>
      </label>
      <label className={`btn btn-primary${!attested || busy ? ' disabled' : ''}`} aria-disabled={!attested || busy} style={{ alignSelf: 'start' }}>
        <Icon name="upload" />{busy ? 'Checking the model…' : 'Choose a .glb file'}
        <input type="file" accept=".glb,model/gltf-binary" className="sr-only" disabled={!attested || busy} onChange={(e) => { void pick(e.target.files?.[0]); e.target.value = ''; }} />
      </label>
      {error ? <Notice kind="error">{error}</Notice> : null}
    </div>
  );
}

/** The labelled demo avatar (a preset, not a likeness), for people who decline photos or meanwhile. */
function DemoEditor() {
  return (
    <details className="studio-more">
      <summary>Demo avatar options</summary>
      <DemoPresetEditor />
    </details>
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

function DemoPresetEditor() {
  const { ds, me, refresh, toast } = useApp();
  const [def, setDef] = useState<AvatarDefinition>(me.avatar?.definition ?? DEFAULT_AVATAR);
  const set = (k: keyof AvatarDefinition) => (v: string) => setDef((d) => ({ ...d, [k]: v }));
  const save = async () => {
    await ds.saveAvatar(def, false);
    await refresh();
    toast(me.avatar?.kind === 'photo_derived' ? 'Demo avatar saved. Your photo avatar is still the one people see.' : 'Demo avatar saved.');
  };
  return (
    <div className="stack" style={{ gap: 8, marginTop: 10 }}>
      <div className="row"><Avatar def={def} size={96} name={me.displayName} /><p className="muted small" style={{ margin: 0 }}>A stylized stand-in, labelled “demo”, for when you’d rather not use photos. It isn’t your likeness.</p></div>
      <span className="small"><strong>Face</strong></span><Segments values={AVATAR_OPTIONS.face} value={def.face ?? 'soft'} onChange={set('face')} label="Face shape" />
      <span className="small"><strong>Skin tone</strong></span><Swatches values={AVATAR_OPTIONS.skin} value={def.skin} onChange={set('skin')} label="Skin tone" />
      <span className="small"><strong>Hair</strong></span><Segments values={AVATAR_OPTIONS.hair} value={def.hair} onChange={set('hair')} label="Hairstyle" />
      <Swatches values={AVATAR_OPTIONS.hairColor} value={def.hairColor} onChange={set('hairColor')} label="Hair colour" />
      <span className="small"><strong>Clothing</strong></span><Swatches values={AVATAR_OPTIONS.top} value={def.top} onChange={set('top')} label="Top colour" />
      <span className="small"><strong>Accessory</strong></span><Segments values={AVATAR_OPTIONS.accessory} value={def.accessory} onChange={set('accessory')} label="Accessory" />
      <button className="btn btn-outline btn-sm" style={{ alignSelf: 'start' }} onClick={() => void save()}>Save demo avatar</button>
    </div>
  );
}
