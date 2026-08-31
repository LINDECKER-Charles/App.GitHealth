import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api-error';

describe('ApiError', () => {
  it('reads the stable fields from Problem Details', () => {
    const source = new HttpErrorResponse({
      status: 404,
      statusText: 'Not Found',
      url: '/api/projects/missing',
      error: {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
        title: 'Not found',
        status: 404,
        detail: 'The requested project does not exist.',
        instance: '/api/projects/missing',
        code: 'project.not_found',
        traceId: 'trace-42',
      },
    });

    const error = ApiError.from(source);

    expect(error.message).toBe('The requested project does not exist.');
    expect(error.status).toBe(404);
    expect(error.code).toBe('project.not_found');
    expect(error.traceId).toBe('trace-42');
    expect(error.problem?.instance).toBe('/api/projects/missing');
    expect(error.cause).toBe(source);
  });

  it('falls back to the HttpClient error when the response is not Problem Details', () => {
    const source = new HttpErrorResponse({
      status: 0,
      statusText: 'Unknown Error',
      url: '/api/runtime',
      error: 'offline',
    });

    const error = ApiError.from(source);

    expect(error.message).toContain('Http failure response');
    expect(error.status).toBe(0);
    expect(error.code).toBe('http.error');
    expect(error.problem).toBeNull();
  });

  it('normalizes unexpected client errors and preserves an existing ApiError', () => {
    const source = new Error('The browser refused the request.');

    const error = ApiError.from(source);

    expect(error.message).toBe(source.message);
    expect(error.code).toBe('client.unexpected');
    expect(ApiError.from(error)).toBe(error);
  });
});
