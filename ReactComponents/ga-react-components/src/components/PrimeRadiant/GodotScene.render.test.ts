// postGodotRender — the IXQL RENDER bridge into the Godot iframe.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { postGodotRender } from './GodotScene';

afterEach(() => {
  document.body.innerHTML = '';
});

function godotIframe(): HTMLIFrameElement {
  const iframe = document.createElement('iframe');
  iframe.className = 'godot-scene__iframe';
  document.body.appendChild(iframe);
  return iframe;
}

function godotReady(iframe: HTMLIFrameElement): void {
  window.dispatchEvent(new MessageEvent('message', { data: { type: 'godot:ready' }, source: iframe.contentWindow }));
}

describe('postGodotRender', () => {
  it('posts governance:render once Godot has said it is ready', () => {
    const iframe = godotIframe();
    godotReady(iframe);
    const post = vi.spyOn(iframe.contentWindow!, 'postMessage');

    expect(postGodotRender('grommet', 'on')).toBe(true);
    expect(post).toHaveBeenCalledWith({ type: 'governance:render', target: 'grommet', action: 'on' }, '*');
  });

  it('returns false while the Godot iframe is still loading', () => {
    const iframe = godotIframe();
    const post = vi.spyOn(iframe.contentWindow!, 'postMessage');

    expect(postGodotRender('grommet', 'on')).toBe(false);
    expect(post).not.toHaveBeenCalled();
  });

  it('keeps an earlier Godot iframe ready when another one loads', () => {
    const first = godotIframe();
    godotReady(first);
    const second = godotIframe();
    godotReady(second);
    second.remove();

    expect(postGodotRender('grommet', 'off')).toBe(true);
  });

  it('waits for a reloaded Godot page to say it is ready again', () => {
    const iframe = godotIframe();
    godotReady(iframe);
    iframe.contentWindow!.dispatchEvent(new Event('pagehide'));

    expect(postGodotRender('grommet', 'on')).toBe(false);
    godotReady(iframe);
    expect(postGodotRender('grommet', 'on')).toBe(true);
  });

  it('returns false when no Godot iframe is open', () => {
    expect(postGodotRender('grommet', 'toggle')).toBe(false);
  });
});
