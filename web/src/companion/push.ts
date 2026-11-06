// Opt-in Web Push. Permission is requested only after the user presses "Enable notifications".
import { apiFetch } from '../shared/data';

export type PushStatus = 'unsupported' | 'insecure' | 'server-disabled' | 'denied' | 'default' | 'enabled';

function urlBase64ToUint8Array(base64: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const raw = atob((base64 + padding).replace(/-/g, '+').replace(/_/g, '/'));
  const out = new Uint8Array(new ArrayBuffer(raw.length));
  for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
  return out;
}

export async function pushStatus(): Promise<PushStatus> {
  if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) return 'unsupported';
  if (!window.isSecureContext) return 'insecure';
  if (Notification.permission === 'denied') return 'denied';
  const reg = await navigator.serviceWorker.getRegistration('/sw.js');
  const sub = await reg?.pushManager.getSubscription();
  if (sub && Notification.permission === 'granted') return 'enabled';
  return 'default';
}

export async function enablePush(): Promise<PushStatus> {
  const status = await pushStatus();
  if (status === 'unsupported' || status === 'insecure' || status === 'denied') return status;
  const { publicKey, enabled } = await apiFetch<{ publicKey: string | null; enabled: boolean }>('/v1/push/vapid-public-key');
  if (!enabled || !publicKey) return 'server-disabled';
  const permission = await Notification.requestPermission();
  if (permission !== 'granted') return permission === 'denied' ? 'denied' : 'default';
  const reg = await navigator.serviceWorker.register('/sw.js');
  await navigator.serviceWorker.ready;
  const sub = (await reg.pushManager.getSubscription()) ?? (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(publicKey) }));
  const json = sub.toJSON();
  const ua = navigator.userAgent;
  const family = /OculusBrowser/.test(ua) ? 'quest-browser' : /Edg\//.test(ua) ? 'edge' : /Chrome\//.test(ua) ? 'chrome' : /Firefox\//.test(ua) ? 'firefox' : /Safari\//.test(ua) ? 'safari' : 'other';
  await apiFetch('/v1/push/subscriptions', { method: 'POST', body: JSON.stringify({ endpoint: json.endpoint, keys: json.keys, user_agent_family: family }) });
  return 'enabled';
}

export async function disablePush(): Promise<void> {
  const reg = await navigator.serviceWorker.getRegistration('/sw.js');
  const sub = await reg?.pushManager.getSubscription();
  if (sub) {
    await apiFetch('/v1/push/subscriptions/delete', { method: 'POST', body: JSON.stringify({ endpoint: sub.endpoint }) }).catch(() => undefined);
    await sub.unsubscribe();
  }
}

export const PUSH_COPY: Record<PushStatus, string> = {
  unsupported: "This browser doesn't support background notifications. Your inbox still collects everything.",
  insecure: 'Notifications need a secure (HTTPS) connection. Your inbox still works.',
  'server-disabled': "Push isn't configured on this server yet. Your inbox still works.",
  denied: 'Notifications are blocked for this site. You can allow them in your browser settings; the inbox works either way.',
  default: 'Off. Turn on to get a generic heads-up like “A shared moment is waiting.”',
  enabled: 'On for this browser. Messages are generic and never include captions or places.',
};
