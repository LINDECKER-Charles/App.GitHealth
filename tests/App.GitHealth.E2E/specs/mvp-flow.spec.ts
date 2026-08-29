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

test("parcourt le MVP sans modifier le dépôt ni contacter un tiers", async ({
  page,
}) => {
  const externalHosts = observeExternalHosts(page);
  await openWorkspace(page, context.app.baseUrl);
  await expect(
    page.getByText("Aucun dépôt observé pour l'instant"),
  ).toBeVisible();
  await addRepository(page, context.fixture.repositoryPath, "Recette E2E");
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

test("joue la séquence d'ouverture et la laisse couper", async ({ page }) => {
  await page.goto(context.app.baseUrl);
  const intro = page.locator("app-boot-intro");
  await expect(intro).toBeVisible();
  await expect(intro.getByText("Séquence de démarrage")).toBeVisible();
  await page.getByRole("button", { name: "Passer l'introduction" }).click();
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
    .getByRole("button", { name: "Lancer la première analyse", exact: true })
    .click();
  await expect(page.locator(".dashboard-table tbody tr")).not.toHaveCount(0, {
    timeout: 60_000,
  });
  await expect(
    page.locator(".dashboard-tile", { hasText: "Nettoyage possible" }),
  ).toBeVisible();
}

async function explainBranch(page: Page): Promise<void> {
  await page
    .locator(".dashboard-table tbody tr")
    .filter({ hasText: "feature/divergente" })
    .click();
  const fiche = page.locator("app-branch-fiche");
  await expect(fiche.locator(".fiche-name")).toHaveText("feature/divergente");
  await expect(fiche.getByText("Pourquoi cette recommandation")).toBeVisible();
  await expect(fiche.locator(".etb-code__pre")).toContainText(
    "git branch -d feature/divergente",
  );
  await page.getByRole("button", { name: "Fermer la fiche" }).click();
}

async function savePolicy(page: Page): Promise<void> {
  await page.getByRole("link", { name: "Politiques" }).click();
  await page.getByLabel("Nouveau motif protégé").fill("refs/heads/main");
  await page
    .locator(".pattern-form")
    .first()
    .getByRole("button", { name: "Ajouter" })
    .click();
  await expect(page.getByText("Modifications non enregistrées")).toBeVisible();
  await page.getByRole("button", { name: "Enregistrer la politique" }).click();
  await expect(page.locator(".workspace-toast")).toContainText("enregistrée");
  await page.getByRole("link", { name: "Diagnostic" }).click();
}

async function verifyExports(
  page: Page,
  outputDirectory: string,
): Promise<void> {
  const csv = await download(page, join(outputDirectory, "branches.csv"), () =>
    page.getByRole("button", { name: "Exporter en CSV" }).click(),
  );
  expect(csv.subarray(0, 3)).toEqual(Buffer.from([0xef, 0xbb, 0xbf]));
  expect(csv.toString("utf8")).toContain("referenceName");

  const database = await download(
    page,
    join(outputDirectory, "githealth.db.backup"),
    () => page.getByRole("link", { name: "Sauvegarder les données" }).click(),
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
  await expect(page.getByText("Dépôts observés")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Recette E2E" }),
  ).toBeVisible();
  await expect(page.locator(".dashboard-table tbody tr")).not.toHaveCount(0);
}
