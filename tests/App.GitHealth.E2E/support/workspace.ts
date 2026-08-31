import { expect, type Page } from "@playwright/test";

const introStorageKey = "githealth.intro";
const introSkippedValue = "skipped";

/**
 * Opens the workspace without replaying the opening sequence: it is covered
 * by its own test, and must not pace the other flows.
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

/** Fills the path, waits for the live validation, then saves the repository. */
export async function addRepository(
  page: Page,
  repositoryPath: string,
  displayName: string,
): Promise<void> {
  await page.getByRole("button", { name: "Add a repository" }).last().click();
  await page.getByLabel("Repository path").fill(repositoryPath);
  await expect(page.getByText("Repository recognised")).toBeVisible();
  await page.getByLabel("Display name").fill(displayName);
  await page.getByLabel("Baseline").selectOption("refs/heads/main");
  await page.getByRole("button", { name: "Add repository" }).click();
  await expect(page.getByRole("heading", { name: displayName })).toBeVisible();
}
