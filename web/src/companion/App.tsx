import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { Me, Room } from '../shared/contracts';
import { ApiError, currentMode, dataSource, setMode, storeToken, type DataSource } from '../shared/data';
import { Icon, Logo } from './ui';
import { Inbox } from './views/Inbox';
import { Keepsakes } from './views/Keepsakes';
import { OurDay } from './views/OurDay';
import { Settings } from './views/Settings';
import { Share } from './views/Share';
import { Together } from './views/Together';
import { Join, Welcome } from './views/Welcome';

interface AppCtx {
  ds: DataSource;
  me: Me;
  rooms: Room[];
  room: Room | null;
  setRoomId: (id: string) => void;
  refresh: () => Promise<void>;
  toast: (message: string) => void;
  signOut: () => void;
  navigate: (route: string) => void;
}

const Ctx = createContext<AppCtx | null>(null);
export const useApp = () => {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useApp outside provider');
  return ctx;
};

function useHashRoute(): [string, (r: string) => void] {
  const read = () => location.hash.replace(/^#\/?/, '') || 'day';
  const [route, setRoute] = useState(read);
  useEffect(() => {
    const on = () => setRoute(read());
    window.addEventListener('hashchange', on);
    return () => window.removeEventListener('hashchange', on);
  }, []);
  return [route, (r: string) => { location.hash = `/${r}`; }];
}

const NAV = [
  { route: 'day', label: 'Our day', icon: 'calendar' },
  { route: 'moments', label: 'Moments', icon: 'image' },
  { route: 'together', label: 'Together', icon: 'heart' },
  { route: 'keepsakes', label: 'Keepsakes', icon: 'bookmark' },
  { route: 'settings', label: 'Settings', icon: 'settings' },
] as const;

export function App() {
  const [mode, setModeState] = useState(currentMode());
  const [route, navigate] = useHashRoute();

  const choose = (m: 'sample' | 'live' | null) => {
    setMode(m);
    setModeState(m);
  };

  if (route.startsWith('join/') && mode !== 'live') {
    return <Welcome onSample={() => choose('sample')} onSignedIn={() => choose('live')} joinToken={route.slice(5)} />;
  }
  if (!mode) return <Welcome onSample={() => choose('sample')} onSignedIn={() => choose('live')} />;
  return <Signed mode={mode} route={route} navigate={navigate} onExit={() => { storeToken(null); choose(null); navigate('day'); }} />;
}

function Signed({ mode, route, navigate, onExit }: { mode: 'sample' | 'live'; route: string; navigate: (r: string) => void; onExit: () => void }) {
  const ds = useMemo(() => dataSource(mode), [mode]);
  const [me, setMe] = useState<Me | null>(null);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [roomId, setRoomIdState] = useState<string | null>(() => {
    try { return localStorage.getItem(`samesky.room.${mode}`); } catch { return null; }
  });
  const [error, setError] = useState<string | null>(null);
  const [toastMsg, setToast] = useState<string | null>(null);
  const [unread, setUnread] = useState(0);

  const refresh = useCallback(async () => {
    try {
      const [m, r, inbox] = await Promise.all([ds.me(), ds.rooms(), ds.inbox()]);
      setMe(m);
      setRooms(r);
      setUnread(inbox.unread);
      setError(null);
    } catch (e) {
      if (e instanceof ApiError && (e.status === 401 || e.code === 'invalid_token')) onExit();
      else setError(e instanceof Error ? e.message : 'Something went wrong.');
    }
  }, [ds, onExit]);

  useEffect(() => {
    void refresh();
    const on = () => void refresh();
    window.addEventListener('samesky:sample-changed', on);
    const timer = window.setInterval(() => void ds.inbox().then((i) => setUnread(i.unread)).catch(() => undefined), 30_000);
    return () => {
      window.removeEventListener('samesky:sample-changed', on);
      window.clearInterval(timer);
    };
  }, [ds, refresh]);

  const toast = useCallback((message: string) => {
    setToast(message);
    window.setTimeout(() => setToast((t) => (t === message ? null : t)), 4200);
  }, []);

  const room = rooms.find((r) => r.id === roomId) ?? rooms[0] ?? null;
  const setRoomId = (id: string) => {
    setRoomIdState(id);
    try { localStorage.setItem(`samesky.room.${mode}`, id); } catch { /* ignore */ }
  };

  if (error && !me) {
    return (
      <div className="main" style={{ maxWidth: 640, margin: '10vh auto' }}>
        <div className="card stack">
          <h1 className="section-title">We couldn't load SameSky</h1>
          <p className="muted">{error}</p>
          <div className="row wrap">
            <button className="btn btn-primary" onClick={() => void refresh()}>Try again</button>
            <button className="btn btn-outline" onClick={onExit}>Back to start</button>
          </div>
        </div>
      </div>
    );
  }
  if (!me) return <div className="main muted" aria-busy="true">Loading your day…</div>;

  const base = route.split('/')[0];
  const ctx: AppCtx = { ds, me, rooms, room, setRoomId, refresh, toast, signOut: onExit, navigate };
  let view;
  if (base === 'moments') view = <Share />;
  else if (base === 'together') view = <Together />;
  else if (base === 'keepsakes') view = <Keepsakes />;
  else if (base === 'settings') view = <Settings />;
  else if (base === 'inbox') view = <Inbox />;
  else if (base === 'join') view = <Join token={route.slice(5)} />;
  else view = <OurDay />;

  const pageClass = base === 'together' || base === 'keepsakes' ? 'page-evening' : base === 'moments' ? 'page-composer' : 'page-day';
  return (
    <Ctx.Provider value={ctx}>
      <div className={`shell ${pageClass}`}>
        <nav className="sidebar" aria-label="Main">
          <a className="brand" href="#/day"><Logo /><span className="brand-name">SameSky</span></a>
          {NAV.map((n) => (
            <a key={n.route} className="nav-item" href={`#/${n.route}`} aria-current={base === n.route || (n.route === 'day' && base === '') ? 'page' : undefined}>
              <Icon name={n.icon} />{n.label}
            </a>
          ))}
          <a className="nav-item" href="/room.html"><Icon name="headset" />Enter our room</a>
          <div className="nav-spacer" />
          <p className="mode-badge">
            {mode === 'sample' ? <><strong>Sample day</strong> · nothing leaves this browser</> : <>Signed in as {me.displayName}</>}
          </p>
        </nav>
        <main className="main" id="main">
          <div className="topbar">
            {rooms.length > 1 ? (
              <label className="row small">
                <span className="sr-only">Room</span>
                <select className="room-select" value={room?.id} onChange={(e) => setRoomId(e.target.value)}>
                  {rooms.map((r) => (
                    <option key={r.id} value={r.id}>{r.kind === 'direct' ? `Direct · ${r.name}` : `Circle · ${r.name}`}</option>
                  ))}
                </select>
              </label>
            ) : null}
            {mode === 'sample' ? <span className="chip label warn" title="Everyone in the sample is fictional; its recaps are precomputed (no AI call).">Sample · fictional people</span> : null}
            <span className="spacer" />
            <a className="icon-btn bell glass" href="#/inbox" aria-label={`Inbox, ${unread} unread`}>
              <Icon name="bell" />
              {unread ? <span className="count">{unread}</span> : null}
            </a>
          </div>
          {view}
        </main>
        <nav className="mobile-tabs" aria-label="Main">
          {NAV.slice(0, 4).map((n) => (
            <a key={n.route} href={`#/${n.route}`} aria-current={base === n.route ? 'page' : undefined}><Icon name={n.icon} />{n.label}</a>
          ))}
          <a href="#/settings" aria-current={base === 'settings' ? 'page' : undefined}><Icon name="settings" />More</a>
        </nav>
      </div>
      {toastMsg ? <div className="toast" role="status">{toastMsg}</div> : null}
    </Ctx.Provider>
  );
}
