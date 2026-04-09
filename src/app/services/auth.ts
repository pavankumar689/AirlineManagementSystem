import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private url = environment.authUrl;

  // ─── In-memory token storage (NOT localStorage) ───────────────────────────
  private _accessToken: string | null = null;
  private _role: string = '';
  private _name: string = '';
  private _email: string = '';

  constructor(private http: HttpClient, private router: Router) {}

  // ─── Auth Endpoints ────────────────────────────────────────────────────────

  login(email: string, password: string): Observable<any> {
    return this.http.post<any>(
      `${this.url}/api/auth/login`,
      { email, password },
      { withCredentials: true }
    ).pipe(
      tap((res) => this.storeSession(res))
    );
  }

  register(data: any): Observable<any> {
    return this.http.post<any>(`${this.url}/api/auth/register`, data);
  }

  /** Called by interceptor on 401 — uses HttpOnly refresh token cookie */
  refreshToken(): Observable<any> {
    return this.http.post<any>(
      `${this.url}/api/auth/refresh`,
      {},
      { withCredentials: true }
    ).pipe(
      tap((res) => this.storeSession(res))
    );
  }

  logout(): void {
    this.http.post(`${this.url}/api/auth/logout`, {}, { withCredentials: true })
      .subscribe({ error: () => {} });
    this.clearSession();
    this.router.navigate(['/login']);
  }

  getProfile() {
    return this.http.get<any>(`${this.url}/api/auth/profile`);
  }

  updateProfile(data: any) {
    return this.http.put<any>(`${this.url}/api/auth/profile`, data);
  }

  changePassword(data: any) {
    return this.http.post<any>(`${this.url}/api/auth/change-password`, data);
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post<any>(`${this.url}/api/auth/forgot-password`, { email });
  }

  resetPassword(userId: number, token: string, newPassword: string): Observable<any> {
    return this.http.post<any>(`${this.url}/api/auth/reset-password`, { userId, token, newPassword });
  }

  getRewardPoints(): Observable<any> {
    return this.http.get<any>(`${this.url}/api/auth/rewards`);
  }

  redeemPoints(pointsToRedeem: number, bookingTotal: number, referenceId?: string): Observable<any> {
    return this.http.post<any>(`${this.url}/api/auth/rewards/redeem`, {
      pointsToRedeem,
      bookingTotal,
      referenceId
    });
  }

  // ─── Token / Session ───────────────────────────────────────────────────────

  private storeSession(res: any): void {
    this._accessToken = res.accessToken;
    this._role = res.role || '';
    this._name = res.fullName || '';
    this._email = res.email || '';
  }

  private clearSession(): void {
    this._accessToken = null;
    this._role = '';
    this._name = '';
    this._email = '';
  }

  getToken(): string | null {
    return this._accessToken;
  }

  getRole(): string {
    return this._role;
  }

  getName(): string {
    return this._name;
  }

  getEmail(): string {
    return this._email;
  }

  isLoggedIn(): boolean {
    return !!this._accessToken;
  }
}