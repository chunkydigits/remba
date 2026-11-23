(function (global) {
  'use strict';

  var STYLE_ID = 'remba-save-styles-v2';
  var ATTR = 'data-remba-save';
  var tokenCache = null;

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
      buttonColor: background,
      buttonBackground: color,
    };
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
      '.remba-save-button__mark{display:block;width:1.45rem;height:1.45rem;overflow:visible}',
      '.remba-save-button__mark .remba-mark-body{fill:currentColor !important}',
      '.remba-save-button__mark .remba-mark-accent,.remba-save-button__mark .remba-mark-accent *{fill:#17b07a !important}',
      '.remba-panel{margin-top:.45rem;min-width:15rem;max-width:20rem;min-height:7.5rem;padding:.35rem;',
      'border:1px solid var(--remba-border);border-radius:.75rem;background:var(--remba-bg);',
      'color:var(--remba-color);box-shadow:0 10px 28px rgba(0,0,0,.14),0 2px 6px rgba(0,0,0,.06);',
      'position:relative}',
      '.remba-panel[hidden]{display:none!important}',
      '.remba-menu__item{display:flex;align-items:center;justify-content:space-between;gap:.75rem;',
      'width:100%;border:0;border-radius:.5rem;background:transparent;color:inherit;',
      'padding:.55rem .65rem;cursor:pointer;text-align:left;font:inherit}',
      '.remba-menu__item:hover,.remba-menu__item.is-open{background:var(--remba-hover)}',
      '.remba-menu__chevron{opacity:.55}',
      '.remba-submenu{margin-top:.2rem;padding:.25rem;border-radius:.55rem;background:var(--remba-hover);',
      'min-height:3rem}',
      '.remba-submenu[hidden]{display:none!important}',
      '.remba-list-row{display:flex;align-items:center;gap:.35rem;min-height:2.1rem;',
      'padding:.15rem .25rem .15rem .55rem;border-radius:.45rem}',
      '.remba-list-row:hover{background:var(--remba-hover)}',
      '.remba-list-name{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}',
      '.remba-add{appearance:none;flex:0 0 auto;width:1.75rem;height:1.75rem;border:0;',
      'border-radius:999px;display:grid;place-items:center;background:var(--remba-hover);',
      'color:var(--remba-color);cursor:pointer;font:700 1rem/1 system-ui,sans-serif;padding:0}',
      '.remba-add:hover:not(:disabled){background:var(--remba-button-bg);color:var(--remba-button-color)}',
      '.remba-add:disabled{opacity:.85;cursor:default}',
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
      '.remba-panel.is-loading .remba-menu__item,.remba-panel.is-loading .remba-submenu{visibility:hidden}',
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

  function setLoading(root, loading, message) {
    var panel = root.querySelector('.remba-panel');
    var loadingEl = root.querySelector('.remba-loading');
    var label = root.querySelector('.remba-loading__label');
    if (!panel || !loadingEl) {
      return;
    }
    panel.classList.toggle('is-loading', !!loading);
    loadingEl.hidden = !loading;
    if (label) {
      label.textContent = message || 'Loading…';
    }
  }

  function requestAccessToken(root, config) {
    var cached = getCachedToken();
    if (cached) {
      return Promise.resolve(cached);
    }

    return new Promise(function (resolve, reject) {
      var authFrame = root.querySelector('.remba-auth');
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
          reject(error);
          return;
        }
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
      // Keep the inline auth frame visible (spinner would cover the sign-in UI).
      setLoading(root, false);
      setStatus(root.querySelector('.remba-status'), 'Signing in…', false);
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

  function filterListsForItem(lists, item) {
    if (!item.entityType) {
      return lists;
    }
    return lists.filter(function (list) {
      var listType = String(list.entityType || '').trim().toLowerCase();
      return !listType || listType === item.entityType;
    });
  }

  function renderLists(submenu, lists, item, root, config, statusEl) {
    submenu.innerHTML = '';

    if (!lists.length) {
      var empty = document.createElement('div');
      empty.className = 'remba-status';
      empty.textContent = item.entityType
        ? 'No matching ' + item.entityType + ' lists. Create one in Remba first.'
        : 'No lists yet. Create one in Remba first.';
      submenu.appendChild(empty);
      return;
    }

    lists
      .slice()
      .sort(function (a, b) {
        return String(a.name || '').localeCompare(String(b.name || ''));
      })
      .forEach(function (list) {
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

        add.addEventListener('click', function (event) {
          event.preventDefault();
          event.stopPropagation();
          if (add.disabled) {
            return;
          }

          add.disabled = true;
          add.innerHTML = '<span class="remba-spinner" aria-hidden="true"></span>';
          setStatus(statusEl, '', false);
          setLoading(root, true, 'Saving…');

          resolveItemRecord(root, config, item)
            .then(function (resolved) {
              return apiFetch(
                root,
                config,
                '/api/lists/' + encodeURIComponent(list.id) + '/entries',
                {
                  method: 'POST',
                  body: buildCreateBody(resolved),
                },
              ).then(function () {
                return resolved;
              });
            })
            .then(function (resolved) {
              if (!resolved.referral) {
                return null;
              }
              return apiFetch(root, config, '/api/lists/' + encodeURIComponent(list.id), {
                method: 'PUT',
                body: { referralLink: resolved.referral },
              }).catch(function () {
                return null;
              });
            })
            .then(function () {
              setLoading(root, false);
              add.textContent = '✓';
              setStatus(statusEl, 'Added to “' + (list.name || 'list') + '”.', false);
            })
            .catch(function (error) {
              setLoading(root, false);
              add.disabled = false;
              add.textContent = '+';
              setStatus(statusEl, (error && error.message) || 'Could not add item.', true);
            });
        });

        row.appendChild(name);
        row.appendChild(add);
        submenu.appendChild(row);
      });
  }

  function loadLists(root, item, config) {
    var submenu = root.querySelector('.remba-submenu');
    var statusEl = root.querySelector('.remba-status');
    var trigger = root.querySelector('.remba-menu__item');

    trigger.classList.add('is-open');
    trigger.setAttribute('aria-expanded', 'true');
    submenu.hidden = false;
    submenu.innerHTML = '';
    setStatus(statusEl, '', false);
    setLoading(root, true, item.id ? 'Looking up item…' : 'Loading your lists…');

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
        return apiFetch(root, config, '/api/lists');
      })
      .then(function (lists) {
        var filtered = filterListsForItem(Array.isArray(lists) ? lists : [], item);
        renderLists(submenu, filtered, item, root, config, statusEl);
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
    var trigger = root.querySelector('.remba-menu__item');
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
    if (trigger) {
      trigger.classList.remove('is-open');
      trigger.setAttribute('aria-expanded', 'false');
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

  function markSvg() {
    // Remba R mark (list bullets + bowl/leg). Inline SVG so the icon always paints.
    return [
      '<svg class="remba-save-button__mark" viewBox="0 0 64 64" aria-hidden="true" focusable="false">',
      '<path class="remba-mark-body" d="M30 8h16c9.9 0 18 8.1 18 18 0 7.4-4.5 13.8-10.9 16.5L64 56H47.2L36.5 40H30v16h-8V8h8zm0 8v16h7.8c4.5 0 8.2-3.7 8.2-8.2S42.3 16 37.8 16H30z"/>',
      '<g class="remba-mark-accent">',
      '<circle cx="7" cy="16" r="3"/>',
      '<rect x="13" y="13" width="13" height="6" rx="3"/>',
      '<circle cx="7" cy="32" r="3"/>',
      '<rect x="13" y="29" width="13" height="6" rx="3"/>',
      '<circle cx="7" cy="48" r="3"/>',
      '<rect x="13" y="45" width="13" height="6" rx="3"/>',
      '</g>',
      '</svg>',
    ].join('');
  }

  function buildRootHtml() {
    return [
      '<button type="button" class="remba-save-button" aria-label="Save to Remba" aria-haspopup="true" aria-expanded="false">',
      markSvg(),
      '</button>',
      '<div class="remba-panel" role="menu" aria-label="Remba" hidden>',
      '<div class="remba-loading" hidden>',
      '<span class="remba-spinner" aria-hidden="true"></span>',
      '<span class="remba-loading__label">Loading…</span>',
      '</div>',
      '<button type="button" class="remba-menu__item" role="menuitem" aria-haspopup="menu" aria-expanded="false">',
      '<span>Add to List</span><span class="remba-menu__chevron" aria-hidden="true">›</span>',
      '</button>',
      '<div class="remba-submenu" role="menu" hidden></div>',
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
    el.innerHTML = buildRootHtml();
    applyTheme(el, theme);
    el.setAttribute('data-remba-mounted', 'true');

    var button = el.querySelector('.remba-save-button');
    var addItem = el.querySelector('.remba-menu__item');

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

    addItem.addEventListener('click', function (event) {
      event.preventDefault();
      event.stopPropagation();
      var submenu = el.querySelector('.remba-submenu');
      if (!submenu.hidden && !el.querySelector('.remba-panel').classList.contains('is-loading')) {
        submenu.hidden = true;
        addItem.classList.remove('is-open');
        addItem.setAttribute('aria-expanded', 'false');
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
