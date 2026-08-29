import { expect, type Page } from "@playwright/test";

const introStorageKey = "githealth.intro";
const introSkippedValue = "skipped";

/**
 * Ouvre l'espace de travail sans rejouer la séquence d'introduction : elle est
 * couverte par son propre test, et n'a pas à rythmer les autres parcours.
 */
export async function openWorkspace(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.addInitScript(
    ([key, value]) => window.sessionStorage.setItem(key, value),
    [introStorageKey, introSkippedValue] as const,
  );
  await page.goto(baseUrl);
  await expect(page.locator(".topbar")).toBeVisible();
}

/** Renseigne le chemin, attend la validation en direct puis enregistre le dépôt. */
export async function addRepository(
  page: Page,
  repositoryPath: string,
  displayName: string,
): Promise<void> {
  await page.getByRole("button", { name: "Ajouter un dépôt" }).last().click();
  await page.getByLabel("Chemin du dépôt").fill(repositoryPath);
  await expect(page.getByText("Dépôt reconnu")).toBeVisible();
  await page.getByLabel("Nom affiché").fill(displayName);
  await page
    .getByLabel("Référence de comparaison")
    .selectOption("refs/heads/main");
  await page.getByRole("button", { name: "Ajouter le dépôt" }).click();
  await expect(page.getByRole("heading", { name: displayName })).toBeVisible();
}
