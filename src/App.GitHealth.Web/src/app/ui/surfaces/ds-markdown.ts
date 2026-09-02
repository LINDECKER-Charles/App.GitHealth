import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { parseMarkdown } from '../../core/markdown/markdown-blocks';

/**
 * Renders Markdown as elements, never as HTML. The text comes from a language model, so it
 * is parsed into values first and bound through ordinary interpolation, which escapes what
 * it prints. There is no `innerHTML` here, and there must not be one.
 *
 * Headings carry `role="heading"` with a level rather than being real `h1`…`h6`: an answer
 * lives inside a page that already has its own outline, and must not rewrite it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ds-markdown' },
  imports: [NgTemplateOutlet],
  selector: 'ds-markdown',
  styles: `
    :host {
      display: block;
      font: var(--type-body);
      color: var(--text-primary);
    }

    :host > :first-child {
      margin-top: 0;
    }

    :host > :last-child {
      margin-bottom: 0;
    }

    p,
    ul,
    ol,
    pre,
    blockquote,
    table {
      margin: 0 0 var(--space-3);
    }

    .md-heading {
      margin: var(--space-4) 0 var(--space-2);
      font: var(--type-subheading);
      color: var(--text-primary);
    }

    .md-heading--1,
    .md-heading--2 {
      font: var(--type-heading);
    }

    ul,
    ol {
      padding-left: var(--space-5);
    }

    li {
      margin-bottom: var(--space-1);
    }

    code {
      padding: 0 var(--space-1);
      border-radius: var(--radius-xs);
      background: var(--surface-code);
      font: var(--type-code-sm);
      color: var(--text-code);
    }

    pre {
      padding: var(--space-3);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-md);
      background: var(--surface-code);
      overflow-x: auto;
    }

    pre code {
      padding: 0;
      background: none;
    }

    blockquote {
      padding: var(--space-2) var(--space-3);
      border-left: 2px solid var(--border-strong);
      background: var(--surface-muted);
      color: var(--text-secondary);
    }

    table {
      width: 100%;
      border-collapse: collapse;
      font: var(--type-small);
      display: block;
      overflow-x: auto;
    }

    th,
    td {
      padding: var(--space-1) var(--space-2);
      border: 1px solid var(--border-subtle);
      text-align: left;
      vertical-align: top;
    }

    th {
      background: var(--surface-muted);
      font-weight: var(--weight-semibold);
    }

    hr {
      margin: var(--space-4) 0;
      border: 0;
      border-top: 1px solid var(--divider);
    }

    a {
      color: var(--text-link);
    }
  `,
  template: `
    @for (block of blocks(); track $index) {
      @switch (block.kind) {
        @case ('heading') {
          <div
            [class]="'md-heading md-heading--' + block.level"
            role="heading"
            [attr.aria-level]="block.level"
          >
            <ng-container *ngTemplateOutlet="line; context: { $implicit: block.spans }" />
          </div>
        }
        @case ('code') {
          <pre><code>{{ block.code }}</code></pre>
        }
        @case ('list') {
          @if (block.ordered) {
            <ol>
              @for (item of block.items; track $index) {
                <li><ng-container *ngTemplateOutlet="line; context: { $implicit: item }" /></li>
              }
            </ol>
          } @else {
            <ul>
              @for (item of block.items; track $index) {
                <li><ng-container *ngTemplateOutlet="line; context: { $implicit: item }" /></li>
              }
            </ul>
          }
        }
        @case ('quote') {
          <blockquote>
            <ng-container *ngTemplateOutlet="line; context: { $implicit: block.spans }" />
          </blockquote>
        }
        @case ('table') {
          <table>
            <thead>
              <tr>
                @for (cell of block.header; track $index) {
                  <th><ng-container *ngTemplateOutlet="line; context: { $implicit: cell }" /></th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of block.rows; track $index) {
                <tr>
                  @for (cell of row; track $index) {
                    <td><ng-container *ngTemplateOutlet="line; context: { $implicit: cell }" /></td>
                  }
                </tr>
              }
            </tbody>
          </table>
        }
        @case ('rule') {
          <hr />
        }
        @default {
          <p><ng-container *ngTemplateOutlet="line; context: { $implicit: block.spans }" /></p>
        }
      }
    }

    <ng-template #line let-spans>
      @for (span of spans; track $index) {
        @switch (span.kind) {
          @case ('strong') {
            <strong>{{ span.text }}</strong>
          }
          @case ('emphasis') {
            <em>{{ span.text }}</em>
          }
          @case ('code') {
            <code>{{ span.text }}</code>
          }
          @case ('link') {
            <a [href]="span.href" target="_blank" rel="noreferrer noopener">{{ span.text }}</a>
          }
          @default {
            {{ span.text }}
          }
        }
      }
    </ng-template>
  `,
})
export class DsMarkdown {
  readonly text = input.required<string>();

  protected readonly blocks = computed(() => parseMarkdown(this.text()));
}
