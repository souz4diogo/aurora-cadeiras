import type { AxiosInstance } from 'axios';

import type { TokenResponse } from '@/core/http/apiClient';

export class AuthApi {
  constructor(private readonly http: AxiosInstance) {}

  async login(email: string, senha: string): Promise<TokenResponse> {
    const { data } = await this.http.post<TokenResponse>('/auth/login', { email, senha });
    return data;
  }

  /** Revoga o refresh token no servidor (docs/08 §4). */
  async logout(refreshToken: string): Promise<void> {
    await this.http.post('/auth/logout', { refreshToken });
  }
}
