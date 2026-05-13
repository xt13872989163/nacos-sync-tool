interface AxiosLikeError {
  config?: {
    method?: string;
    url?: string;
    baseURL?: string;
  };
  response?: {
    status?: number;
    data?: unknown;
  };
  code?: string;
  message?: string;
}

export function readHttpErrorMessage(error: unknown): string {
  if (isAxiosLikeError(error)) {
    const method = error.config?.method?.toUpperCase() ?? 'HTTP';
    const url = `${error.config?.baseURL ?? ''}${error.config?.url ?? ''}`;
    const status = error.response?.status ?? 'unknown';
    const data = formatResponseData(error.response?.data);
    const transportMessage = error.response ? '' : formatTransportMessage(error);

    return `${method} ${url} failed with status ${status}${data ? `: ${data}` : ''}${transportMessage}`;
  }

  return error instanceof Error ? error.message : String(error);
}

function formatTransportMessage(error: AxiosLikeError): string {
  const details = [error.code, error.message].filter(Boolean).join(' ');
  return details ? ` (${details})` : '';
}

function isAxiosLikeError(error: unknown): error is AxiosLikeError {
  return Boolean(error && typeof error === 'object' && ('response' in error || 'config' in error));
}

function formatResponseData(data: unknown): string {
  if (!data) {
    return '';
  }

  if (typeof data === 'string') {
    return data.slice(0, 500);
  }

  return JSON.stringify(data).slice(0, 500);
}
