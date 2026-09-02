import {
  addBaselines,
  baselineMoveUpLabel,
  baselineRemoveLabel,
  canAddBaseline,
  isBaselineListDirty,
  maximumBaselineCount,
  moveBaseline,
  removeBaseline,
} from './baseline-draft';

function fullList(): readonly string[] {
  return Array.from({ length: maximumBaselineCount }, (_, index) => `refs/heads/b${index}`);
}

describe('addBaselines', () => {
  it('appends in the order given and ignores what is already declared', () => {
    const added = addBaselines(['refs/heads/main'], ['refs/heads/dev', 'refs/heads/main']);
    expect(added).toEqual(['refs/heads/main', 'refs/heads/dev']);
  });

  it('trims the candidate and drops an empty one', () => {
    expect(addBaselines([], [' refs/heads/dev ', '  '])).toEqual(['refs/heads/dev']);
  });

  it('refuses to go past the cap the API enforces', () => {
    expect(addBaselines(fullList(), ['refs/heads/extra'])).toEqual(fullList());
  });
});

describe('removeBaseline', () => {
  it('drops the named baseline', () => {
    expect(removeBaseline(['refs/heads/main', 'refs/heads/dev'], 'refs/heads/main')).toEqual([
      'refs/heads/dev',
    ]);
  });

  it('keeps the last one: a project needs a baseline to compare against', () => {
    expect(removeBaseline(['refs/heads/main'], 'refs/heads/main')).toEqual(['refs/heads/main']);
  });
});

describe('moveBaseline', () => {
  it('promotes a baseline one place towards the primary position', () => {
    const moved = moveBaseline(['refs/heads/main', 'refs/heads/dev'], 'refs/heads/dev', -1);
    expect(moved).toEqual(['refs/heads/dev', 'refs/heads/main']);
  });

  it('leaves the list untouched when the move would leave it', () => {
    const baselines = ['refs/heads/main', 'refs/heads/dev'];
    expect(moveBaseline(baselines, 'refs/heads/main', -1)).toEqual(baselines);
    expect(moveBaseline(baselines, 'refs/heads/dev', 1)).toEqual(baselines);
    expect(moveBaseline(baselines, 'refs/heads/absent', -1)).toEqual(baselines);
  });
});

describe('canAddBaseline', () => {
  it('closes once the cap is reached', () => {
    expect(canAddBaseline(['refs/heads/main'])).toBe(true);
    expect(canAddBaseline(fullList())).toBe(false);
  });
});

describe('isBaselineListDirty', () => {
  it('reports a change of content and a change of order', () => {
    expect(isBaselineListDirty(['refs/heads/main'], ['refs/heads/main'])).toBe(false);
    expect(isBaselineListDirty(['refs/heads/main'], [])).toBe(true);
    expect(
      isBaselineListDirty(
        ['refs/heads/dev', 'refs/heads/main'],
        ['refs/heads/main', 'refs/heads/dev'],
      ),
    ).toBe(true);
  });
});

describe('baseline labels', () => {
  it('name the baseline they act on', () => {
    expect(baselineRemoveLabel('refs/heads/dev')).toContain('refs/heads/dev');
    expect(baselineMoveUpLabel('refs/heads/dev')).toContain('refs/heads/dev');
  });
});
