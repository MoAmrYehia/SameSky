import { useEffect, useState } from 'react';
import type { InboxItem } from '../../shared/contracts';
import { useApp } from '../App';
import { Icon, Notice } from '../ui';

const ICON: Record<InboxItem['kind'], string> = { recap_ready: 'calendar', reply_received: 'reply', call_opportunity: 'users' };

export function Inbox() {
  const { ds, rooms, setRoomId, navigate, toast } = useApp();
  const [items, setItems] = useState<InboxItem[] | null>(null);
  const load = () => void ds.inbox().then((i) => setItems(i.items)).catch(() => setItems([]));
  useEffect(load, [ds]);

  const open = async (n: InboxItem) => {
    await ds.markRead(n.id);
    setRoomId(n.roomId);
    if (n.kind === 'call_opportunity') {
      navigate('together');
      return;
    }
    navigate('day');
  };

  const respond = async (n: InboxItem, action: 'connect' | 'later' | 'not_now' | 'pause') => {
    const r = await ds.respond(n.resourceId, action);
    await ds.markRead(n.id);
    if (action === 'connect') {
      if (r.handoffLink) {
        toast('Opening your calling app. SameSky doesn’t place the call itself.');
        window.open(r.handoffLink, '_blank', 'noopener');
      } else location.href = `/room.html?room=${n.roomId}`;
    } else if (action === 'pause') toast('Suggestions paused for 7 days.');
    load();
  };

  return (
    <>
      <h1 className="page-title" style={{ marginBottom: 20 }}>Inbox</h1>
      <div className="stack" style={{ maxWidth: 760 }}>
        <Notice>Lock-screen notifications only ever say something generic, like “A shared moment is waiting.” Everything else stays in here.</Notice>
        {items === null ? <p className="muted">Loading…</p> : null}
        {items?.length === 0 ? <p className="muted">Nothing waiting. Enjoy the quiet.</p> : null}
        {items?.map((n) => (
          <div key={n.id} className={`inbox-item${n.read ? '' : ' unread'}`}>
            <span className="icon-btn"><Icon name={ICON[n.kind]} /></span>
            <div className="grow">
              <strong>{n.text}</strong>
              <div className="small muted">{rooms.find((r) => r.id === n.roomId)?.name ?? 'A room'} · {new Date(n.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}</div>
            </div>
            {n.kind === 'call_opportunity' ? (
              <div className="row wrap">
                <button className="btn btn-primary" onClick={() => void respond(n, 'connect')}>Connect</button>
                <button className="btn btn-outline" onClick={() => void respond(n, 'later')}>Later</button>
                <button className="btn btn-quiet" onClick={() => void respond(n, 'not_now')}>Not now</button>
                <button className="btn btn-quiet" onClick={() => void respond(n, 'pause')}>Pause suggestions</button>
              </div>
            ) : <button className="btn btn-outline" onClick={() => void open(n)}>Open</button>}
          </div>
        ))}
      </div>
    </>
  );
}
