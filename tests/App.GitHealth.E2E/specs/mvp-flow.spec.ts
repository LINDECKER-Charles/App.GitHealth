import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test, type Page } from "@playwright/test";
import { GitHealthApp } from "../support/app-process.js";
import {
  createRepositoryFixture,
  disposeRepositoryFixture,
  repositoryFingerprint,
  type RepositoryFixture,
} from "../support/repository-fixture.js";
import { addRepository, openWorkspace } from "../support/workspace.js";

interface RecipeContext {
  readonly app: GitHealthApp;
  readonly fixture: RepositoryFixture;
}

let context: RecipeContext;
let fixtureForCleanup: RepositoryFixture | undefined;
let appForCleanup: GitHealthApp | undefined;

test.beforeAll(async () => {
  const fixture = createRepositoryFixture();
  const app = new GitHealthApp(join(fixture.rootPath, "data"));
  fixtureForCleanup = fixture;
  appForCleanup = app;
  await app.start();
  context = { app, fixture };
});

test.afterAll(async () => {
  await appForCleanup?.stop();
  if (fixtureForCleanup !== undefined) {
    disposeRepositoryFixture(fixtureForCleanup.rootPath);
  }
});

test("walks the MVP without modifying the repository or contacting a third party", async ({
  page,
}) => {
  const externalHosts = observeExternalHosts(page);
  await openWorkspace(page, context.app.baseUrl);
  await expect(page.getByText("No repository observed yet")).toBeVisible();
  await addRepository(page, context.fixture.repositoryPath, "E2E acceptance");
  await runAnalysis(page);
  await explainBranch(page);
  await savePolicy(page);
  await verifyExports(page, context.fixture.rootPath);
  await restartAndVerifyPersistence(page, context);

  expect(externalHosts).toEqual([]);
  expect(repositoryFingerprint(context.fixture.repositoryPath)).toBe(
    context.fixture.beforeFingerprint,
  );
});

test("plays the opening sequence and lets it be skipped", async ({ page }) => {
  await page.goto(context.app.baseUrl);
  const intro = page.locator("app-boot-intro");
  await expect(intro).toBeVisible();
  await expect(intro.getByText("Opening sequence")).toBeVisible();
  await page.getByRole("button", { name: "Skip the intro" }).click();
  await expect(intro).toBeHidden();
  await expect(page.locator(".topbar")).toBeVisible();
});

function observeExternalHosts(page: Page): string[] {
  const externalHosts: string[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (
      url.protocol.startsWith("http") &&
      !["127.0.0.1", "localhost"].includes(url.hostname)
    ) {
      externalHosts.push(url.hostname);
    }
  });
  return externalHosts;
}

async function runAnalysis(page: Page): Promise<void> {
  await page
    .getByRole("button", { name: "Run the first analysis", exact: true })
    .click();
  await expect(page.locator(".dashboard-table tbody tr")).not.toHaveCount(0, {
    timeout: 60_000,
  });
  await expect(
    page.locator(".dashboard-tile", { hasText: "Cleanup possible" }),
  ).toBeVisible();
}

async function explainBranch(page: Page): Promise<void> {
  await page
    .locator(".dashboard-table tbody tr")
    .filter({ hasText: "feature/divergente" })
    .click();
  const card = page.locator("app-branch-card");
  await expect(card.locator(".card-name")).toHaveText("feature/divergente");
  await expect(card.getByText("Why this recommendation")).toBeVisible();
  await expect(card.locator(".etb-code__pre")).toContainText(
    "git branch -d feature/divergente",
  );
  await page.getByRole("button", { name: "Close the card" }).click();
}

async function savePolicy(page: Page): Promise<void> {
  await page.getByRole("link", { name: "Policies" }).click();
  await page.getByLabel("New protected pattern").fill("refs/heads/main");
  await page
    .locator(".pattern-form")
    .first()
    .getByRole("button", { name: "Add" })
    .click();
  await expect(page.getByText("Unsaved changes")).toBeVisible();
  await page.getByRole("button", { name: "Save the policy" }).click();
  await expect(page.locator(".workspace-toast")).toContainText("Policy saved");
  await page.getByRole("link", { name: "Diagnostic" }).click();
}

async function verifyExports(
  page: Page,
  outputDirectory: string,
): Promise<void> {
  const csv = await download(page, join(outputDirectory, "branches.csv"), () =>
    page.getByRole("button", { name: "Export as CSV" }).click(),
  );
  expect(csv.subarray(0, 3)).toEqual(Buffer.from([0xef, 0xbb, 0xbf]));
  expect(csv.toString("utf8")).toContain("referenceName");

  const database = await download(
    page,
    join(outputDirectory, "githealth.db.backup"),
    () => page.getByRole("link", { name: "Back up the data" }).click(),
  );
  expect(database.subarray(0, 16).toString("ascii")).toBe("SQLite format 3\0");
}

async function download(
  page: Page,
  targetPath: string,
  trigger: () => Promise<void>,
): Promise<Buffer> {
  const downloadPromise = page.waitForEvent("download");
  await trigger();
  const file = await downloadPromise;
  await file.saveAs(targetPath);
  return readFileSync(targetPath);
}

async function restartAndVerifyPersistence(
  page: Page,
  recipe: RecipeContext,
): Promise<void> {
  await recipe.app.stop();
  await recipe.app.start();
  await openWorkspace(page, recipe.app.baseUrl);
  await expect(page.getByText("Observed repositories")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "E2E acceptance" }),
  ).toBeVisible();
  await expect(page.locator(".dashboard-table tbody tr")).not.toHaveCount(0);
}
