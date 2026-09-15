import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LocalApiState } from '../api/api.models';
import { LocalApiStore } from './local-api-store';

const url = '/api/local-api';
const tokenUrl = `${url}/token`;

const closed: LocalApiState = {
  isEnabled: false,
  port: 7823,
  status: 'Closed',
  address: null,
  failureMessage: null,
  hasToken: false,
  tokenPrefix: null,
  tokenIssuedAtUtc: null,
};

const listening: LocalApiState = {
  ...closed,
  isEnabled: true,
  status: 'Listening',
  address: 'http://127.0.0.1:7823/',
  hasToken: true,
  tokenPrefix: 'abc123',
  tokenIssuedAtUtc: '2026-09-15T10:00:00+00:00',
};

describe('LocalApiStore', () => {
  let store: LocalApiStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(LocalApiStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the access as closed until the API answers', () => {
    expect(store.isListening()).toBe(false);
    expect(store.address()).toBeNull();
  });

  it('exposes the address once the port is open', () => {
    store.load();
    http.expectOne(url).flush(listening);

    expect(store.isListening()).toBe(true);
    expect(store.address()).toBe('http://127.0.0.1:7823/');
  });

  it('issues a token on the way through when the access has never had one', () => {
    store.load();
    http.expectOne(url).flush(closed);

    store.apply(true, 7823);

    const issue = http.expectOne(tokenUrl);
    expect(issue.request.method).toBe('POST');
    issue.flush({ token: 'secret-token', access: { ...closed, hasToken: true } });

    const update = http.expectOne(url);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ isEnabled: true, port: 7823 });
    update.flush(listening);

    expect(store.issuedToken()).toBe('secret-token');
    expect(store.isListening()).toBe(true);
  });

  it('reopens without issuing a second token when one is already in force', () => {
    store.load();
    http.expectOne(url).flush({ ...listening, isEnabled: false, status: 'Closed' });

    store.apply(true, 9100);

    http.expectNone(tokenUrl);
    http.expectOne(url).flush(listening);
    expect(store.issuedToken()).toBeNull();
  });

  it('closes the access without touching the token', () => {
    store.load();
    http.expectOne(url).flush(listening);

    store.apply(false, 7823);

    http.expectNone(tokenUrl);
    const update = http.expectOne(url);
    expect(update.request.body).toEqual({ isEnabled: false, port: 7823 });
    update.flush({ ...listening, isEnabled: false, status: 'Closed', address: null });

    expect(store.isListening()).toBe(false);
  });

  it('shows a new token once and forgets it on request', () => {
    store.load();
    http.expectOne(url).flush(listening);

    store.issueToken();
    http.expectOne(tokenUrl).flush({ token: 'fresh-token', access: listening });
    expect(store.issuedToken()).toBe('fresh-token');

    store.forgetToken();
    expect(store.issuedToken()).toBeNull();
  });

  it('explains a refused change and stops being busy', () => {
    store.load();
    http.expectOne(url).flush(closed);

    store.apply(false, 80);
    http.expectOne(url).error(new ProgressEvent('error'), { status: 400 });

    expect(store.isBusy()).toBe(false);
    expect(store.error()).not.toBeNull();
  });

  it('refuses to stack calls while one is in flight', () => {
    store.load();
    http.expectOne(url).flush(listening);

    store.apply(false, 7823);
    store.apply(true, 7823);

    http.expectOne(url).flush(closed);
  });
});
