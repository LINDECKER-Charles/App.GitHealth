import { Location } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { SnapshotDetailResponse } from '../../core/api/api.models';
import { BranchDetails } from './branch-details';

const snapshotId = '22222222-2222-2222-2222-222222222222';

const detail: SnapshotDetailResponse = {
  analysisId: '11111111-1111-1111-1111-111111111111',
  attributionStatus: 'Available',
  capturedAtUtc: '2026-08-29T10:15:00Z',
  contributors: [
    {
      commitCount: 3,
      email: 'ada@example.test',
      name: 'Ada Lovelace',
    },
  ],
  mailmapApplied: true,
  policy: {
    activeUntilDays: 30,
    excludedPatterns: [],
    inactiveAfterDays: 90,
    protectedPatterns: ['refs/heads/release/*'],
  },
  referenceCommit: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
  referenceName: 'refs/heads/main',
  snapshot: {
    activity: 'Inactive',
    aheadCount: 3,
    behindCount: 1,
    commitId: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    id: snapshotId,
    isExcluded: false,
    isProtected: true,
    lastActivityAtUtc: '2026-01-15T08:30:00Z',
    reason: 'Protégée par le motif « refs/heads/release/* »',
    recommendation: 'Excluded',
    referenceName: 'refs/heads/release/2.0',
    relationship: 'CommonAncestor',
    tipAuthor: 'Ada Lovelace',
    topology: 'Diverged',
  },
};

const api = {
  getSnapshot: vi.fn(() => of(detail)),
};

const location = {
  back: vi.fn(),
};

describe('BranchDetails', () => {
  let fixture: ComponentFixture<BranchDetails>;

  beforeEach(async () => {
    vi.clearAllMocks();
    api.getSnapshot.mockReturnValue(of(detail));
    await TestBed.configureTestingModule({
      imports: [BranchDetails],
      providers: [
        { provide: GitHealthApiClient, useValue: api },
        { provide: Location, useValue: location },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ snapshotId }) },
          },
        },
      ],
    }).compileComponents();
  });

  it('explains facts, SHAs, protection and contributors', () => {
    createFixture();

    expect(text()).toContain('release/2.0');
    expect(text()).toContain(detail.snapshot.commitId);
    expect(text()).toContain(detail.referenceCommit);
    expect(text()).toContain('Commits accessibles depuis cette branche');
    expect(text()).toContain('Ada Lovelace');
    expect(text()).toContain('3 commits');
    expect(text()).toContain('Identités normalisées par .mailmap');
    expect(text()).toContain('Protégée par le motif');
    expect(text()).toContain('Politique au moment du scan');
  });

  it('states when attribution is impossible after a merge', () => {
    api.getSnapshot.mockReturnValue(
      of({
        ...detail,
        attributionStatus: 'UnavailableAfterMerge',
        contributors: [],
        snapshot: {
          ...detail.snapshot,
          aheadCount: 0,
          behindCount: 4,
          isProtected: false,
          reason: 'Fusionnée et inactive',
          topology: 'Merged',
        },
      }),
    );
    createFixture();

    expect(text()).toContain('Attribution impossible après fusion');
    expect(text()).toContain('Git ne permet plus');
    expect(element('.contributor-list')).toBeNull();
  });

  it('shows an API error and retries loading', () => {
    api.getSnapshot
      .mockReturnValueOnce(throwError(() => new Error('Snapshot introuvable')))
      .mockReturnValueOnce(of(detail));
    createFixture();

    expect(text()).toContain('Snapshot introuvable');
    button('Réessayer').click();
    fixture.detectChanges();

    expect(api.getSnapshot).toHaveBeenCalledTimes(2);
    expect(text()).toContain('Ada Lovelace');
  });

  it('returns to the previous view', () => {
    createFixture();
    button('Retour au tableau').click();

    expect(location.back).toHaveBeenCalledOnce();
  });

  function createFixture(): void {
    fixture = TestBed.createComponent(BranchDetails);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function element(selector: string): Element | null {
    return (fixture.nativeElement as HTMLElement).querySelector(selector);
  }

  function button(label: string): HTMLButtonElement {
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    const match = buttons.find((candidate) => candidate.textContent?.includes(label));
    if (match === undefined) {
      throw new Error('Bouton introuvable : ' + label);
    }

    return match;
  }
});
