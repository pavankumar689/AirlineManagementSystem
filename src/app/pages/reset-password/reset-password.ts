import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-reset-password',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss'
})
export class ResetPassword implements OnInit {
  userId: number = 0;
  token: string = '';
  newPassword = '';
  confirmPassword = '';
  message = '';
  error = '';
  loading = false;

  constructor(
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.userId = Number(params['userId']);
      this.token = params['token'] || '';

      if (!this.userId || !this.token) {
        this.error = 'Invalid reset link. Please request a new one.';
      }
    });
  }

  submit() {
    if (this.newPassword !== this.confirmPassword) {
      this.error = 'Passwords do not match.';
      return;
    }

    if (this.newPassword.length < 6) {
      this.error = 'Password must be at least 6 characters.';
      return;
    }

    this.loading = true;
    this.error = '';
    this.message = '';

    this.auth.resetPassword(this.userId, this.token, this.newPassword).subscribe({
      next: (res) => {
        this.message = res.message || 'Password reset successfully.';
        this.loading = false;
        this.cdr.detectChanges();
        // optionally route them to login immediately or let them click
      },
      error: (err) => {
        this.error = err?.error?.message || 'Failed to reset password. Link may be expired.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
