/**
 * Hash of the running bundle (main-XXXXXXXX.js), so each deploy has its own value.
 * Appended to unhashed assets (translations, appSettings) so a browser never keeps
 * last release's copy after a deploy.
 */
export const BUILD_VERSION: string =
  (document.querySelector('script[src*="main-"]') as HTMLScriptElement | null)
    ?.src.match(/main-([A-Za-z0-9]+)\.js/)?.[1] ?? 'dev';

/** `assets/x.json` → `assets/x.json?v=<build>` */
export const versioned = (url: string): string => `${url}?v=${BUILD_VERSION}`;
