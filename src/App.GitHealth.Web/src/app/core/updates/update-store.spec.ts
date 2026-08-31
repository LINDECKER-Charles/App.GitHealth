import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UpdateStatus } from '../api/api.models';
import { UpdateStore } from './update-store';

const available: UpdateStatus = {
  availability: 'Available',
  currentVersion: '0.1.0',
  availableVersion: '0.1.1',
};

describe('UpdateStore', () => {
  let store: UpdateStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(UpdateStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('announces nothing while the status is unknown', () => {
    expect(store.isAvailable()).toBe(false);
    expect(store.availableVersion()).toBeNull();
  });

  it('exposes the published version when an update exists', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);

    expect(store.isAvailable()).toBe(true);
    expect(store.availableVersion()).toBe('0.1.1');
  });

  it('stays silent when updates are not supported', () => {
    store.load();
    http.expectOne('/api/updates').flush({
      availability: 'Unsupported',
      currentVersion: null,
      availableVersion: null,
    } satisfies UpdateStatus);

    expect(store.isAvailable()).toBe(false);
  });

  it('stays silent when the call fails', () => {
    store.load();
    http.expectOne('/api/updates').error(new ProgressEvent('error'), { status: 500 });

    expect(store.status()).toBeNull();
    expect(store.isAvailable()).toBe(false);
  });

  it('triggers the update and stays running until the restart', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);

    store.apply();

    const request = http.expectOne('/api/updates/apply');
    expect(request.request.method).toBe('POST');
    expect(store.isApplying()).toBe(true);

    // 202 with no body: the host restarts the application, this page does not survive.
    request.flush(null, { status: 202, statusText: 'Accepted' });

    expect(store.isApplying()).toBe(true);
    expect(store.error()).toBeNull();
  });

  it('releases the button when nothing could be downloaded', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);
    store.apply();

    // 200 carrying a status: the host applied nothing and says why.
    http.expectOne('/api/updates/apply').flush({
      availability: 'Unknown',
      currentVersion: '0.1.0',
      availableVersion: null,
    } satisfies UpdateStatus);

    expect(store.isApplying()).toBe(false);
    expect(store.isAvailable()).toBe(false);
    expect(store.error()).not.toBeNull();
  });

  it('applies nothing when no update is available', () => {
    store.apply();

    http.expectNone('/api/updates/apply');
    expect(store.isApplying()).toBe(false);
  });

  it('becomes available again and explains why applying failed', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);
    store.apply();

    http.expectOne('/api/updates/apply').error(new ProgressEvent('error'), { status: 500 });

    expect(store.isApplying()).toBe(false);
    expect(store.isAvailable()).toBe(true);
    expect(store.error()).not.toBeNull();
  });
});
