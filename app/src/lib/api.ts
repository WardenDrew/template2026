export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number
  ) {
    super(message);
  }
}

type ErrorResponse = {
  title?: string;
  detail?: string;
  error?: string;
  errors?: Record<string, string[]>;
};

export async function apiGetJson<TResponse>(path: string) {
  return await apiFetchJson<TResponse>(path, {
    method: "GET"
  });
}

export async function apiPostJson<TResponse>(
  path: string,
  body: Record<string, unknown> = {}
) {
  return await apiFetchJson<TResponse>(path, {
    body: JSON.stringify(body),
    method: "POST"
  });
}

async function apiFetchJson<TResponse>(
  path: string,
  init: RequestInit
): Promise<TResponse> {
  const accessToken = window.localStorage.getItem("template.accessToken");
  const headers = new Headers(init.headers);

  headers.set("Accept", "application/json");

  if (accessToken !== null) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  if (init.body !== undefined && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`/api${path}`, {
    ...init,
    headers
  });

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response), response.status);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

async function readErrorMessage(response: Response) {
  const fallbackMessage = `Request failed with status ${response.status}.`;

  try {
    const responseBody = (await response.json()) as ErrorResponse;
    const firstValidationError = responseBody.errors
      ? Object.values(responseBody.errors)[0]?.[0]
      : undefined;

    return (
      firstValidationError ??
      responseBody.detail ??
      responseBody.error ??
      responseBody.title ??
      fallbackMessage
    );
  } catch {
    return fallbackMessage;
  }
}
