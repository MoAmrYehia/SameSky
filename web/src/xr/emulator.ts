// Development-only support for Meta's Immersive Web Emulator (IWER), which @iwsdk/vite-plugin-iwer injects on
// localhost to emulate a Quest 3 in a desktop browser. None of this runs on a headset: the plugin skips Quest
// Browser, and `window.IWER_DEVICE` exists only when the emulator is active.

declare global {
  interface Window {
    /** The emulated XR device (IWER `XRDevice`) when the emulator is active. */
    IWER_DEVICE?: {
      name: string;
      remote?: { dispatch(method: string, params?: Record<string, unknown>): Promise<unknown> };
    };
  }
}

/** True when the page is running against the emulated headset rather than a real one. */
export function emulatorActive(): boolean {
  return typeof window !== 'undefined' && !!window.IWER_DEVICE;
}

/**
 * Desktop Chrome now ships the WebXR Layers API natively, so three.js sees `createProjectionLayer` on the
 * native `XRWebGLBinding` and tries to build one around the emulator's session object, which the native
 * constructor rejects ("parameter 1 is not of type 'XRSession'") and the session dies immediately. The IWER
 * build bundled in @iwsdk/vite-plugin-iwer 0.2.2 doesn't replace that global. While emulating, swap in a
 * binding without projection layers, so three.js uses the `XRWebGLLayer` path the emulator implements.
 * Call this before the session starts. It is a no-op on a real headset and when no emulator is present.
 */
export function prepareEmulatedSession(): void {
  if (!emulatorActive()) return;
  const g = globalThis as unknown as { XRWebGLBinding?: { prototype: object } };
  if (g.XRWebGLBinding && 'createProjectionLayer' in g.XRWebGLBinding.prototype) {
    g.XRWebGLBinding = class EmulatedWebGLBinding {} as unknown as { prototype: object };
  }
}
