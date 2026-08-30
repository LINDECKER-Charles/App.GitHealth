import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UpdateStatus } from '../api/api.models';
import { UpdateStore } from './update-store';

const available: UpdateStatus = {
  availability: 'Available',
  currentVersion: '0.1.0-rc.1',
  availableVersion: '0.1.0-rc.2',
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

  it('n’annonce rien tant que le statut est inconnu', () => {
    expect(store.isAvailable()).toBe(false);
    expect(store.availableVersion()).toBeNull();
  });

  it('expose la version publiée quand une mise à jour existe', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);

    expect(store.isAvailable()).toBe(true);
    expect(store.availableVersion()).toBe('0.1.0-rc.2');
  });

  it('reste muet quand les mises à jour ne sont pas prises en charge', () => {
    store.load();
    http.expectOne('/api/updates').flush({
      availability: 'Unsupported',
      currentVersion: null,
      availableVersion: null,
    } satisfies UpdateStatus);

    expect(store.isAvailable()).toBe(false);
  });

  it('reste muet quand l’appel échoue', () => {
    store.load();
    http.expectOne('/api/updates').error(new ProgressEvent('error'), { status: 500 });

    expect(store.status()).toBeNull();
    expect(store.isAvailable()).toBe(false);
  });

  it('déclenche la mise à jour et signale qu’elle est en cours', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);

    store.apply();

    const request = http.expectOne('/api/updates/apply');
    expect(request.request.method).toBe('POST');
    expect(store.isApplying()).toBe(true);
    request.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('n’applique rien sans mise à jour disponible', () => {
    store.apply();

    http.expectNone('/api/updates/apply');
    expect(store.isApplying()).toBe(false);
  });

  it('redevient disponible quand l’application échoue', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);
    store.apply();

    http.expectOne('/api/updates/apply').error(new ProgressEvent('error'), { status: 500 });

    expect(store.isApplying()).toBe(false);
  });
});
