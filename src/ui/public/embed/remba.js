(function (global) {
  'use strict';

  var STYLE_ID = 'remba-save-styles-v17';
  var ATTR = 'data-remba-save';
  var CHEVRON_DOWN = '▾';
  var CHEVRON_UP = '▴';
  var MORPH_MS = 420;
  var BRAND_ACCENT = '#2ec4b6';
  var shellStack = 20;
  var tokenCache = null;

  var LIST_TYPES = [
    { key: 'book', label: 'Books' },
    { key: 'film', label: 'Film' },
    { key: 'tv', label: 'TV' },
    { key: 'country', label: 'Countries' },
    { key: 'recipe', label: 'Recipes' },
  ];

  var THEMES = {
    light: {
      color: '#111111',
      background: '#ffffff',
      border: 'rgba(0,0,0,.12)',
      muted: '#666666',
      hover: 'rgba(0,0,0,.06)',
      danger: '#b00020',
    },
    dark: {
      color: '#f4f4f4',
      background: '#171717',
      border: 'rgba(255,255,255,.14)',
      muted: '#a3a3a3',
      hover: 'rgba(255,255,255,.08)',
      danger: '#ff8a80',
    },
  };

  function currentScript() {
    return document.currentScript || (function () {
      var scripts = document.getElementsByTagName('script');
      for (var i = scripts.length - 1; i >= 0; i -= 1) {
        var src = scripts[i].getAttribute('src') || '';
        if (src.indexOf('embed/remba.js') !== -1) {
          return scripts[i];
        }
      }
      return null;
    })();
  }

  function readAttr(el, name, fallback) {
    if (!el) {
      return fallback || '';
    }
    var value = el.getAttribute(name);
    if (value == null || value === '') {
      return fallback || '';
    }
    return value;
  }

  function readOption(el, name, fallback) {
    return readAttr(el, 'data-' + name, fallback);
  }

  function scriptConfig() {
    var script = currentScript();
    var appUrl = readAttr(script, 'data-app-url', '');
    var apiUrl = readAttr(script, 'data-api-url', '');

    if (!appUrl && script && script.getAttribute('src')) {
      try {
        var url = new URL(script.getAttribute('src'), global.location.href);
        appUrl = url.origin + url.pathname.replace(/\/embed\/remba\.js(?:\?.*)?$/, '');
      } catch (error) {
        appUrl = global.location.origin;
      }
    }

    if (!apiUrl) {
      if (/localhost|127\.0\.0\.1/.test(appUrl) || /localhost|127\.0\.0\.1/.test(global.location.host)) {
        apiUrl = 'https://localhost:7257';
      } else if (/dev\.remba\.it/.test(appUrl) || /dev\.remba\.it/.test(global.location.host)) {
        apiUrl = 'https://dev-api.remba.it';
      } else {
        apiUrl = 'https://api.remba.it';
      }
    }

    return {
      appUrl: String(appUrl || global.location.origin).replace(/\/$/, ''),
      apiUrl: String(apiUrl).replace(/\/$/, ''),
      auth0Domain: readAttr(script, 'data-auth0-domain', 'remba-it.uk.auth0.com'),
      auth0ClientId: readAttr(
        script,
        'data-auth0-client-id',
        'VgRe2f4ao5A4WaH8ckLstNHk7vrbupgS',
      ),
      auth0Audience: readAttr(script, 'data-auth0-audience', 'https://api.remba.it'),
      theme: readAttr(script, 'data-theme', 'light'),
      color: readAttr(script, 'data-color', ''),
      background: readAttr(script, 'data-background', ''),
      border: readAttr(script, 'data-border', ''),
      muted: readAttr(script, 'data-muted', ''),
    };
  }

  function resolveTheme(config, el) {
    var themeName = (readOption(el, 'theme') || config.theme || 'light').toLowerCase();
    var base = THEMES[themeName] || THEMES.light;
    var color = readOption(el, 'color') || config.color || base.color;
    var background = readOption(el, 'background') || config.background || base.background;
    var border = readOption(el, 'border') || config.border || base.border;
    var muted = readOption(el, 'muted') || config.muted || base.muted;

    // Bespoke text+background: derive supporting tones when not supplied.
    if ((config.color || config.background || readOption(el, 'color') || readOption(el, 'background'))
      && !(readOption(el, 'border') || config.border)) {
      border = mixBorder(color, background);
    }
    if ((config.color || config.background || readOption(el, 'color') || readOption(el, 'background'))
      && !(readOption(el, 'muted') || config.muted)) {
      muted = mixMuted(color, background);
    }

    return {
      name: THEMES[themeName] ? themeName : 'custom',
      color: color,
      background: background,
      border: border,
      muted: muted,
      hover: mixHover(color, background),
      danger: base.danger,
      // Closed circle and open panel share the same surface colours.
      buttonColor: color,
      buttonBackground: background,
    };
  }

  function isDarkColor(value) {
    var hex = String(value || '').trim();
    var match = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(hex);
    if (!match) {
      // Named/advanced colours — assume dark faces need the light artwork.
      return /black|dark|#0|#1|#2/i.test(hex);
    }
    var raw = match[1];
    if (raw.length === 3) {
      raw = raw[0] + raw[0] + raw[1] + raw[1] + raw[2] + raw[2];
    }
    var r = parseInt(raw.slice(0, 2), 16);
    var g = parseInt(raw.slice(2, 4), 16);
    var b = parseInt(raw.slice(4, 6), 16);
    // Relative luminance threshold.
    return (r * 299 + g * 587 + b * 114) / 1000 < 140;
  }

  function markIconSrcForBackground(appUrl, background) {
    // *-dark is the light artwork for dark surfaces.
    var file = isDarkColor(background)
      ? 'word-icon-transparent-dark.png'
      : 'word-icon-transparent.png';
    return String(appUrl || '').replace(/\/$/, '') + '/assets/images/' + file;
  }

  function markIconSrc(appUrl, theme) {
    return markIconSrcForBackground(appUrl, theme.buttonBackground);
  }

  function syncMark(root, theme, open) {
    var img = root.querySelector('.remba-mark');
    if (!img || !theme) {
      return;
    }
    var surface = open ? theme.background : theme.buttonBackground;
    var next = markIconSrcForBackground(root._rembaAppUrl || '', surface);
    // Re-assigning the same URL still reloads the image in some browsers and flashes.
    if (img.getAttribute('src') === next) {
      return;
    }
    try {
      if (img.src === new URL(next, window.location.href).href) {
        return;
      }
    } catch (error) {
      // Ignore URL parse errors and fall through to assign.
    }
    img.src = next;
  }

  function flipMark(mark, applyDomChange) {
    if (!mark) {
      applyDomChange();
      return;
    }

    var first = mark.getBoundingClientRect();
    applyDomChange();
    var last = mark.getBoundingClientRect();
    var dx = first.left - last.left;
    var dy = first.top - last.top;
    if (!dx && !dy) {
      return;
    }

    mark.style.transition = 'none';
    mark.style.transform = 'translate(' + dx + 'px, ' + dy + 'px)';
    void mark.offsetWidth;
    mark.style.transition = 'transform ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1)';
    mark.style.transform = 'translate(0, 0)';

    window.setTimeout(function () {
      mark.style.transition = '';
      mark.style.transform = '';
    }, MORPH_MS + 40);
  }

  function colinerFontUrl(appUrl) {
    return String(appUrl || '').replace(/\/$/, '') + '/assets/Coliner-Regular.ttf';
  }

  function mixHover(color, background) {
    return 'color-mix(in srgb, ' + color + ' 8%, ' + background + ')';
  }

  function mixBorder(color, background) {
    return 'color-mix(in srgb, ' + color + ' 16%, ' + background + ')';
  }

  function mixMuted(color, background) {
    return 'color-mix(in srgb, ' + color + ' 55%, ' + background + ')';
  }

  function applyTheme(root, theme) {
    root.style.setProperty('--remba-color', theme.color);
    root.style.setProperty('--remba-bg', theme.background);
    root.style.setProperty('--remba-border', theme.border);
    root.style.setProperty('--remba-muted', theme.muted);
    root.style.setProperty('--remba-hover', theme.hover);
    root.style.setProperty('--remba-danger', theme.danger);
    root.style.setProperty('--remba-button-color', theme.buttonColor);
    root.style.setProperty('--remba-button-bg', theme.buttonBackground);
    root.setAttribute('data-remba-theme', theme.name);
  }

  function ensureStyles(appUrl) {
    if (document.getElementById(STYLE_ID)) {
      return;
    }

    var style = document.createElement('style');
    style.id = STYLE_ID;
    style.textContent = [
      '@font-face{font-family:"Coliner";src:url("' + colinerFontUrl(appUrl) + '") format("truetype");',
      'font-weight:400;font-style:normal;font-display:swap}',
      '.remba-root{position:relative;display:inline-block;width:2.5rem;height:2.5rem;',
      'max-width:100%;vertical-align:top;z-index:1;',
      'font:500 .9rem/1.25 system-ui,-apple-system,Segoe UI,sans-serif;',
      'color:var(--remba-color,#111);--remba-bg:#fff;--remba-border:rgba(0,0,0,.12);',
      '--remba-muted:#666;--remba-hover:rgba(0,0,0,.06);--remba-danger:#b00020;',
      '--remba-button-color:#fff;--remba-button-bg:#111;--remba-brand:' + BRAND_ACCENT + '}',
      '.remba-root.is-open{z-index:50}',
      '.remba-shell{position:absolute;z-index:5;box-sizing:border-box;overflow:hidden;',
      'width:2.5rem;height:2.5rem;max-height:2.5rem;padding:0;border:1px solid var(--remba-border);',
      'border-radius:999px;background:var(--remba-button-bg);color:var(--remba-button-color);',
      'box-shadow:0 1px 2px rgba(0,0,0,.14);cursor:pointer;',
      'display:flex;flex-direction:column;',
      'transition:width ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1),',
      'height ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1),',
      'max-height ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1),',
      'border-radius ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1),',
      'padding ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1),',
      'background ' + MORPH_MS + 'ms ease,color ' + MORPH_MS + 'ms ease,',
      'box-shadow ' + MORPH_MS + 'ms ease}',
      '.remba-shell--tl{top:0;left:0}',
      '.remba-shell--tr{top:0;right:0}',
      '.remba-shell--bl{bottom:0;left:0}',
      '.remba-shell--br{bottom:0;right:0}',
      '.remba-shell:focus-visible{outline:2px solid var(--remba-button-bg);outline-offset:2px}',
      '.remba-shell.is-open{width:min(22.5rem,calc(100vw - 2rem));height:auto;',
      'max-height:min(80vh,42rem);padding:.85rem;border-radius:.85rem;',
      'background:var(--remba-bg);color:var(--remba-color);cursor:default;',
      'box-shadow:0 10px 28px rgba(0,0,0,.14),0 2px 6px rgba(0,0,0,.06)}',
      '.remba-shell__chrome{display:flex;align-items:center;justify-content:center;',
      'gap:0;flex:0 0 auto;width:100%;height:2.5rem;min-height:2.5rem;position:relative;',
      'z-index:3;transition:justify-content ' + MORPH_MS + 'ms ease,height ' + MORPH_MS + 'ms ease,',
      'min-height ' + MORPH_MS + 'ms ease,margin ' + MORPH_MS + 'ms ease,gap ' + MORPH_MS + 'ms ease}',
      '.remba-shell.is-open .remba-shell__chrome{justify-content:space-between;gap:.55rem;',
      'height:auto;min-height:2rem;margin:0 0 .7rem}',
      '.remba-shell__brand{display:flex;align-items:center;justify-content:center;gap:.45rem;',
      'min-width:0;width:100%;height:100%}',
      '.remba-shell.is-open .remba-shell__brand{justify-content:flex-start;width:auto;flex:1;',
      'height:auto}',
      '.remba-mark{display:block;width:1.55rem;height:1.55rem;object-fit:contain;flex:0 0 auto;',
      'position:relative;z-index:5;will-change:transform}',
      '.remba-word{font-family:Coliner,system-ui,sans-serif;font-size:1.2rem;line-height:1;',
      'letter-spacing:.01em;white-space:nowrap;opacity:0;max-width:0;overflow:hidden;',
      'transform:translateX(-.35rem);pointer-events:none;flex:0 0 auto;',
      'transition:opacity .28s ease ' + Math.round(MORPH_MS * 0.25) + 'ms,',
      'max-width .28s ease ' + Math.round(MORPH_MS * 0.2) + 'ms,',
      'transform .28s ease ' + Math.round(MORPH_MS * 0.25) + 'ms}',
      '.remba-shell.is-open .remba-word{opacity:1;max-width:8rem;transform:none;pointer-events:auto}',
      '.remba-shell:not(.is-open) .remba-word{transition-delay:0s;position:absolute;width:0;height:0}',
      '.remba-word__dot{color:var(--remba-brand)}',
      '.remba-shell__close{appearance:none;flex:0 0 auto;width:1.85rem;height:1.85rem;padding:0;',
      'border:0;border-radius:.5rem;cursor:pointer;display:grid;place-items:center;',
      'background:var(--remba-hover);color:var(--remba-muted);',
      'font:600 1.2rem/1 system-ui,sans-serif;opacity:0;pointer-events:none;transform:scale(.85);',
      'transition:opacity .25s ease ' + Math.round(MORPH_MS * 0.35) + 'ms,transform .25s ease ',
      Math.round(MORPH_MS * 0.35) + 'ms}',
      '.remba-shell:not(.is-open) .remba-shell__close{position:absolute;right:0;top:50%;',
      'width:0;height:0;margin:0;overflow:hidden;border:0;transition-delay:0s}',
      '.remba-shell.is-open .remba-shell__close{opacity:1;pointer-events:auto;transform:none;',
      'position:relative;right:auto;top:auto;z-index:4}',
      '.remba-shell__close:hover{background:var(--remba-button-bg);color:var(--remba-button-color)}',
      '.remba-shell__close:focus-visible{outline:2px solid var(--remba-color);outline-offset:1px}',
      '.remba-shell__body{display:grid;grid-template-rows:0fr;opacity:0;flex:1;min-height:0;',
      'transition:grid-template-rows ' + MORPH_MS + 'ms cubic-bezier(.2,.8,.2,1),',
      'opacity .24s ease}',
      '.remba-shell.is-open .remba-shell__body{grid-template-rows:1fr;opacity:1;',
      'transition-delay:0s,.12s}',
      '.remba-shell:not(.is-open) .remba-shell__body{transition-delay:0s,0s}',
      '.remba-shell__body-inner{overflow:hidden;min-height:0}',
      '.remba-menu__item{display:flex;align-items:center;justify-content:space-between;gap:.75rem;',
      'width:100%;border:0;border-radius:.5rem;background:transparent;color:inherit;',
      'padding:.55rem .65rem;cursor:pointer;text-align:left;font:inherit}',
      '.remba-menu__item:hover,.remba-menu__item.is-open{background:var(--remba-hover)}',
      '.remba-menu__chevron{opacity:.55;font-size:.85em;line-height:1}',
      '.remba-submenu{margin-top:.2rem;padding:.25rem;border-radius:.55rem;background:var(--remba-hover);',
      'min-height:3rem}',
      '.remba-submenu[hidden]{display:none!important}',
      '.remba-list-row{display:flex;align-items:center;gap:.35rem;min-height:2.1rem;',
      'padding:.15rem .25rem .15rem .55rem;border-radius:.45rem}',
      '.remba-list-row:hover{background:var(--remba-hover)}',
      '.remba-list-name{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}',
      '.remba-add,.remba-expand{appearance:none;flex:0 0 auto;width:1.75rem;height:1.75rem;border:0;',
      'border-radius:999px;display:grid;place-items:center;background:var(--remba-hover);',
      'color:var(--remba-color);cursor:pointer;font:700 1rem/1 system-ui,sans-serif;padding:0}',
      '.remba-expand{font-size:.75rem;font-weight:600}',
      '.remba-add:hover:not(:disabled),.remba-expand:hover:not(:disabled){',
      'background:var(--remba-button-bg);color:var(--remba-button-color)}',
      '.remba-add:disabled,.remba-expand:disabled{opacity:.85;cursor:default}',
      '.remba-divider{height:1px;margin:.35rem .45rem;background:var(--remba-border)}',
      '.remba-create{margin-top:.15rem}',
      '.remba-create__toggle{display:flex;align-items:center;justify-content:space-between;gap:.75rem;',
      'width:100%;border:0;border-radius:.5rem;background:transparent;color:inherit;',
      'padding:.55rem .65rem;cursor:pointer;text-align:left;font:inherit}',
      '.remba-create__toggle:hover,.remba-create.is-open .remba-create__toggle{background:var(--remba-hover)}',
      '.remba-create__body{margin-top:.2rem;padding:.25rem;border-radius:.55rem;background:var(--remba-hover)}',
      '.remba-create__body[hidden]{display:none!important}',
      '.remba-create__types{padding:.05rem 0}',
      '.remba-create-row{border-radius:.45rem}',
      '.remba-create-row.is-open{background:var(--remba-hover)}',
      '.remba-create-row__main{display:flex;align-items:center;gap:.35rem;min-height:2.1rem;',
      'padding:.15rem .25rem .15rem .55rem}',
      '.remba-create-row__form{display:flex;align-items:center;gap:.35rem;padding:0 .25rem .45rem .55rem}',
      '.remba-create-row__form[hidden]{display:none!important}',
      '.remba-create-input{flex:1;min-width:0;border:1px solid var(--remba-border);border-radius:.4rem;',
      'padding:.35rem .45rem;background:var(--remba-bg);color:var(--remba-color);font:inherit}',
      '.remba-create-input:focus{outline:2px solid var(--remba-color);outline-offset:1px}',
      '.remba-status{padding:.55rem .65rem;color:var(--remba-muted);font-size:.82rem}',
      '.remba-status.is-error{color:var(--remba-danger)}',
      '.remba-auth{margin-top:.35rem;width:100%;min-height:11rem;border:0;border-radius:.55rem;',
      'background:var(--remba-hover)}',
      '.remba-auth[hidden]{display:none!important}',
      '.remba-loading{position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;',
      'justify-content:center;gap:.65rem;padding:1rem;border-radius:inherit;',
      'background:color-mix(in srgb, var(--remba-bg) 92%, transparent);color:var(--remba-muted);',
      'font-size:.82rem;text-align:center;z-index:1;pointer-events:none;opacity:1;',
      'transition:opacity .18s ease}',
      '.remba-loading.is-leaving{opacity:0}',
      '.remba-loading[hidden]{display:none!important}',
      '.remba-spinner{width:1.35rem;height:1.35rem;border-radius:999px;',
      'border:2px solid var(--remba-border);border-top-color:var(--remba-color);',
      'animation:remba-spin .7s linear infinite}',
      '@keyframes remba-spin{to{transform:rotate(360deg)}}',
      '.remba-shell.is-busy .remba-menu__item,.remba-shell.is-busy .remba-submenu,',
      '.remba-shell.is-busy .remba-divider,.remba-shell.is-busy .remba-create{visibility:hidden}',
    ].join('');
    document.head.appendChild(style);
  }

  function getCachedToken() {
    if (!tokenCache || !tokenCache.accessToken) {
      return null;
    }
    if (Date.now() > tokenCache.expiresAt - 15000) {
      tokenCache = null;
      return null;
    }
    return tokenCache.accessToken;
  }

  function setStatus(el, message, isError) {
    if (!el) {
      return;
    }
    el.hidden = !message;
    el.textContent = message || '';
    el.className = 'remba-status' + (isError ? ' is-error' : '');
  }

  function setBusy(root, busy) {
    var shell = root.querySelector('.remba-shell');
    if (shell) {
      shell.classList.toggle('is-busy', !!busy);
    }
  }

  function resolveAnchor(root) {
    var rect = root.getBoundingClientRect();
    var midX = rect.left + rect.width / 2;
    var midY = rect.top + rect.height / 2;
    var openLeft = midX > window.innerWidth * 0.55;
    var openUp = midY > window.innerHeight * 0.55;
    if (openUp && openLeft) {
      return 'br';
    }
    if (openUp) {
      return 'bl';
    }
    if (openLeft) {
      return 'tr';
    }
    return 'tl';
  }

  function applyShellAnchor(shell, anchor) {
    shell.classList.remove('remba-shell--tl', 'remba-shell--tr', 'remba-shell--bl', 'remba-shell--br');
    shell.classList.add('remba-shell--' + anchor);
    shell.setAttribute('data-anchor', anchor);
  }

  function elevateOpenShell(root) {
    shellStack += 1;
    var z = String(shellStack);
    root.style.zIndex = z;
    var shell = root.querySelector('.remba-shell');
    if (shell) {
      shell.style.zIndex = z;
    }
    // Lift a positioned parent (e.g. playground corner wrap) so siblings cannot cover us.
    var parent = root.parentElement;
    if (parent && parent !== document.body && parent !== document.documentElement) {
      var parentStyle = window.getComputedStyle(parent);
      if (parentStyle.position !== 'static') {
        parent.style.zIndex = z;
        parent.setAttribute('data-remba-stack', 'true');
      }
    }
  }

  function clearShellElevation(root) {
    root.style.zIndex = '';
    var shell = root.querySelector('.remba-shell');
    if (shell) {
      shell.style.zIndex = '';
    }
    var parent = root.parentElement;
    if (parent && parent.getAttribute('data-remba-stack') === 'true') {
      parent.style.zIndex = '';
      parent.removeAttribute('data-remba-stack');
    }
  }

  function resetShellContent(root) {
    var submenu = root.querySelector('.remba-submenu');
    var createEl = root.querySelector('.remba-create');
    var trigger = root.querySelector('.remba-menu__item--add');
    var authFrame = root.querySelector('.remba-auth');
    var statusEl = root.querySelector('.remba-status');

    if (submenu) {
      submenu.hidden = true;
      submenu.innerHTML = '';
    }
    if (createEl) {
      createEl.classList.remove('is-open');
      createEl.innerHTML = '';
    }
    if (trigger) {
      trigger.classList.remove('is-open');
      trigger.setAttribute('aria-expanded', 'false');
      setExpandChevron(trigger.querySelector('.remba-menu__chevron'), false);
    }
    if (authFrame) {
      authFrame.hidden = true;
      authFrame.removeAttribute('src');
    }
    setLoading(root, false);
    setStatus(statusEl, '', false);
  }

  function setLoading(root, loading, message) {
    var loadingEl = root.querySelector('.remba-loading');
    var label = root.querySelector('.remba-loading__label');
    var authFrame = root.querySelector('.remba-auth');
    if (!loadingEl) {
      return;
    }

    loadingEl.classList.remove('is-leaving');
    setBusy(root, !!loading);
    loadingEl.hidden = !loading;
    if (loading && authFrame) {
      authFrame.hidden = true;
    }
    if (label) {
      label.textContent = message || 'Loading…';
    }
  }

  function dismissLoading(root) {
    var loadingEl = root.querySelector('.remba-loading');
    setBusy(root, false);
    if (!loadingEl || loadingEl.hidden) {
      return;
    }

    loadingEl.classList.add('is-leaving');
    window.setTimeout(function () {
      if (!loadingEl.classList.contains('is-leaving')) {
        return;
      }
      loadingEl.hidden = true;
      loadingEl.classList.remove('is-leaving');
      var label = root.querySelector('.remba-loading__label');
      if (label) {
        label.textContent = 'Loading…';
      }
    }, 180);
  }

  function showAuthPanel(root) {
    var loadingEl = root.querySelector('.remba-loading');
    var authFrame = root.querySelector('.remba-auth');
    // Keep menu hidden (is-busy) but show the auth frame instead of the spinner.
    setBusy(root, true);
    if (loadingEl) {
      loadingEl.hidden = true;
    }
    setStatus(root.querySelector('.remba-status'), 'Signing in…', false);
    return authFrame;
  }

  function requestAccessToken(root, config) {
    var cached = getCachedToken();
    if (cached) {
      return Promise.resolve(cached);
    }

    return new Promise(function (resolve, reject) {
      var authFrame = showAuthPanel(root);
      if (!authFrame) {
        reject(new Error('Auth frame missing.'));
        return;
      }

      var settled = false;
      var params = new URLSearchParams({
        domain: config.auth0Domain,
        clientId: config.auth0ClientId,
        audience: config.auth0Audience,
        embedded: '1',
      });

      function cleanup() {
        global.removeEventListener('message', onMessage);
      }

      function finish(error, token) {
        if (settled) {
          return;
        }
        settled = true;
        cleanup();
        authFrame.hidden = true;
        authFrame.removeAttribute('src');
        setStatus(root.querySelector('.remba-status'), '', false);
        if (error) {
          setLoading(root, false);
          reject(error);
          return;
        }
        // Resume spinner while the follow-up API calls run.
        setLoading(root, true, 'Loading your lists…');
        resolve(token);
      }

      function onMessage(event) {
        var data = event && event.data;
        if (!data || data.type !== 'remba-auth') {
          return;
        }
        try {
          if (event.origin !== new URL(config.appUrl).origin) {
            return;
          }
        } catch (error) {
          return;
        }

        if (data.ok && data.accessToken) {
          tokenCache = {
            accessToken: data.accessToken,
            expiresAt: data.expiresAt || Date.now() + 3600 * 1000,
          };
          finish(null, data.accessToken);
          return;
        }
        finish(new Error(data.error || 'Sign-in failed'));
      }

      global.addEventListener('message', onMessage);
      authFrame.hidden = false;
      authFrame.src = config.appUrl + '/embed/auth.html?' + params.toString();
    });
  }

  function apiFetch(root, config, path, options) {
    options = options || {};
    return requestAccessToken(root, config).then(function (token) {
      return fetch(config.apiUrl + path, {
        method: options.method || 'GET',
        headers: {
          Accept: 'application/json',
          Authorization: 'Bearer ' + token,
          ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        },
        body: options.body ? JSON.stringify(options.body) : undefined,
      }).then(function (response) {
        if (!response.ok) {
          throw new Error('Request failed (' + response.status + ')');
        }
        if (response.status === 204) {
          return null;
        }
        return response.json();
      });
    });
  }

  function defaultIdType(entityType, idValue) {
    var type = String(entityType || '').toLowerCase();
    var id = String(idValue || '').trim();
    if (type === 'book') {
      return /^\d{9}[\dXx]$|^\d{13}$/.test(id.replace(/[-\s]/g, '')) ? 'isbn' : 'isbn';
    }
    if (type === 'film' || type === 'tv') {
      return 'imdbId';
    }
    if (type === 'country') {
      return 'iso2';
    }
    if (type === 'recipe') {
      return 'url';
    }
    return 'url';
  }

  function parseItemJson(raw) {
    if (!raw) {
      return null;
    }
    try {
      var parsed = JSON.parse(raw);
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
        return null;
      }
      return parsed;
    } catch (error) {
      return null;
    }
  }

  function buildItem(el, overrides) {
    overrides = overrides || {};
    var json = parseItemJson(overrides.itemJson || readOption(el, 'item'));
    var source = json || {};

    var entityType =
      overrides.entityType ||
      source.entityType ||
      readOption(el, 'entity-type') ||
      '';
    var id = overrides.id || source.id || readOption(el, 'id') || '';
    var idType =
      overrides.idType ||
      source.idType ||
      readOption(el, 'id-type') ||
      (id ? defaultIdType(entityType, id) : '');

    return {
      entityType: String(entityType || '').trim().toLowerCase(),
      id: String(id || '').trim(),
      idType: String(idType || '').trim(),
      name: overrides.name || source.name || readOption(el, 'name') || '',
      link:
        overrides.link ||
        source.link ||
        readOption(el, 'link') ||
        (entityType === 'recipe' ? id : '') ||
        global.location.href,
      imageUrl: overrides.imageUrl || source.imageUrl || readOption(el, 'image-url') || '',
      author: overrides.author || source.author || readOption(el, 'author') || '',
      director: overrides.director || source.director || readOption(el, 'director') || '',
      referral:
        overrides.referral ||
        source.referral ||
        readOption(el, 'referral') ||
        global.location.href,
    };
  }

  function itemIsReady(item) {
    return !!(item.name || (item.entityType && item.id));
  }

  function resolveItemRecord(root, config, item) {
    if (!item.entityType || !item.id) {
      return Promise.resolve(item);
    }

    var query =
      '/api/entities/search?entityType=' +
      encodeURIComponent(item.entityType) +
      '&q=' +
      encodeURIComponent(item.id);

    return apiFetch(root, config, query)
      .then(function (results) {
        var hit = Array.isArray(results) && results.length ? results[0] : null;
        if (!hit) {
          return item;
        }

        return Object.assign({}, item, {
          name: item.name || hit.name || item.id,
          link: item.link || hit.link || item.link,
          imageUrl: item.imageUrl || hit.imageUrl || '',
          author: item.author || hit.author || '',
          director: item.director || hit.director || '',
          idType: item.idType || hit.identifierType || item.idType,
          id: hit.identifierValue || item.id,
          entityId: hit.entityId || undefined,
        });
      })
      .catch(function () {
        return item;
      });
  }

  function buildCreateBody(item) {
    var body = {
      name: item.name || item.id || 'Untitled',
      link: item.link || undefined,
      imageUrl: item.imageUrl || undefined,
      author: item.author || undefined,
      director: item.director || undefined,
    };

    if (item.entityId) {
      body.entityId = item.entityId;
    }

    if (item.id && item.idType) {
      body.identifier = {
        type: item.idType,
        value: item.id,
      };
    }

    return body;
  }

  function listMatchesItem(list, item) {
    if (!item.entityType) {
      return true;
    }
    var listType = String(list.entityType || '').trim().toLowerCase();
    return !listType || listType === item.entityType;
  }

  function sortListsForItem(lists, item) {
    return lists.slice().sort(function (a, b) {
      var aMatch = listMatchesItem(a, item) ? 0 : 1;
      var bMatch = listMatchesItem(b, item) ? 0 : 1;
      if (aMatch !== bMatch) {
        return aMatch - bMatch;
      }
      return String(a.name || '').localeCompare(String(b.name || ''));
    });
  }

  function saveItemToList(root, config, item, listId, listName, actionButton) {
    if (actionButton) {
      actionButton.disabled = true;
      actionButton.innerHTML = '<span class="remba-spinner" aria-hidden="true"></span>';
    }
    setStatus(root.querySelector('.remba-status'), '', false);
    setLoading(root, true, 'Saving…');

    return resolveItemRecord(root, config, item)
      .then(function (resolved) {
        return apiFetch(root, config, '/api/lists/' + encodeURIComponent(listId) + '/entries', {
          method: 'POST',
          body: buildCreateBody(resolved),
        }).then(function () {
          return resolved;
        });
      })
      .then(function (resolved) {
        if (!resolved.referral) {
          return null;
        }
        return apiFetch(root, config, '/api/lists/' + encodeURIComponent(listId), {
          method: 'PUT',
          body: { referralLink: resolved.referral },
        }).catch(function () {
          return null;
        });
      })
      .then(function () {
        setLoading(root, false);
        markListButtonPresent(actionButton, listName);
        setStatus(
          root.querySelector('.remba-status'),
          'Added to “' + (listName || 'list') + '”.',
          false,
        );
      })
      .catch(function (error) {
        setLoading(root, false);
        if (actionButton) {
          actionButton.disabled = false;
          actionButton.textContent = '+';
        }
        setStatus(
          root.querySelector('.remba-status'),
          (error && error.message) || 'Could not add item.',
          true,
        );
        throw error;
      });
  }

  function createListAndAddItem(root, config, item, entityType, listName, actionButton) {
    var name = String(listName || '').trim();
    if (!name) {
      setStatus(root.querySelector('.remba-status'), 'Enter a list name.', true);
      return Promise.reject(new Error('name_required'));
    }

    if (actionButton) {
      actionButton.disabled = true;
      actionButton.innerHTML = '<span class="remba-spinner" aria-hidden="true"></span>';
    }
    setStatus(root.querySelector('.remba-status'), '', false);
    setLoading(root, true, 'Creating list…');

    var body = {
      name: name,
      entityType: entityType || undefined,
    };
    if (item.referral) {
      body.referralLink = item.referral;
    }

    return apiFetch(root, config, '/api/lists', { method: 'POST', body: body })
      .then(function (created) {
        setLoading(root, true, 'Saving…');
        return saveItemToList(
          root,
          config,
          item,
          created.id,
          created.name || name,
          actionButton,
        ).catch(function () {
          // Status already set by saveItemToList.
        });
      })
      .catch(function (error) {
        if (error && error.message === 'name_required') {
          return;
        }
        setLoading(root, false);
        if (actionButton) {
          actionButton.disabled = false;
          actionButton.textContent = '+';
        }
        setStatus(
          root.querySelector('.remba-status'),
          (error && error.message) || 'Could not create list.',
          true,
        );
      });
  }

  function orderedListTypes(item) {
    var preferred = String((item && item.entityType) || '').toLowerCase();
    return LIST_TYPES.slice().sort(function (a, b) {
      var aMatch = preferred && a.key === preferred ? 0 : 1;
      var bMatch = preferred && b.key === preferred ? 0 : 1;
      if (aMatch !== bMatch) {
        return aMatch - bMatch;
      }
      return a.label.localeCompare(b.label);
    });
  }

  function setExpandChevron(el, open) {
    if (!el) {
      return;
    }
    el.textContent = open ? CHEVRON_UP : CHEVRON_DOWN;
  }

  function closeAddSection(root) {
    var submenu = root.querySelector('.remba-submenu');
    var trigger = root.querySelector('.remba-menu__item--add');
    if (submenu) {
      submenu.hidden = true;
    }
    if (trigger) {
      trigger.classList.remove('is-open');
      trigger.setAttribute('aria-expanded', 'false');
      setExpandChevron(trigger.querySelector('.remba-menu__chevron'), false);
    }
  }

  function closeCreateSection(root) {
    var createEl = root.querySelector('.remba-create');
    if (!createEl) {
      return;
    }
    createEl.classList.remove('is-open');
    var body = createEl.querySelector('.remba-create__body');
    var toggle = createEl.querySelector('.remba-create__toggle');
    if (body) {
      body.hidden = true;
    }
    if (toggle) {
      toggle.setAttribute('aria-expanded', 'false');
      setExpandChevron(toggle.querySelector('.remba-menu__chevron'), false);
    }
  }

  function renderCreateSection(container, item, root, config) {
    var wasOpen = container.classList.contains('is-open');
    container.innerHTML = '';
    container.classList.toggle('is-open', wasOpen);

    var toggle = document.createElement('button');
    toggle.type = 'button';
    toggle.className = 'remba-create__toggle';
    toggle.setAttribute('aria-expanded', wasOpen ? 'true' : 'false');
    toggle.innerHTML =
      '<span>Create list</span><span class="remba-menu__chevron" aria-hidden="true">' +
      (wasOpen ? CHEVRON_UP : CHEVRON_DOWN) +
      '</span>';

    var body = document.createElement('div');
    body.className = 'remba-create__body';
    body.hidden = !wasOpen;

    var types = document.createElement('div');
    types.className = 'remba-create__types';
    body.appendChild(types);

    toggle.addEventListener('click', function (event) {
      event.preventDefault();
      event.stopPropagation();
      var open = body.hidden;
      if (open) {
        closeAddSection(root);
      }
      body.hidden = !open;
      container.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      setExpandChevron(toggle.querySelector('.remba-menu__chevron'), open);
    });

    container.appendChild(toggle);
    container.appendChild(body);

    orderedListTypes(item).forEach(function (type) {
      var row = document.createElement('div');
      row.className = 'remba-create-row';

      var main = document.createElement('div');
      main.className = 'remba-create-row__main';

      var label = document.createElement('span');
      label.className = 'remba-list-name';
      label.textContent = type.label;

      var expand = document.createElement('button');
      expand.type = 'button';
      expand.className = 'remba-expand';
      expand.setAttribute('aria-label', 'Name a new ' + type.label + ' list');
      expand.setAttribute('aria-expanded', 'false');
      expand.textContent = CHEVRON_DOWN;

      var form = document.createElement('div');
      form.className = 'remba-create-row__form';
      form.hidden = true;

      var input = document.createElement('input');
      input.className = 'remba-create-input';
      input.type = 'text';
      input.maxLength = 120;
      input.placeholder = 'List name';
      input.value = type.label;
      input.setAttribute('aria-label', type.label + ' list name');

      var add = document.createElement('button');
      add.type = 'button';
      add.className = 'remba-add';
      add.setAttribute('aria-label', 'Create ' + type.label + ' list and add item');
      add.textContent = '+';

      function closeForm() {
        row.classList.remove('is-open');
        form.hidden = true;
        expand.setAttribute('aria-expanded', 'false');
        setExpandChevron(expand, false);
      }

      function openForm() {
        types.querySelectorAll('.remba-create-row.is-open').forEach(function (openRow) {
          if (openRow !== row) {
            openRow.classList.remove('is-open');
            var openFormEl = openRow.querySelector('.remba-create-row__form');
            var openExpand = openRow.querySelector('.remba-expand');
            if (openFormEl) openFormEl.hidden = true;
            if (openExpand) {
              openExpand.setAttribute('aria-expanded', 'false');
              setExpandChevron(openExpand, false);
            }
          }
        });
        row.classList.add('is-open');
        form.hidden = false;
        expand.setAttribute('aria-expanded', 'true');
        setExpandChevron(expand, true);
        input.focus();
        input.select();
      }

      expand.addEventListener('click', function (event) {
        event.preventDefault();
        event.stopPropagation();
        if (form.hidden) {
          openForm();
        } else {
          closeForm();
        }
      });

      add.addEventListener('click', function (event) {
        event.preventDefault();
        event.stopPropagation();
        if (add.disabled) {
          return;
        }
        createListAndAddItem(root, config, item, type.key, input.value, add);
      });

      input.addEventListener('keydown', function (event) {
        if (event.key === 'Enter') {
          event.preventDefault();
          add.click();
        }
        if (event.key === 'Escape') {
          event.preventDefault();
          closeForm();
        }
      });

      main.appendChild(label);
      main.appendChild(expand);
      form.appendChild(input);
      form.appendChild(add);
      row.appendChild(main);
      row.appendChild(form);
      types.appendChild(row);
    });
  }

  function membershipQuery(item) {
    var params = [];
    if (item.idType && item.id) {
      params.push('identifierType=' + encodeURIComponent(item.idType));
      params.push('identifierValue=' + encodeURIComponent(item.id));
    }
    if (item.entityId) {
      params.push('entityId=' + encodeURIComponent(item.entityId));
    }
    return params.length ? params.join('&') : '';
  }

  function fetchContainingListIds(root, config, item) {
    var query = membershipQuery(item);
    if (!query) {
      return Promise.resolve([]);
    }

    return apiFetch(root, config, '/api/lists/containing?' + query)
      .then(function (listIds) {
        return Array.isArray(listIds) ? listIds : [];
      })
      .catch(function () {
        return [];
      });
  }

  function markListButtonPresent(button, listName) {
    if (!button) {
      return;
    }
    button.disabled = true;
    button.textContent = '✓';
    button.setAttribute('aria-label', 'Already on ' + (listName || 'list'));
    button.classList.add('is-on-list');
  }

  function renderLists(submenu, lists, item, root, config, containingIds) {
    submenu.innerHTML = '';
    containingIds = containingIds || [];
    var containing = {};
    containingIds.forEach(function (id) {
      if (id) {
        containing[String(id)] = true;
      }
    });

    if (!lists.length) {
      var empty = document.createElement('div');
      empty.className = 'remba-status';
      empty.textContent = 'No lists yet — create one below.';
      submenu.appendChild(empty);
      return;
    }

    sortListsForItem(lists, item).forEach(function (list) {
      var row = document.createElement('div');
      row.className = 'remba-list-row';

      var name = document.createElement('span');
      name.className = 'remba-list-name';
      name.title = list.name || '';
      name.textContent = list.name || 'Untitled list';

      var add = document.createElement('button');
      add.type = 'button';
      add.className = 'remba-add';
      add.setAttribute('aria-label', 'Add to ' + (list.name || 'list'));
      add.textContent = '+';

      if (list.id && containing[String(list.id)]) {
        markListButtonPresent(add, list.name);
      } else {
        add.addEventListener('click', function (event) {
          event.preventDefault();
          event.stopPropagation();
          if (add.disabled) {
            return;
          }
          saveItemToList(root, config, item, list.id, list.name, add).catch(function () {
            // status already set
          });
        });
      }

      row.appendChild(name);
      row.appendChild(add);
      submenu.appendChild(row);
    });
  }

  function loadLists(root, item, config) {
    var submenu = root.querySelector('.remba-submenu');
    var createEl = root.querySelector('.remba-create');
    var statusEl = root.querySelector('.remba-status');
    var trigger = root.querySelector('.remba-menu__item--add');

    closeCreateSection(root);
    if (trigger) {
      trigger.classList.add('is-open');
      trigger.setAttribute('aria-expanded', 'true');
      setExpandChevron(trigger.querySelector('.remba-menu__chevron'), true);
    }
    submenu.hidden = false;
    submenu.innerHTML = '';
    setStatus(statusEl, '', false);
    setLoading(root, true, item.id ? 'Looking up item…' : 'Loading your lists…');
    if (createEl) {
      renderCreateSection(createEl, item, root, config);
    }

    function shellIsActive() {
      var shellEl = root.querySelector('.remba-shell');
      return root.classList.contains('is-open') && !(shellEl && shellEl._rembaClosing);
    }

    var prepare = item.entityType && item.id
      ? resolveItemRecord(root, config, item).then(function (resolved) {
          if (!shellIsActive()) {
            return item;
          }
          Object.keys(resolved).forEach(function (key) {
            item[key] = resolved[key];
          });
          setLoading(root, true, 'Loading your lists…');
          return item;
        })
      : Promise.resolve(item);

    prepare
      .then(function () {
        if (!shellIsActive()) {
          return null;
        }
        return Promise.all([
          apiFetch(root, config, '/api/lists'),
          fetchContainingListIds(root, config, item),
        ]);
      })
      .then(function (results) {
        if (!results || !shellIsActive()) {
          return;
        }
        var lists = results[0];
        var containingIds = results[1];
        renderLists(
          submenu,
          Array.isArray(lists) ? lists : [],
          item,
          root,
          config,
          containingIds,
        );
        if (createEl) {
          renderCreateSection(createEl, item, root, config);
        }
        setLoading(root, false);
      })
      .catch(function (error) {
        if (!shellIsActive()) {
          return;
        }
        submenu.innerHTML = '';
        setLoading(root, false);
        setStatus(statusEl, (error && error.message) || 'Could not load lists.', true);
      });
  }

  function setShellChromeState(shell, open) {
    if (!shell) {
      return;
    }
    var closeBtn = shell.querySelector('.remba-shell__close');
    shell.setAttribute('aria-expanded', open ? 'true' : 'false');
    if (open) {
      shell.setAttribute('role', 'dialog');
      shell.setAttribute('aria-label', 'Remba');
      shell.removeAttribute('tabindex');
      if (closeBtn) {
        closeBtn.tabIndex = 0;
      }
    } else {
      shell.setAttribute('role', 'button');
      shell.setAttribute('aria-label', 'Save to Remba');
      shell.tabIndex = 0;
      if (closeBtn) {
        closeBtn.tabIndex = -1;
      }
    }
  }

  function collapse(root) {
    var shell = root.querySelector('.remba-shell');
    var theme = root._rembaTheme;
    if (!root.classList.contains('is-open') || !shell) {
      root.classList.remove('is-open');
      if (shell) {
        shell.classList.remove('is-open');
        setShellChromeState(shell, false);
      }
      syncMark(root, theme, false);
      clearShellElevation(root);
      resetShellContent(root);
      return;
    }

    if (shell._rembaClosing) {
      return;
    }
    shell._rembaClosing = true;
    dismissLoading(root);

    var mark = shell.querySelector('.remba-mark');
    flipMark(mark, function () {
      syncMark(root, theme, false);
      setShellChromeState(shell, false);
      root.classList.remove('is-open');
      shell.classList.remove('is-open');
    });

    var finished = false;
    function finish() {
      if (finished) {
        return;
      }
      finished = true;
      shell._rembaClosing = false;
      shell.removeEventListener('transitionend', onEnd);
      clearShellElevation(root);
      resetShellContent(root);
    }

    function onEnd(event) {
      if (event.target !== shell) {
        return;
      }
      if (event.propertyName && event.propertyName !== 'width' && event.propertyName !== 'max-height') {
        return;
      }
      finish();
    }

    shell.addEventListener('transitionend', onEnd);
    window.setTimeout(finish, MORPH_MS + 80);
  }

  function expand(root, item, config) {
    var shell = root.querySelector('.remba-shell');
    if (!shell || shell._rembaClosing) {
      return;
    }

    applyShellAnchor(shell, resolveAnchor(root));
    elevateOpenShell(root);

    var mark = shell.querySelector('.remba-mark');
    flipMark(mark, function () {
      root.classList.add('is-open');
      setShellChromeState(shell, true);
      syncMark(root, root._rembaTheme, true);
      shell.classList.add('is-open');
    });

    loadLists(root, item, config);
  }

  function markImg(appUrl, theme) {
    return (
      '<img class="remba-mark" src="' +
      markIconSrc(appUrl, theme) +
      '" alt="" width="24" height="24" decoding="async" />'
    );
  }

  function buildRootHtml(appUrl, theme) {
    return [
      '<div class="remba-shell remba-shell--tl" role="button" tabindex="0"',
      ' aria-label="Save to Remba" aria-haspopup="dialog" aria-expanded="false">',
      '<div class="remba-shell__chrome">',
      '<div class="remba-shell__brand">',
      markImg(appUrl, theme),
      '<span class="remba-word" aria-hidden="true">remba<span class="remba-word__dot">.</span>it</span>',
      '</div>',
      '<button type="button" class="remba-shell__close" aria-label="Close" tabindex="-1">×</button>',
      '</div>',
      '<div class="remba-shell__body">',
      '<div class="remba-shell__body-inner">',
      '<button type="button" class="remba-menu__item remba-menu__item--add" aria-haspopup="menu" aria-expanded="false">',
      '<span>Add to List</span><span class="remba-menu__chevron" aria-hidden="true">' + CHEVRON_DOWN + '</span>',
      '</button>',
      '<div class="remba-submenu" role="menu" hidden></div>',
      '<div class="remba-divider" aria-hidden="true"></div>',
      '<div class="remba-create"></div>',
      '<iframe class="remba-auth" title="Remba sign-in" hidden></iframe>',
      '<div class="remba-status" hidden></div>',
      '</div>',
      '</div>',
      '<div class="remba-loading" hidden>',
      '<span class="remba-spinner" aria-hidden="true"></span>',
      '<span class="remba-loading__label">Loading…</span>',
      '</div>',
      '</div>',
    ].join('');
  }

  function unmount(el) {
    if (!el) {
      return;
    }
    collapse(el);
    el.removeAttribute('data-remba-mounted');
    el.classList.remove('remba-root', 'is-open');
    el.innerHTML = '';
  }

  function mount(el, overrides) {
    if (!el) {
      return el;
    }

    overrides = overrides || {};
    if (el.getAttribute('data-remba-mounted') === 'true') {
      if (!overrides.force) {
        return el;
      }
      unmount(el);
    }

    var config = Object.assign({}, scriptConfig(), overrides.config || {});
    var localApp = readOption(el, 'app-url');
    var localApi = readOption(el, 'api-url');
    if (localApp) config.appUrl = localApp.replace(/\/$/, '');
    if (localApi) config.apiUrl = localApi.replace(/\/$/, '');

    ensureStyles(config.appUrl);

    var item = buildItem(el, overrides);
    var theme = resolveTheme(config, el);

    el.classList.add('remba-root');
    el.innerHTML = buildRootHtml(config.appUrl, theme);
    applyTheme(el, theme);
    el._rembaTheme = theme;
    el._rembaAppUrl = config.appUrl;
    el.setAttribute('data-remba-mounted', 'true');

    var shell = el.querySelector('.remba-shell');
    var addItem = el.querySelector('.remba-menu__item--add');
    var closeBtn = el.querySelector('.remba-shell__close');

    function openFromTrigger() {
      if (!itemIsReady(item)) {
        applyShellAnchor(shell, resolveAnchor(el));
        el.classList.add('is-open');
        shell.classList.add('is-open');
        elevateOpenShell(el);
        setShellChromeState(shell, true);
        syncMark(el, theme, true);
        setStatus(
          el.querySelector('.remba-status'),
          'Embed needs data-entity-type + data-id, data-item JSON, or data-name.',
          true,
        );
        return;
      }
      if (el.classList.contains('is-open')) {
        return;
      }
      expand(el, item, config);
    }

    shell.addEventListener('click', function (event) {
      if (el.classList.contains('is-open')) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      openFromTrigger();
    });

    shell.addEventListener('keydown', function (event) {
      if (el.classList.contains('is-open')) {
        return;
      }
      if (event.key !== 'Enter' && event.key !== ' ') {
        return;
      }
      event.preventDefault();
      openFromTrigger();
    });

    if (closeBtn) {
      closeBtn.addEventListener('click', function (event) {
        event.preventDefault();
        event.stopPropagation();
        collapse(el);
      });
    }

    addItem.addEventListener('click', function (event) {
      event.preventDefault();
      event.stopPropagation();
      var submenu = el.querySelector('.remba-submenu');
      if (!submenu.hidden && !shell.classList.contains('is-busy')) {
        submenu.hidden = true;
        addItem.classList.remove('is-open');
        addItem.setAttribute('aria-expanded', 'false');
        setExpandChevron(addItem.querySelector('.remba-menu__chevron'), false);
        return;
      }
      loadLists(el, item, config);
    });

    return el;
  }

  function init(root) {
    var scope = root || document;
    var nodes = scope.querySelectorAll('[' + ATTR + ']');
    for (var i = 0; i < nodes.length; i += 1) {
      mount(nodes[i]);
    }
  }

  document.addEventListener('click', function (event) {
    var openRoots = document.querySelectorAll('.remba-root.is-open');
    for (var i = 0; i < openRoots.length; i += 1) {
      if (!openRoots[i].contains(event.target)) {
        collapse(openRoots[i]);
      }
    }
  });

  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape') {
      return;
    }
    var openRoots = document.querySelectorAll('.remba-root.is-open');
    for (var i = 0; i < openRoots.length; i += 1) {
      collapse(openRoots[i]);
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      init();
    });
  } else {
    init();
  }

  global.RembaSave = {
    mount: mount,
    unmount: unmount,
    init: init,
    themes: THEMES,
  };
})(typeof window !== 'undefined' ? window : this);
