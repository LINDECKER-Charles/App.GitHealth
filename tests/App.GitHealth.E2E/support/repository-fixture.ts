import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { basename, join, resolve, sep } from "node:path";
import { tmpdir } from "node:os";

const fixturePrefix = "githealth-e2e-";
const unsafeGitEnvironmentNames = new Set([
  "GIT_CONFIG",
  "GIT_CONFIG_COUNT",
  "GIT_CONFIG_GLOBAL",
  "GIT_CONFIG_NOSYSTEM",
  "GIT_CONFIG_PARAMETERS",
  "GIT_CONFIG_SYSTEM",
  "GIT_DIR",
  "GIT_EXTERNAL_DIFF",
  "GIT_INDEX_FILE",
  "GIT_OBJECT_DIRECTORY",
  "GIT_WORK_TREE",
]);
const commitEnvironment = Object.fromEntries(
  Object.entries(process.env).filter(
    ([name]) =>
      !unsafeGitEnvironmentNames.has(name.toUpperCase()) &&
      !name.toUpperCase().startsWith("GIT_TRACE"),
  ),
);
Object.assign(commitEnvironment, {
  GIT_AUTHOR_DATE: "2020-01-02T10:00:00Z",
  GIT_COMMITTER_DATE: "2020-01-02T10:00:00Z",
  GIT_NO_LAZY_FETCH: "1",
  GIT_OPTIONAL_LOCKS: "0",
  GIT_TERMINAL_PROMPT: "0",
});

export interface RepositoryFixture {
  readonly beforeFingerprint: string;
  readonly repositoryPath: string;
  readonly rootPath: string;
}

interface CommitSpec {
  readonly content: string;
  readonly message: string;
  readonly relativePath: string;
}

export function createRepositoryFixture(): RepositoryFixture {
  const rootPath = mkdtempSync(join(tmpdir(), fixturePrefix));
  const repositoryPath = join(rootPath, "Dépôt équipe");
  mkdirSync(repositoryPath);
  initializeRepository(repositoryPath);
  createFeatureBranch(repositoryPath);
  createMergedBranch(repositoryPath);
  createDivergedBranch(repositoryPath);
  return {
    beforeFingerprint: repositoryFingerprint(repositoryPath),
    repositoryPath,
    rootPath,
  };
}

export function repositoryFingerprint(repositoryPath: string): string {
  return [
    git(repositoryPath, "rev-parse", "HEAD"),
    git(repositoryPath, "for-each-ref", "--format=%(refname):%(objectname)"),
    git(repositoryPath, "reflog", "show", "--all", "--format=%gD:%H"),
    git(repositoryPath, "status", "--porcelain=v2"),
    git(repositoryPath, "diff", "--cached", "--binary"),
    indexFingerprint(repositoryPath),
  ].join("\n---\n");
}

function indexFingerprint(repositoryPath: string): string {
  const indexPath = git(
    repositoryPath,
    "rev-parse",
    "--path-format=absolute",
    "--git-path",
    "index",
  ).trim();
  return createHash("sha256").update(readFileSync(indexPath)).digest("hex");
}

export function disposeRepositoryFixture(rootPath: string): void {
  const resolvedRoot = resolve(rootPath);
  const temporaryRoot = resolve(tmpdir()) + sep;
  if (
    !resolvedRoot.startsWith(temporaryRoot) ||
    !basename(resolvedRoot).startsWith(fixturePrefix)
  ) {
    throw new Error(
      `Refused to clean up outside the allowed scope: ${resolvedRoot}`,
    );
  }

  rmSync(resolvedRoot, { force: true, recursive: true });
}

function initializeRepository(repositoryPath: string): void {
  git(repositoryPath, "init", "--initial-branch=main");
  git(repositoryPath, "config", "user.name", "Équipe GitHealth");
  git(repositoryPath, "config", "user.email", "githealth@example.invalid");
  writeFileSync(
    join(repositoryPath, "README.md"),
    "# GitHealth fixture\n",
    "utf8",
  );
  writeFileSync(
    join(repositoryPath, ".mailmap"),
    "Équipe GitHealth <githealth@example.invalid> Alias <alias@example.invalid>\n",
    "utf8",
  );
  git(repositoryPath, "add", "--", "README.md", ".mailmap");
  git(repositoryPath, "commit", "-m", "initialise the fixture");
}

function createFeatureBranch(repositoryPath: string): void {
  git(repositoryPath, "switch", "-c", "feature/équipe");
  commitFile(repositoryPath, {
    content: "feature\n",
    message: "add a feature",
    relativePath: "feature.txt",
  });
  git(repositoryPath, "config", "user.name", "Alias");
  git(repositoryPath, "config", "user.email", "alias@example.invalid");
  commitFile(repositoryPath, {
    content: "contribution\n",
    message: "add a contribution",
    relativePath: "alias.txt",
  });
  restoreIdentity(repositoryPath);
  git(repositoryPath, "switch", "main");
}

function createMergedBranch(repositoryPath: string): void {
  git(repositoryPath, "switch", "-c", "maintenance/fusionnee");
  commitFile(repositoryPath, {
    content: "merged\n",
    message: "prepare a merge",
    relativePath: "merged.txt",
  });
  git(repositoryPath, "switch", "main");
  git(
    repositoryPath,
    "merge",
    "--no-ff",
    "maintenance/fusionnee",
    "-m",
    "merge the branch",
  );
}

function createDivergedBranch(repositoryPath: string): void {
  const rootCommit = git(
    repositoryPath,
    "rev-list",
    "--max-parents=0",
    "HEAD",
  ).trim();
  git(repositoryPath, "switch", "-c", "feature/divergente", rootCommit);
  commitFile(repositoryPath, {
    content: "divergence\n",
    message: "diverge from the baseline",
    relativePath: "diverged.txt",
  });
  git(repositoryPath, "switch", "main");
  commitFile(repositoryPath, {
    content: "baseline\n",
    message: "move the baseline forward",
    relativePath: "main.txt",
  });
}

function commitFile(repositoryPath: string, spec: CommitSpec): void {
  writeFileSync(join(repositoryPath, spec.relativePath), spec.content, "utf8");
  git(repositoryPath, "add", "--", spec.relativePath);
  git(repositoryPath, "commit", "-m", spec.message);
}

function restoreIdentity(repositoryPath: string): void {
  git(repositoryPath, "config", "user.name", "Équipe GitHealth");
  git(repositoryPath, "config", "user.email", "githealth@example.invalid");
}

function git(repositoryPath: string, ...arguments_: string[]): string {
  const safeArguments = [
    "--no-pager",
    "-c",
    "core.fsmonitor=false",
    "-c",
    "maintenance.auto=false",
    ...arguments_,
  ];
  return execFileSync("git", safeArguments, {
    cwd: repositoryPath,
    encoding: "utf8",
    env: commitEnvironment,
    stdio: ["ignore", "pipe", "pipe"],
  });
}
