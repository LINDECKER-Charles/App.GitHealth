import { HttpErrorResponse } from '@angular/common/http';

const defaultErrorCode = 'http.error';
const unexpectedErrorCode = 'client.unexpected';
const defaultErrorMessage = 'Une erreur inattendue est survenue.';

export interface ApiProblemDetails {
  readonly type: string | null;
  readonly title: string | null;
  readonly status: number | null;
  readonly detail: string | null;
  readonly instance: string | null;
  readonly code: string | null;
  readonly traceId: string | null;
}

interface ApiErrorDetails {
  readonly status: number;
  readonly code: string;
  readonly traceId: string | null;
  readonly problem: ApiProblemDetails | null;
  readonly cause: unknown;
}

export class ApiError extends Error {
  override readonly name = 'ApiError';

  private constructor(
    message: string,
    private readonly details: ApiErrorDetails,
  ) {
    super(message, { cause: details.cause });
  }

  get status(): number {
    return this.details.status;
  }

  get code(): string {
    return this.details.code;
  }

  get traceId(): string | null {
    return this.details.traceId;
  }

  get problem(): ApiProblemDetails | null {
    return this.details.problem;
  }

  get error(): ApiProblemDetails | null {
    return this.details.problem;
  }

  static from(error: unknown): ApiError {
    if (error instanceof ApiError) {
      return error;
    }

    if (!(error instanceof HttpErrorResponse)) {
      return ApiError.fromUnexpectedError(error);
    }

    const problem = readProblemDetails(error.error);
    const message = problem?.detail ?? problem?.title ?? error.message ?? defaultErrorMessage;
    return new ApiError(message, {
      status: error.status || problem?.status || 0,
      code: problem?.code ?? defaultErrorCode,
      traceId: problem?.traceId ?? null,
      problem,
      cause: error,
    });
  }

  private static fromUnexpectedError(error: unknown): ApiError {
    const message = error instanceof Error ? error.message : defaultErrorMessage;
    return new ApiError(message, {
      status: 0,
      code: unexpectedErrorCode,
      traceId: null,
      problem: null,
      cause: error,
    });
  }
}

export function apiErrorMessage(error: unknown, fallback: string = defaultErrorMessage): string {
  const message = ApiError.from(error).message.trim();
  return message.length > 0 ? message : fallback;
}

function readProblemDetails(value: unknown): ApiProblemDetails | null {
  if (!isRecord(value)) {
    return null;
  }

  return {
    type: readString(value, 'type'),
    title: readString(value, 'title'),
    status: readNumber(value, 'status'),
    detail: readString(value, 'detail'),
    instance: readString(value, 'instance'),
    code: readString(value, 'code'),
    traceId: readString(value, 'traceId'),
  };
}

function isRecord(value: unknown): value is Readonly<Record<string, unknown>> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(value: Readonly<Record<string, unknown>>, key: string): string | null {
  const candidate = value[key];
  return typeof candidate === 'string' ? candidate : null;
}

function readNumber(value: Readonly<Record<string, unknown>>, key: string): number | null {
  const candidate = value[key];
  return typeof candidate === 'number' ? candidate : null;
}
