import { expect, test, type Page } from "@playwright/test";
import { openWorkspace } from "../support/workspace.js";

const dockerUrl = process.env["GITHEALTH_DOCKER_URL"];
const repositoryName = process.env["GITHEALTH_DOCKER_REPOSITORY"] ?? "";
const completeFlow = process.env["GITHEALTH_DOCKER_COMPLETE_FLOW"] === "true";

test.skip(
  dockerUrl === undefined || repositoryName.length === 0,
  "Docker instance or test repository not requested.",
);

test("selects and validates a repository from the mounted folder", async ({
  page,
}) => {
  const apiFailures: string[] = [];
  page.on("response", (response) => {
    if (response.url().includes("/api/") && !response.ok()) {
      apiFailures.push(`${response.status()} ${response.url()}`);
    }
  });

  await selectAndValidateRepository(page);
  await expect(page.getByText("Repository recognised")).toBeVisible();
  expect(apiFailures).toEqual([]);
});

test("adds the repository and finishes its first analysis", async ({
  page,
}) => {
  test.skip(!completeFlow, "Persistent flow not requested.");

  await selectAndValidateRepository(page);
  await page.getByLabel("Display name").fill(repositoryName);
  await page.getByRole("button", { name: "Add repository" }).click();
  await expect(
    page.getByRole("heading", { level: 1, name: repositoryName }),
  ).toBeVisible();

  await page
    .getByRole("button", { name: "Run the first analysis", exact: true })
    .click();
  await expect(page.locator(".dashboard-table tbody tr")).not.toHaveCount(0, {
    timeout: 60_000,
  });
});

async function selectAndValidateRepository(page: Page): Promise<void> {
  await openWorkspace(page, dockerUrl!);
  await page.getByRole("button", { name: "Add a repository" }).last().click();
  await expect(
    page.getByPlaceholder("/repositories/my-repository"),
  ).toBeVisible();

  await page.getByRole("button", { name: "Browse" }).click();
  await page.getByRole("button", { name: repositoryName }).click();
  const repositoryPath = `/repositories/${repositoryName}`;
  await expect(page.getByText(repositoryPath)).toBeVisible();
  await page.getByRole("button", { name: "Use this path" }).click();

  await expect(page.getByLabel("Repository path")).toHaveValue(repositoryPath);
}
