import { TestBed } from '@angular/core/testing';
import { DesktopBridge } from './desktop-bridge';
import { ExternalLinkInterceptor } from './external-link-interceptor';

class FakeDesktopBridge {
  readonly opened: string[] = [];

  constructor(readonly isAvailable: boolean) {}

  openExternal(url: string): Promise<boolean> {
    this.opened.push(url);
    return Promise.resolve(true);
  }
}

function interceptorWith(desktop: FakeDesktopBridge): ExternalLinkInterceptor {
  TestBed.configureTestingModule({
    providers: [{ provide: DesktopBridge, useValue: desktop }],
  });
  const interceptor = TestBed.inject(ExternalLinkInterceptor);
  interceptor.start();
  return interceptor;
}

function link(attributes: Record<string, string>): HTMLAnchorElement {
  const anchor = document.createElement('a');
  for (const [name, value] of Object.entries(attributes)) {
    anchor.setAttribute(name, value);
  }

  document.body.append(anchor);
  return anchor;
}

function clickOn(element: Element): MouseEvent {
  const event = new MouseEvent('click', { bubbles: true, cancelable: true });
  element.dispatchEvent(event);
  return event;
}

describe('ExternalLinkInterceptor', () => {
  afterEach(() => {
    document.body.replaceChildren();
    TestBed.resetTestingModule();
  });

  it('hands an external link to the shell instead of the dead webview', () => {
    const desktop = new FakeDesktopBridge(true);
    interceptorWith(desktop);

    const event = clickOn(link({ href: 'https://example.test/guide', target: '_blank' }));

    expect(event.defaultPrevented).toBe(true);
    expect(desktop.opened).toEqual(['https://example.test/guide']);
  });

  it('follows a link nested inside the anchor', () => {
    const desktop = new FakeDesktopBridge(true);
    interceptorWith(desktop);
    const anchor = link({ href: 'https://example.test/guide', target: '_blank' });
    const label = document.createElement('span');
    anchor.append(label);

    clickOn(label);

    expect(desktop.opened).toEqual(['https://example.test/guide']);
  });

  it('leaves in-app navigation alone', () => {
    const desktop = new FakeDesktopBridge(true);
    interceptorWith(desktop);

    const event = clickOn(link({ href: '/projects/1' }));

    expect(event.defaultPrevented).toBe(false);
    expect(desktop.opened).toEqual([]);
  });

  it('turns away a scheme that is not the web', () => {
    const desktop = new FakeDesktopBridge(true);
    interceptorWith(desktop);

    clickOn(link({ href: 'mailto:someone@example.test', target: '_blank' }));

    expect(desktop.opened).toEqual([]);
  });

  it('stays out of the way in a browser', () => {
    const desktop = new FakeDesktopBridge(false);
    interceptorWith(desktop);

    const event = clickOn(link({ href: 'https://example.test/guide', target: '_blank' }));

    expect(event.defaultPrevented).toBe(false);
    expect(desktop.opened).toEqual([]);
  });
});
