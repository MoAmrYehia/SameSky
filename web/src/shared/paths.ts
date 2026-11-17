/**
 * Where the site is served from: "/" in development, "/SameSky/" on GitHub Pages (Vite's `base`, set from
 * SAMESKY_BASE at build time). Files in web/public and links to the two pages go through it, so the same build
 * works at a domain's root or under a sub-path.
 */
export const BASE: string = import.meta.env.BASE_URL;

/** A file in web/public, or one of the pages, by its path from the site root: publicUrl('assets/env/lounge_1k.hdr'). */
export function publicUrl(path: string): string {
  return BASE + path.replace(/^\/+/, '');
}

/**
 * The hosted judges' build has no server behind it (GitHub Pages is static), so it offers the sample day only:
 * no sign-in, no account calls. Set VITE_SAMPLE_ONLY=1 at build time.
 */
export const SAMPLE_ONLY: boolean = import.meta.env.VITE_SAMPLE_ONLY === '1';
