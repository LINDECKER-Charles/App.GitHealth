# Editorial and visual direction

This document sets out how GitHealth presents itself in the README, the documentation,
the release screenshots and any future public material. It extends the application's
**Établi** design system; it does not introduce a second brand.

## Positioning

**Leading sentence**

> The Git facts before the decision.

**Short promise**

> See which branches still matter — without touching the repository.

**Sign-off**

> GitHealth observes. You keep the decision.

GitHealth is not sold as a magic score, a cleanup robot, or yet another forge. It is a
local diagnostic bench that makes a Git situation readable, explains its interpretation
and leaves the final action to the user.

## Concept: the diagnostic file

The documentation follows the same chain as the product:

```text
observed fact  →  interpretation  →  policy  →  verdict  →  human action
```

Every important promise must be traceable to its evidence. A verdict visual therefore
shows, at a minimum, the signal, the rule applied, the recommendation and the share of
uncertainty. The vocabulary of investigation serves traceability, without judicial drama
or excessive medical certainty.

The concept comes in three families:

1. **File** — numbers, references, timestamps and reproducible information.
2. **Anatomy** — thin lines connecting the facts to the verdict, never an isolated score.
3. **Register of refusals** — the product's boundaries made visible and verifiable.

## Visual grammar

### Palette

| Role | Colour | Usage |
|---|---|---|
| Graphite | `#1a1815` / `#fcfbf9` | Chassis, text, calm surfaces |
| Brass | `#a87b27` / `#d9b25f` | Brand, topology, conclusion points |
| Cobalt | `#2e45c9` / `#6b82e8` | Interaction, active evidence, momentum |
| Green | `#157f4b` / `#93cfae` | Safe state, local read, success |
| Amber | `#b45b09` / `#f3c37c` | Attention and review needed |
| Red | `#c0322b` / `#f0b0a9` | Proven danger, never mere decoration |

The full values stay defined in
[`_colors.scss`](../src/App.GitHealth.Web/src/styles/ds/tokens/_colors.scss). Supporting
material does not create a new shade when an existing token already expresses the intent.

### Typography

- **IBM Plex Sans** carries the headings, the explanations and the editorial voice.
- **IBM Plex Mono** carries references, metrics, commands, versions and evidence.
- Large headings are short, firm and slightly tightened.
- Mono is never used for a whole paragraph: it marks the data, not the voice.

An SVG image must keep a system fallback stack, because GitHub does not load the
application's local fonts in every context.

### Shapes and composition

- large calm areas, a discreet technical grid and controlled density;
- rounded topology lines, hollow points and a cobalt pulse at the analysis point;
- low-elevation cards, measured radius, borders more present than shadows;
- `01`, `02`, `03` numbering to build a file, not to decorate;
- plenty of space around a verdict, little around the data that proves it;
- light and dark variants mandatory for every hero or main screenshot.

## README architecture

The README follows a decision funnel:

1. **Promise** — understand the value in under ten seconds.
2. **Visual evidence** — see the real product on a real diagnosis.
3. **Capabilities** — check that the functional need is covered.
4. **Boundary** — understand why local and read-only are credible.
5. **Activation** — start GitHealth without reading the whole documentation.
6. **Depth** — reach architecture, security, benchmarks or contribution.

Badges never replace a positioning sentence. Tables serve exact comparisons; lists serve
capabilities; GitHub alerts are reserved for limitations that genuinely change an
installation decision.

## Voice

GitHealth's voice is calm, precise and adult.

| Do | Avoid |
|---|---|
| "GitHealth explains why this branch needs a review." | "A revolutionary AI cleans up your branches." |
| "No Git write is ever performed." | "100 % safe." |
| "References can be stale without a deliberate fetch." | Hiding a limitation in a footnote. |
| "Candidate for manual cleanup." | "Dead branch" or "delete now". |

The preferred verbs are **read**, **observe**, **compare**, **explain**, **suggest** and
**verify**. The verbs **heal**, **judge** and **clean up automatically** are ruled out:
they overstate the actual behaviour.

## Screenshots and demonstrations

- use a deterministic scenario, or the GitHealth repository itself;
- show no information that could not be published in the repository;
- keep the same viewport for the light and dark variants;
- keep the baseline, the metrics, the topology and the recommendation visible;
- caption the screenshot with the scenario, not with "screenshot of the application";
- regenerate the screenshots whenever the journey or the semantics change.

The current pair lives in [`docs/assets/readme`](assets/readme). The README picks the
right variant with `<picture>` and `prefers-color-scheme`.

## Directions ruled out

- **Medical practice**: immediate, but too clichéd and too certain for heuristics.
- **Scholarly biological metaphors**: original, but longer to explain than Git itself.
- **A README navigated entirely by topology**: spectacular, unstable and hard to scan.
- **Refusal as the main promise**: credible, but it sells the limits before the value.
- **A grammar of seals everywhere**: traceability quickly becomes visual overload.

The final choice combines the evidentiary file, the anatomy of the verdict and the
register of refusals. The more experimental direction — several assets generated from one
executable repository scenario — remains a possible evolution if screenshots become
automated.

## Maintenance

Whenever a public capability changes:

1. update the value sentence or the limitation concerned;
2. check the link to the technical evidence;
3. regenerate the screenshot if the visible journey changes;
4. check both themes;
5. re-read the README at mobile and desktop widths;
6. ship the documentation in the same change as the code.

Successful art direction does not hide the reality of the product: it makes its rigour
immediately perceptible.
