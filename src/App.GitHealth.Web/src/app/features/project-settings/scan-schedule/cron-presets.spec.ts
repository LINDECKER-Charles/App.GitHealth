import {
  cronPresetOptions,
  cronPresets,
  customPreset,
  hasCronShape,
  normalizeCron,
  presetOf,
} from './cron-presets';

describe('normalizeCron', () => {
  it('collapses whatever spacing was typed, as the API stores it back', () => {
    expect(normalizeCron('  0   9  *  *  * ')).toBe('0 9 * * *');
  });

  it('answers an empty string for a blank field', () => {
    expect(normalizeCron('   ')).toBe('');
  });
});

describe('presetOf', () => {
  it('recognises an offered rhythm however it was spaced', () => {
    expect(presetOf('0  9  *  *  1-5')).toBe('0 9 * * 1-5');
  });

  it('falls back to custom for an expression nobody offered', () => {
    expect(presetOf('7 3 * * *')).toBe(customPreset);
  });

  it('falls back to custom for an empty field', () => {
    expect(presetOf('')).toBe(customPreset);
  });
});

describe('hasCronShape', () => {
  it('accepts the five fields the API expects', () => {
    expect(hasCronShape('*/15 * * * *')).toBe(true);
    expect(hasCronShape('0,30 9-17 * * 1-5')).toBe(true);
  });

  it('refuses anything that is not five fields', () => {
    expect(hasCronShape('')).toBe(false);
    expect(hasCronShape('* * * *')).toBe(false);
    expect(hasCronShape('* * * * * *')).toBe(false);
  });

  it('refuses fields built from anything but digits and cron punctuation', () => {
    expect(hasCronShape('0 9 * * MON')).toBe(false);
  });
});

describe('cronPresetOptions', () => {
  it('offers every rhythm, with the custom entry last', () => {
    const options = cronPresetOptions();
    expect(options).toHaveLength(cronPresets.length + 1);
    expect(options.at(-1)?.value).toBe(customPreset);
  });

  // A preset the picker cannot select back is a rhythm that silently reads as custom.
  it('offers only expressions the picker recognises', () => {
    for (const preset of cronPresets) {
      expect(presetOf(preset.value)).toBe(preset.value);
    }
  });
});
