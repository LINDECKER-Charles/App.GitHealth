import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";
import { once } from "node:events";
import { existsSync } from "node:fs";
import { createServer } from "node:net";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../..",
);
const defaultPublishDirectory = resolve(repositoryRoot, "artifacts/e2e-app");

export class GitHealthApp {
  private process: ChildProcessWithoutNullStreams | undefined;
  private output = "";

  public constructor(private readonly dataDirectory: string) {}

  public baseUrl = "";

  public async start(): Promise<void> {
    if (this.process !== undefined) {
      throw new Error("GitHealth est déjà démarré.");
    }

    const port = await availablePort();
    this.baseUrl = `http://127.0.0.1:${port}`;
    this.process = startProcess(this.dataDirectory, port);
    this.captureOutput(this.process);
    await waitForHealth(this.baseUrl, this.process, () => this.output);
  }

  public async stop(): Promise<void> {
    const current = this.process;
    this.process = undefined;
    if (current === undefined || current.exitCode !== null) {
      return;
    }

    current.kill("SIGTERM");
    const exited = await Promise.race([
      once(current, "exit").then(() => true),
      delay(10_000).then(() => false),
    ]);
    if (!exited) {
      current.kill("SIGKILL");
      await once(current, "exit");
    }
  }

  private captureOutput(current: ChildProcessWithoutNullStreams): void {
    this.output = "";
    current.stdout.on(
      "data",
      (chunk: Buffer) => (this.output += chunk.toString("utf8")),
    );
    current.stderr.on(
      "data",
      (chunk: Buffer) => (this.output += chunk.toString("utf8")),
    );
  }
}

function startProcess(
  dataDirectory: string,
  port: number,
): ChildProcessWithoutNullStreams {
  const publishDirectory =
    process.env["GITHEALTH_E2E_PUBLISH"] ?? defaultPublishDirectory;
  const assemblyPath = resolve(publishDirectory, "githealth.dll");
  if (!existsSync(assemblyPath)) {
    throw new Error(`Publication E2E introuvable : ${assemblyPath}`);
  }

  return spawn(
    "dotnet",
    [
      assemblyPath,
      "--no-browser",
      "--port",
      port.toString(),
      "--data-dir",
      dataDirectory,
    ],
    { cwd: publishDirectory, env: process.env, stdio: "pipe" },
  );
}

async function availablePort(): Promise<number> {
  const server = createServer();
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const address = server.address();
  if (address === null || typeof address === "string") {
    throw new Error("Aucun port loopback disponible.");
  }

  server.close();
  await once(server, "close");
  return address.port;
}

async function waitForHealth(
  baseUrl: string,
  current: ChildProcessWithoutNullStreams,
  logs: () => string,
): Promise<void> {
  const deadline = Date.now() + 30_000;
  while (Date.now() < deadline && current.exitCode === null) {
    try {
      const response = await fetch(`${baseUrl}/health`);
      if (response.ok) {
        return;
      }
    } catch {
      // Le processus peut ne pas encore avoir ouvert son socket.
    }
    await delay(200);
  }

  throw new Error(`GitHealth n'a pas démarré.\n${logs()}`);
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((complete) => setTimeout(complete, milliseconds));
}
