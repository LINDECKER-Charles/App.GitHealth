import { expect, test, type Page } from "@playwright/test";
import { openWorkspace } from "../support/workspace.js";

const dockerUrl = process.env["GITHEALTH_DOCKER_URL"];
const repositoryName = process.env["GITHEALTH_DOCKER_REPOSITORY"] ?? "";
const completeFlow = process.env["GITHEALTH_DOCKER_COMPLETE_FLOW"] === "true";

test.skip(
  dockerUrl === undefined || repositoryName.length === 0,
  "Instance Docker ou dépôt de test non demandé.",
);

test("sélectionne et valide un dépôt depuis le dossier monté", async ({
  page,
}) => {
  const apiFailures: string[] = [];
  page.on("response", (response) => {
    if (response.url().includes("/api/") && !response.ok()) {
      apiFailures.push(`${response.status()} ${response.url()}`);
    }
  });

  await selectAndValidateRepository(page);
  await expect(page.getByText("Dépôt reconnu")).toBeVisible();
  expect(apiFailures).toEqual([]);
});

test("ajoute le dépôt et termine sa première analyse", async ({ page }) => {
  test.skip(!completeFlow, "Parcours persistant non demandé.");

  await selectAndValidateRepository(page);
  await page.getByLabel("Nom affiché").fill(repositoryName);
  await page.getByRole("button", { name: "Ajouter le dépôt" }).click();
  await expect(
    page.getByRole("heading", { level: 1, name: repositoryName }),
  ).toBeVisible();

  await page
    .getByRole("button", { name: "Lancer la première analyse", exact: true })
    .click();
  await expect(page.locator(".dashboard-table tbody tr")).not.toHaveCount(0, {
    timeout: 60_000,
  });
});

async function selectAndValidateRepository(page: Page): Promise<void> {
  await openWorkspace(page, dockerUrl!);
  await page.getByRole("button", { name: "Ajouter un dépôt" }).last().click();
  await expect(page.getByPlaceholder("/repositories/mon-depot")).toBeVisible();

  await page.getByRole("button", { name: "Parcourir" }).click();
  await page.getByRole("button", { name: repositoryName }).click();
  const repositoryPath = `/repositories/${repositoryName}`;
  await expect(page.getByText(repositoryPath)).toBeVisible();
  await page.getByRole("button", { name: "Utiliser ce chemin" }).click();

  await expect(page.getByLabel("Chemin du dépôt")).toHaveValue(repositoryPath);
}
