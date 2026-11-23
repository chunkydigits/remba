(function (global) {
  'use strict';

  var STYLE_ID = 'remba-save-styles-v10';
  var ATTR = 'data-remba-save';
  var CHEVRON_DOWN = '▾';
  var CHEVRON_UP = '▴';
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
      // Match panel theme so the word-icon asset stays visible.
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

  function usesDarkSurface(theme, background) {
    return theme.name === 'dark'
      || (theme.name === 'custom' && isDarkColor(background || theme.background));
  }

  function markIconSrc(appUrl, theme) {
    // Same assets as the app header: *-dark is the light artwork for dark surfaces.
    var file = usesDarkSurface(theme, theme.buttonBackground)
      ? 'word-icon-transparent-dark.png'
      : 'word-icon-transparent.png';
    return String(appUrl || '').replace(/\/$/, '') + '/assets/images/' + file;
  }

  function fullLogoSrc(appUrl, theme) {
    var file = usesDarkSurface(theme, theme.background)
      ? 'logo-transparent-dark.png'
      : 'logo-transparent-light.png';
    return String(appUrl || '').replace(/\/$/, '') + '/assets/images/' + file;
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

  function ensureStyles() {
    if (document.getElementById(STYLE_ID)) {
      return;
    }

    var style = document.createElement('style');
    style.id = STYLE_ID;
    style.textContent = [
      '.remba-root{position:relative;display:inline-block;max-width:100%;',
      'font:500 .9rem/1.25 system-ui,-apple-system,Segoe UI,sans-serif;',
      'color:var(--remba-color,#111);--remba-bg:#fff;--remba-border:rgba(0,0,0,.12);',
      '--remba-muted:#666;--remba-hover:rgba(0,0,0,.06);--remba-danger:#b00020;',
      '--remba-button-color:#fff;--remba-button-bg:#111}',
      '.remba-save-button{appearance:none;border:1px solid var(--remba-border);border-radius:999px;',
      'width:2.5rem;height:2.5rem;padding:0;background:var(--remba-button-bg);',
      'color:var(--remba-button-color);cursor:pointer;display:inline-grid;place-items:center;',
      'box-shadow:0 1px 2px rgba(0,0,0,.14);line-height:0}',
      '.remba-save-button:hover,.remba-root.is-open .remba-save-button{filter:brightness(1.08)}',
      '.remba-save-button:focus-visible{outline:2px solid var(--remba-button-bg);outline-offset:2px}',
      '.remba-save-button__mark{display:block;width:1.55rem;height:1.55rem;object-fit:contain}',
      '.remba-panel{margin-top:.45rem;min-width:18.5rem;width:min(22.5rem,calc(100vw - 2rem));',
      'min-height:9rem;padding:.85rem;border:1px solid var(--remba-border);border-radius:.85rem;',
      'background:var(--remba-bg);color:var(--remba-color);',
      'box-shadow:0 10px 28px rgba(0,0,0,.14),0 2px 6px rgba(0,0,0,.06);position:relative;',
      'box-sizing:border-box}',
      '.remba-panel[hidden]{display:none!important}',
      '.remba-panel__header{display:flex;align-items:center;justify-content:space-between;',
      'gap:.75rem;min-height:2rem;margin:0 0 .7rem;padding:0 .05rem}',
      '.remba-panel__brand{display:block;height:1.45rem;width:auto;max-width:100%;',
      'object-fit:contain;object-position:left center;flex:1;min-width:0}',
      '.remba-panel__close{appearance:none;position:relative;z-index:3;flex:0 0 auto;',
      'width:1.85rem;height:1.85rem;padding:0;border:0;border-radius:.5rem;cursor:pointer;',
      'display:grid;place-items:center;background:var(--remba-hover);color:var(--remba-muted);',
      'font:600 1.2rem/1 system-ui,sans-serif}',
      '.remba-panel__close:hover{background:var(--remba-button-bg);color:var(--remba-button-color)}',
      '.remba-panel__close:focus-visible{outline:2px solid var(--remba-color);outline-offset:1px}',
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
      'justify-content:center;gap:.65rem;padding:1rem;border-radius:.75rem;',
      'background:color-mix(in srgb, var(--remba-bg) 92%, transparent);color:var(--remba-muted);',
      'font-size:.82rem;text-align:center;z-index:2}',
      '.remba-loading[hidden]{display:none!important}',
      '.remba-spinner{width:1.35rem;height:1.35rem;border-radius:999px;',
      'border:2px solid var(--remba-border);border-top-color:var(--remba-color);',
      'animation:remba-spin .7s linear infinite}',
      '@keyframes remba-spin{to{transform:rotate(360deg)}}',
      '.remba-panel.is-busy .remba-menu__item,.remba-panel.is-busy .remba-submenu,',
      '.remba-panel.is-busy .remba-divider,.remba-panel.is-busy .remba-create{visibility:hidden}',
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
    var panel = root.querySelector('.remba-panel');
    if (panel) {
      panel.classList.toggle('is-busy', !!busy);
    }
  }

  function setLoading(root, loading, message) {
    var loadingEl = root.querySelector('.remba-loading');
    var label = root.querySelector('.remba-loading__label');
    var authFrame = root.querySelector('.remba-auth');
    if (!loadingEl) {
      return;
    }

    setBusy(root, !!loading);
    loadingEl.hidden = !loading;
    if (loading && authFrame) {
      authFrame.hidden = true;
    }
    if (label) {
      label.textContent = message || 'Loading…';
    }
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

    var prepare = item.entityType && item.id
      ? resolveItemRecord(root, config, item).then(function (resolved) {
          Object.keys(resolved).forEach(function (key) {
            item[key] = resolved[key];
          });
          setLoading(root, true, 'Loading your lists…');
          return item;
        })
      : Promise.resolve(item);

    prepare
      .then(function () {
        return Promise.all([
          apiFetch(root, config, '/api/lists'),
          fetchContainingListIds(root, config, item),
        ]);
      })
      .then(function (results) {
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
        submenu.innerHTML = '';
        setLoading(root, false);
        setStatus(statusEl, (error && error.message) || 'Could not load lists.', true);
      });
  }

  function collapse(root) {
    root.classList.remove('is-open');
    var button = root.querySelector('.remba-save-button');
    var panel = root.querySelector('.remba-panel');
    var submenu = root.querySelector('.remba-submenu');
    var createEl = root.querySelector('.remba-create');
    var trigger = root.querySelector('.remba-menu__item--add');
    var authFrame = root.querySelector('.remba-auth');
    var statusEl = root.querySelector('.remba-status');

    if (button) {
      button.setAttribute('aria-expanded', 'false');
    }
    if (panel) {
      panel.hidden = true;
    }
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

  function expand(root, item, config) {
    root.classList.add('is-open');
    var button = root.querySelector('.remba-save-button');
    var panel = root.querySelector('.remba-panel');
    if (button) {
      button.setAttribute('aria-expanded', 'true');
    }
    if (panel) {
      panel.hidden = false;
    }
    loadLists(root, item, config);
  }

  function markImg(appUrl, theme) {
    return (
      '<img class="remba-save-button__mark" src="' +
      markIconSrc(appUrl, theme) +
      '" alt="" width="24" height="24" decoding="async" />'
    );
  }

  function brandImg(appUrl, theme) {
    return (
      '<img class="remba-panel__brand" src="' +
      fullLogoSrc(appUrl, theme) +
      '" alt="Remba" decoding="async" />'
    );
  }

  function buildRootHtml(appUrl, theme) {
    return [
      '<button type="button" class="remba-save-button" aria-label="Save to Remba" aria-haspopup="true" aria-expanded="false">',
      markImg(appUrl, theme),
      '</button>',
      '<div class="remba-panel" role="menu" aria-label="Remba" hidden>',
      '<div class="remba-panel__header">',
      brandImg(appUrl, theme),
      '<button type="button" class="remba-panel__close" aria-label="Close">×</button>',
      '</div>',
      '<div class="remba-loading" hidden>',
      '<span class="remba-spinner" aria-hidden="true"></span>',
      '<span class="remba-loading__label">Loading…</span>',
      '</div>',
      '<button type="button" class="remba-menu__item remba-menu__item--add" role="menuitem" aria-haspopup="menu" aria-expanded="false">',
      '<span>Add to List</span><span class="remba-menu__chevron" aria-hidden="true">' + CHEVRON_DOWN + '</span>',
      '</button>',
      '<div class="remba-submenu" role="menu" hidden></div>',
      '<div class="remba-divider" aria-hidden="true"></div>',
      '<div class="remba-create"></div>',
      '<iframe class="remba-auth" title="Remba sign-in" hidden></iframe>',
      '<div class="remba-status" hidden></div>',
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

    ensureStyles();

    var config = Object.assign({}, scriptConfig(), overrides.config || {});
    var localApp = readOption(el, 'app-url');
    var localApi = readOption(el, 'api-url');
    if (localApp) config.appUrl = localApp.replace(/\/$/, '');
    if (localApi) config.apiUrl = localApi.replace(/\/$/, '');

    var item = buildItem(el, overrides);
    var theme = resolveTheme(config, el);

    el.classList.add('remba-root');
    el.innerHTML = buildRootHtml(config.appUrl, theme);
    applyTheme(el, theme);
    el.setAttribute('data-remba-mounted', 'true');

    var button = el.querySelector('.remba-save-button');
    var addItem = el.querySelector('.remba-menu__item--add');
    var closeBtn = el.querySelector('.remba-panel__close');

    button.addEventListener('click', function (event) {
      event.preventDefault();
      event.stopPropagation();
      if (!itemIsReady(item)) {
        el.querySelector('.remba-panel').hidden = false;
        el.classList.add('is-open');
        button.setAttribute('aria-expanded', 'true');
        setStatus(
          el.querySelector('.remba-status'),
          'Embed needs data-entity-type + data-id, data-item JSON, or data-name.',
          true,
        );
        return;
      }
      if (el.classList.contains('is-open')) {
        collapse(el);
        return;
      }
      expand(el, item, config);
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
      if (!submenu.hidden && !el.querySelector('.remba-panel').classList.contains('is-busy')) {
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
