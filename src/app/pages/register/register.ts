import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

type RegisterError = {
  error?: {
    message?: string;
  };
};

@Component({
  selector: 'app-register',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.scss'
})
export class Register {
  fullName = '';
  email = '';
  password = '';
  confirmPassword = '';
  error = '';
  success = '';
  loading = false;

  constructor(
    private auth: AuthService, 
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  register() {
    if (!this.fullName || !this.email || !this.password) {
      this.error = 'Please fill all fields';
      return;
    }
    if (this.password !== this.confirmPassword) {
      this.error = 'Passwords do not match';
      return;
    }
    if (this.password.length < 6) {
      this.error = 'Password must be at least 6 characters';
      return;
    }

    this.loading = true;
    this.error = '';

    this.auth.register({
      fullName: this.fullName,
      email: this.email,
      password: this.password,
      role: 'Passenger'
    }).subscribe({
      next: () => {
        this.success = 'Account created! Redirecting to login...';
        this.loading = false;
        this.cdr.detectChanges();
        setTimeout(() => { this.router.navigate(['/login']); this.cdr.detectChanges(); }, 2000);
      },
      error: (err: RegisterError) => {
        this.error = err.error?.message || 'Registration failed';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}