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

  it('n’annonce rien tant que le statut est inconnu', () => {
    expect(store.isAvailable()).toBe(false);
    expect(store.availableVersion()).toBeNull();
  });

  it('expose la version publiée quand une mise à jour existe', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);

    expect(store.isAvailable()).toBe(true);
    expect(store.availableVersion()).toBe('0.1.1');
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

  it('déclenche la mise à jour et reste en cours jusqu’au redémarrage', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);

    store.apply();

    const request = http.expectOne('/api/updates/apply');
    expect(request.request.method).toBe('POST');
    expect(store.isApplying()).toBe(true);

    // 202 sans corps : l'hôte relance l'application, cette page ne survit pas.
    request.flush(null, { status: 202, statusText: 'Accepted' });

    expect(store.isApplying()).toBe(true);
    expect(store.error()).toBeNull();
  });

  it('libère le bouton quand rien n’a pu être téléchargé', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);
    store.apply();

    // 200 porteur d'un statut : l'hôte n'a rien appliqué et dit pourquoi.
    http.expectOne('/api/updates/apply').flush({
      availability: 'Unknown',
      currentVersion: '0.1.0',
      availableVersion: null,
    } satisfies UpdateStatus);

    expect(store.isApplying()).toBe(false);
    expect(store.isAvailable()).toBe(false);
    expect(store.error()).not.toBeNull();
  });

  it('n’applique rien sans mise à jour disponible', () => {
    store.apply();

    http.expectNone('/api/updates/apply');
    expect(store.isApplying()).toBe(false);
  });

  it('redevient disponible et explique l’échec de l’application', () => {
    store.load();
    http.expectOne('/api/updates').flush(available);
    store.apply();

    http.expectOne('/api/updates/apply').error(new ProgressEvent('error'), { status: 500 });

    expect(store.isApplying()).toBe(false);
    expect(store.isAvailable()).toBe(true);
    expect(store.error()).not.toBeNull();
  });
});
