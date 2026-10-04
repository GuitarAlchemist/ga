// postGodotRender — the IXQL RENDER bridge into the Godot iframe.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { postGodotRender } from './GodotScene';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('postGodotRender', () => {
  it('posts governance:render to the Godot iframe', () => {
    const iframe = document.createElement('iframe');
    iframe.className = 'godot-scene__iframe';
    document.body.appendChild(iframe);
    const post = vi.spyOn(iframe.contentWindow!, 'postMessage');

    expect(postGodotRender('grommet', 'on')).toBe(true);
    expect(post).toHaveBeenCalledWith({ type: 'governance:render', target: 'grommet', action: 'on' }, '*');
  });

  it('returns false when no Godot iframe is open', () => {
    expect(postGodotRender('grommet', 'toggle')).toBe(false);
  });
});
