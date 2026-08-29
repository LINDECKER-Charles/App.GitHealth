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
  await registerProject(page, context);
  await runAnalysis(page);
  await verifyBranchAndPolicy(page);
  await verifyExports(page, context.fixture.rootPath);
  await restartAndVerifyPersistence(page, context);

  expect(externalHosts).toEqual([]);
  expect(repositoryFingerprint(context.fixture.repositoryPath)).toBe(
    context.fixture.beforeFingerprint,
  );
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

async function registerProject(
  page: Page,
  recipe: RecipeContext,
): Promise<void> {
  await page.goto(recipe.app.baseUrl);
  await expect(
    page.getByRole("heading", {
      name: "Vos branches, sous un jour plus clair.",
    }),
  ).toBeVisible();
  await page.getByLabel("Chemin du dépôt").fill(recipe.fixture.repositoryPath);
  await page.getByRole("button", { name: "Vérifier" }).click();
  await expect(page.getByText("Dépôt reconnu")).toBeVisible();
  await page.getByLabel("Nom affiché").fill("Recette E2E");
  await page
    .getByLabel("Référence de comparaison")
    .selectOption("refs/heads/main");
  await page.getByRole("button", { name: "Ajouter et ouvrir" }).click();
  await expect(
    page.getByRole("heading", { name: "Recette E2E" }),
  ).toBeVisible();
}

async function runAnalysis(page: Page): Promise<void> {
  await page
    .getByRole("button", { name: "Lancer la première analyse", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Branches à examiner" }),
  ).toBeVisible({
    timeout: 60_000,
  });
  await expect(page.locator("tbody tr")).not.toHaveCount(0);
}

async function verifyBranchAndPolicy(page: Page): Promise<void> {
  await page.getByRole("link", { name: "feature/divergente" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText(
    "feature/divergente",
  );
  await page.goBack();
  await page.getByRole("link", { name: "Politiques" }).click();
  await page.getByLabel("Branches protégées").fill("refs/heads/main");
  await page.getByRole("button", { name: "Prévisualiser" }).click();
  await expect(page.getByText("refs/heads/main")).toBeVisible();
  await page.getByRole("button", { name: "Enregistrer la politique" }).click();
  await expect(page.getByRole("status")).toContainText("enregistrée");
  await page.getByRole("button", { name: /Retour au projet/ }).click();
}

async function verifyExports(
  page: Page,
  outputDirectory: string,
): Promise<void> {
  const csv = await download(
    page,
    "Exporter cette vue en CSV",
    join(outputDirectory, "branches.csv"),
  );
  expect(csv.subarray(0, 3)).toEqual(Buffer.from([0xef, 0xbb, 0xbf]));
  expect(csv.toString("utf8")).toContain("referenceName");

  const database = await download(
    page,
    "Sauvegarder les données",
    join(outputDirectory, "githealth.db.backup"),
  );
  expect(database.subarray(0, 16).toString("ascii")).toBe("SQLite format 3\0");
}

async function download(
  page: Page,
  linkName: string,
  targetPath: string,
): Promise<Buffer> {
  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("link", { name: linkName }).click();
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
  await page.goto(recipe.app.baseUrl);
  await expect(
    page.getByRole("heading", { name: "Projets récents" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Recette E2E" }),
  ).toBeVisible();
}
