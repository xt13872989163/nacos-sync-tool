import { describe, expect, it } from 'vitest';
import { readHttpErrorMessage } from '../../src/main/services/httpError';

describe('readHttpErrorMessage', () => {
  it('includes method, url, status, and response body for axios-like errors', () => {
    const message = readHttpErrorMessage({
      config: {
        method: 'get',
        baseURL: 'http://nacos.example.com:8848',
        url: '/nacos/v1/console/namespaces'
      },
      response: {
        status: 500,
        data: {
          message: 'caused: user not found'
        }
      }
    });

    expect(message).toBe(
      'GET http://nacos.example.com:8848/nacos/v1/console/namespaces failed with status 500: {"message":"caused: user not found"}'
    );
  });

  it('includes transport details when no response was received', () => {
    const message = readHttpErrorMessage({
      config: {
        method: 'post',
        baseURL: 'http://192.168.8.161:8848',
        url: '/nacos/v1/auth/login'
      },
      code: 'ECONNREFUSED',
      message: 'connect ECONNREFUSED 192.168.8.161:8848'
    });

    expect(message).toBe(
      'POST http://192.168.8.161:8848/nacos/v1/auth/login failed with status unknown (ECONNREFUSED connect ECONNREFUSED 192.168.8.161:8848)'
    );
  });
});
