import { expect, test } from "@playwright/test";

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
  await page.getByRole("button", { name: "Ajouter et ouvrir" }).click();
  await expect(
    page.getByRole("heading", { level: 1, name: repositoryName }),
  ).toBeVisible();

  await page
    .getByRole("button", { name: "Lancer la première analyse", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Branches à examiner" }),
  ).toBeVisible({ timeout: 60_000 });
  await expect(page.locator("tbody tr")).not.toHaveCount(0);
});

async function selectAndValidateRepository(
  page: import("@playwright/test").Page,
): Promise<void> {
  await page.goto(dockerUrl!);
  await expect(page.getByText("Chemins du conteneur")).toBeVisible();

  await page.getByRole("button", { name: "Parcourir" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByRole("button", { name: repositoryName }).click();
  const repositoryPath = `/repositories/${repositoryName}`;
  await expect(page.getByText(repositoryPath)).toBeVisible();
  await page.getByRole("button", { name: "Utiliser ce chemin" }).click();

  await expect(page.getByLabel("Chemin du dépôt")).toHaveValue(repositoryPath);
  await page.getByRole("button", { name: "Vérifier" }).click();
  await expect(page.getByText("Dépôt reconnu")).toBeVisible();
}
