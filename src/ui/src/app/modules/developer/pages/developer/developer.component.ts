import { NgStyle } from '@angular/common';
import {
  afterNextRender,
  Component,
  ElementRef,
  OnDestroy,
  inject,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { environment } from '../../../../../environments/environment';

declare global {
  interface Window {
    RembaSave?: {
      mount: (el: HTMLElement, overrides?: { force?: boolean }) => HTMLElement;
      unmount: (el: HTMLElement) => void;
      init: (root?: ParentNode) => void;
    };
  }
}

type EmbedMode = 'identifier' | 'json' | 'manual';

/** Bump when embed script behaviour/markup changes so browsers fetch a fresh copy. */
const EMBED_SCRIPT_VERSION = '18';

@Component({
  selector: 'app-developer',
  imports: [NgStyle, ReactiveFormsModule, MaterialModule, PageTemplateComponent],
  templateUrl: './developer.component.html',
  styleUrl: './developer.component.scss',
})
export class DeveloperComponent implements OnDestroy {
  private readonly formBuilder = inject(FormBuilder);
  private readonly previewHost = viewChild<ElementRef<HTMLElement>>('previewHost');
  private readonly playgroundHost = viewChild<ElementRef<HTMLElement>>('playgroundHost');
  private remountTimer: number | null = null;
  private embedReady: Promise<void> | null = null;

  readonly configForm = this.formBuilder.nonNullable.group({
    theme: ['light'],
    previewBackdrop: ['light'],
    color: ['#111111'],
    background: ['#ffffff'],
    useOptionalColors: [false],
    border: ['#d4d4d4'],
    muted: ['#666666'],
    entityType: ['book'],
    id: ['9780441172719'],
    idType: [''],
    name: ['The Left Hand of Darkness'],
    author: ['Ursula K. Le Guin'],
    link: ['https://example.com/books/left-hand-of-darkness'],
    mode: ['identifier' as EmbedMode],
  });

  snippet = '';
  jsonExample = '';

  constructor() {
    afterNextRender(() => {
      this.ensureEmbedScript();
      this.configForm.valueChanges.subscribe(() => this.scheduleRemount());
      this.scheduleRemount();
    });
  }

  ngOnDestroy(): void {
    if (this.remountTimer != null) {
      window.clearTimeout(this.remountTimer);
    }
    this.unmountAllEmbeds();
  }

  private unmountAllEmbeds(): void {
    if (!window.RembaSave) {
      return;
    }
    const hosts = [
      this.previewHost()?.nativeElement,
      this.playgroundHost()?.nativeElement,
    ];
    for (const host of hosts) {
      if (!host) {
        continue;
      }
      host.querySelectorAll('[data-remba-save]').forEach((node) => {
        window.RembaSave?.unmount(node as HTMLElement);
      });
    }
  }

  get customTheme(): boolean {
    return this.configForm.controls.theme.value === 'custom';
  }

  get previewBackdropStyle(): Record<string, string> {
    const dark = this.configForm.controls.previewBackdrop.value === 'dark';
    return {
      '--preview-bg': dark ? '#111' : '#fff',
      '--preview-fg': dark ? '#f4f4f4' : '#111',
    };
  }

  private scheduleRemount(): void {
    if (this.remountTimer != null) {
      window.clearTimeout(this.remountTimer);
    }
    this.remountTimer = window.setTimeout(() => this.remountPreview(), 80);
  }

  private embedScriptUrl(): string {
    return `/embed/remba.js?v=${EMBED_SCRIPT_VERSION}`;
  }

  private ensureEmbedScript(): Promise<void> {
    if (this.embedReady) {
      return this.embedReady;
    }

    this.embedReady = new Promise<void>((resolve, reject) => {
      const existing = document.getElementById('remba-embed-script');
      if (existing) {
        existing.remove();
        delete window.RembaSave;
      }

      const script = document.createElement('script');
      script.id = 'remba-embed-script';
      script.src = this.embedScriptUrl();
      script.async = true;
      script.setAttribute('data-api-url', environment.apiBaseUrl);
      script.setAttribute('data-app-url', window.location.origin);
      script.setAttribute('data-auth0-domain', environment.auth0.domain);
      script.setAttribute('data-auth0-client-id', environment.auth0.clientId);
      script.setAttribute('data-auth0-audience', environment.auth0.audience);
      script.setAttribute('data-theme', 'light');
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Failed to load remba.js'));
      document.body.appendChild(script);
    });

    return this.embedReady;
  }

  private remountPreview(): void {
    const value = this.configForm.getRawValue();
    this.jsonExample = this.buildJsonExample(value);
    this.snippet = this.buildSnippet(value);

    const host = this.previewHost()?.nativeElement;
    const playground = this.playgroundHost()?.nativeElement;
    if (!host && !playground) {
      return;
    }

    void this.ensureEmbedScript()
      .then(() => {
        if (!window.RembaSave) {
          throw new Error('RembaSave SDK missing after script load');
        }

        if (host) {
          host.replaceChildren();
          const mount = this.createEmbedMount(value);
          host.appendChild(mount);
          window.RembaSave.mount(mount, { force: true });
        }

        if (playground) {
          playground.querySelectorAll('[data-remba-save]').forEach((node) => {
            window.RembaSave?.unmount(node as HTMLElement);
            node.remove();
          });

          playground.querySelectorAll<HTMLElement>('[data-corner]').forEach((wrap) => {
            const mount = this.createEmbedMount(value);
            wrap.replaceChildren(mount);
            window.RembaSave?.mount(mount, { force: true });
          });
        }
      })
      .catch(() => {
        // Preview stays empty if the embed script fails to load.
      });
  }

  private createEmbedMount(
    value: ReturnType<DeveloperComponent['configForm']['getRawValue']>,
  ): HTMLElement {
    const mount = document.createElement('div');
    mount.setAttribute('data-remba-save', '');

    if (value.theme === 'custom') {
      mount.setAttribute('data-color', value.color);
      mount.setAttribute('data-background', value.background);
      if (value.useOptionalColors) {
        mount.setAttribute('data-border', value.border);
        mount.setAttribute('data-muted', value.muted);
      }
    } else {
      mount.setAttribute('data-theme', value.theme);
    }

    if (value.mode === 'json') {
      mount.setAttribute('data-item', this.buildJsonExample(value));
    } else if (value.mode === 'identifier') {
      if (value.entityType) mount.setAttribute('data-entity-type', value.entityType);
      if (value.id) mount.setAttribute('data-id', value.id);
      if (value.idType) mount.setAttribute('data-id-type', value.idType);
      if (value.name) mount.setAttribute('data-name', value.name);
      if (value.author) mount.setAttribute('data-author', value.author);
      if (value.link) mount.setAttribute('data-link', value.link);
    } else {
      if (value.name) mount.setAttribute('data-name', value.name);
      if (value.author) mount.setAttribute('data-author', value.author);
      if (value.link) mount.setAttribute('data-link', value.link);
    }

    return mount;
  }

  private buildSnippet(value: ReturnType<DeveloperComponent['configForm']['getRawValue']>): string {
    const attrs = [
      `src="${window.location.origin}/embed/remba.js?v=${EMBED_SCRIPT_VERSION}"`,
      `data-app-url="${window.location.origin}"`,
      `data-api-url="${environment.apiBaseUrl}"`,
    ];

    if (value.theme === 'custom') {
      attrs.push(`data-color="${value.color}"`);
      attrs.push(`data-background="${value.background}"`);
      if (value.useOptionalColors) {
        attrs.push(`data-border="${value.border}"`);
        attrs.push(`data-muted="${value.muted}"`);
      }
    } else {
      attrs.push(`data-theme="${value.theme}"`);
    }

    const divAttrs: string[] = ['data-remba-save'];
    if (value.mode === 'json') {
      divAttrs.push(`data-item='${this.buildJsonExample(value)}'`);
    } else if (value.mode === 'identifier') {
      if (value.entityType) divAttrs.push(`data-entity-type="${value.entityType}"`);
      if (value.id) divAttrs.push(`data-id="${value.id}"`);
      if (value.idType) divAttrs.push(`data-id-type="${value.idType}"`);
      if (value.name) divAttrs.push(`data-name="${value.name}"`);
      if (value.author) divAttrs.push(`data-author="${value.author}"`);
      if (value.link) divAttrs.push(`data-link="${value.link}"`);
    } else {
      if (value.name) divAttrs.push(`data-name="${value.name}"`);
      if (value.author) divAttrs.push(`data-author="${value.author}"`);
      if (value.link) divAttrs.push(`data-link="${value.link}"`);
    }

    return [
      '<script',
      ...attrs.map((attr) => `  ${attr}`),
      '  async',
      '></script>',
      '',
      '<div',
      ...divAttrs.map((attr) => `  ${attr}`),
      '></div>',
    ].join('\n');
  }

  private buildJsonExample(value: ReturnType<DeveloperComponent['configForm']['getRawValue']>): string {
    const payload: Record<string, string> = {};
    if (value.entityType) payload['entityType'] = value.entityType;
    if (value.id) payload['id'] = value.id;
    if (value.idType) payload['idType'] = value.idType;
    if (value.name) payload['name'] = value.name;
    if (value.author) payload['author'] = value.author;
    if (value.link) payload['link'] = value.link;
    return JSON.stringify(payload);
  }
}
