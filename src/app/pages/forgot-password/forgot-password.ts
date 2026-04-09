import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-forgot-password',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.scss'
})
export class ForgotPassword {
  email = '';
  message = '';
  error = '';
  loading = false;

  constructor(
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  submit() {
    if (!this.email) {
      this.error = 'Please enter your email address.';
      return;
    }

    this.loading = true;
    this.error = '';
    this.message = '';

    this.auth.forgotPassword(this.email).subscribe({
      next: (res) => {
        this.message = res.message || 'If that email is registered, a reset link has been sent.';
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = err?.error?.message || 'An error occurred. Please try again.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
