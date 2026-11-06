import { useEffect, useState } from 'react';
import { ApiError, apiFetch, dataSource, storeToken } from '../../shared/data';
import { COMMON_ZONES, detectZone } from '../../shared/tz';
import { useApp } from '../App';
import { Icon, Logo, Notice } from '../ui';

export function Welcome({ onSample, onSignedIn, joinToken }: { onSample: () => void; onSignedIn: () => void; joinToken?: string }) {
  const [tab, setTab] = useState<'signin' | 'signup'>(joinToken ? 'signup' : 'signin');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [zone, setZone] = useState(detectZone());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const body = tab === 'signin' ? { email, password } : { email, password, display_name: name, time_zone: zone };
      const res = await apiFetch<{ token: string }>(`/v1/auth/${tab === 'signin' ? 'login' : 'signup'}`, { method: 'POST', body: JSON.stringify(body) }, null);
      storeToken(res.token);
      if (joinToken) {
        await apiFetch('/v1/invites/redeem', { method: 'POST', body: JSON.stringify({ token: joinToken }) }, res.token).catch((err: ApiError) => setError(err.message));
        location.hash = '/day';
      }
      onSignedIn();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="welcome">
      <section className="welcome-hero">
        <div className="row"><Logo size={56} /><span className="brand-name" style={{ fontSize: '2rem', fontWeight: 650 }}>SameSky</span></div>
        <div className="stack" style={{ gap: 18 }}>
          <h1>Your day, side by side.</h1>
          <p>Share a few chosen moments. Meet around the same table, even when you're in different cities and different hours.</p>
          <div className="row wrap">
            <button className="btn btn-primary btn-lg" onClick={onSample}><Icon name="play" />Try a sample day</button>
            <a className="btn btn-lg" style={{ background: 'rgba(255,255,255,.14)', color: '#fff' }} href="/room.html?mode=sample"><Icon name="headset" />Enter the room in VR</a>
          </div>
          <p className="small" style={{ color: '#d9cfbf' }}>The sample needs no account, no headset pairing and no notifications. Everyone in it is fictional.</p>
        </div>
      </section>
      <section className="welcome-panel">
        {joinToken ? <Notice>You've been invited to a SameSky room. Create an account or sign in to join. Only adults can be invited.</Notice> : null}
        <div className="tabs" role="tablist" aria-label="Account">
          <button className="tab" role="tab" aria-selected={tab === 'signin'} onClick={() => setTab('signin')}>Sign in</button>
          <button className="tab" role="tab" aria-selected={tab === 'signup'} onClick={() => setTab('signup')}>Create account</button>
        </div>
        <form className="card stack" onSubmit={submit}>
          {tab === 'signup' ? (
            <label className="field"><span>Display name</span><input className="input" required maxLength={60} value={name} onChange={(e) => setName(e.target.value)} autoComplete="nickname" /></label>
          ) : null}
          <label className="field"><span>Email</span><input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" /></label>
          <label className="field"><span>Password</span><input className="input" type="password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} autoComplete={tab === 'signin' ? 'current-password' : 'new-password'} /></label>
          {tab === 'signup' ? (
            <label className="field">
              <span>Your time zone</span>
              <select className="input" value={zone} onChange={(e) => setZone(e.target.value)}>
                {[...new Set([zone, ...COMMON_ZONES])].map((z) => <option key={z} value={z}>{z.replace(/_/g, ' ')}</option>)}
              </select>
              <small className="muted">Detected from this device — please confirm. You can change it any time you travel; no location sharing needed.</small>
            </label>
          ) : null}
          {error ? <Notice kind="error">{error}</Notice> : null}
          <button className="btn btn-primary btn-lg" disabled={busy}>{tab === 'signin' ? 'Sign in' : 'Create account'}</button>
        </form>
        <div className="split-photo" aria-hidden>
          <div style={{ backgroundImage: 'url(/assets/sample/maya-park-thumb.jpg)' }} />
          <div style={{ backgroundImage: 'url(/assets/sample/adam-waterfront-thumb.jpg)' }} />
        </div>
      </section>
    </div>
  );
}

export function Join({ token }: { token: string }) {
  const { ds, refresh, navigate, setRoomId } = useApp();
  const [state, setState] = useState<'working' | 'error'>('working');
  const [message, setMessage] = useState('');
  useEffect(() => {
    let cancelled = false;
    const live = dataSource('live');
    void live
      .redeem(token)
      .then(async (room) => {
        if (cancelled) return;
        await refresh();
        setRoomId(room.id);
        navigate('day');
      })
      .catch((e: Error) => {
        if (cancelled) return;
        setState('error');
        setMessage(e.message);
      });
    return () => { cancelled = true; };
  }, [token, ds, refresh, navigate, setRoomId]);
  return (
    <div className="card stack" style={{ maxWidth: 560 }}>
      <h1 className="section-title">Joining room…</h1>
      {state === 'error' ? <Notice kind="error">{message}</Notice> : <p className="muted">Checking your invitation.</p>}
    </div>
  );
}
