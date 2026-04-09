import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth';
import { BookingService } from '../../services/booking';

type ProfileData = any;
type BookingList = any[];
type PasswordChangeError = { error?: { message?: string } };

@Component({
  selector: 'app-profile',
  imports: [CommonModule, FormsModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss'
})
export class Profile implements OnInit {
  profile: any = null;
  bookings: any[] = [];
  loading = true;
  savingProfile = false;
  savingPassword = false;
  profileSuccess = '';
  profileError = '';
  passwordSuccess = '';
  passwordError = '';
  rewardPoints = 0;
  rewardHistory: any[] = [];

  profileForm = {
    fullName: '',
    email: ''
  };

  passwordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };

  constructor(
    private authService: AuthService,
    private bookingService: BookingService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadProfile();
    this.loadBookings();
    this.loadRewards();
  }

  loadRewards() {
    this.authService.getRewardPoints().subscribe({
      next: (data) => {
        this.rewardPoints = data.balance || 0;
        this.rewardHistory = data.history || [];
        this.cdr.detectChanges();
      }
    });
  }

  loadProfile() {
    this.loading = true;
    this.authService.getProfile().subscribe({
      next: (data: ProfileData) => {
        this.profile = data;
        this.profileForm.fullName = data.fullName;
        this.profileForm.email = data.email;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => { 
        this.loading = false; 
        this.cdr.detectChanges();
      }
    });
  }

  loadBookings() {
    this.bookingService.getMyBookings().subscribe({
      next: (data: BookingList) => { 
        this.bookings = data; 
        this.cdr.detectChanges();
      },
      error: () => {
        this.cdr.detectChanges();
      }
    });
  }

  updateProfile() {
    this.savingProfile = true;
    this.profileError = '';
    this.authService.updateProfile(this.profileForm).subscribe({
      next: () => {
        this.profileSuccess = 'Profile updated successfully!';
        this.savingProfile = false;
        this.loadProfile();
        setTimeout(() => { this.profileSuccess = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: () => {
        this.profileError = 'Failed to update profile';
        this.savingProfile = false;
        this.cdr.detectChanges();
      }
    });
  }

  changePassword() {
    if (this.passwordForm.newPassword !== this.passwordForm.confirmPassword) {
      this.passwordError = 'Passwords do not match';
      this.cdr.detectChanges();
      return;
    }
    if (this.passwordForm.newPassword.length < 6) {
      this.passwordError = 'Password must be at least 6 characters';
      this.cdr.detectChanges();
      return;
    }

    this.savingPassword = true;
    this.passwordError = '';

    this.authService.changePassword({
      currentPassword: this.passwordForm.currentPassword,
      newPassword: this.passwordForm.newPassword
    }).subscribe({
      next: () => {
        this.passwordSuccess = 'Password changed successfully!';
        this.savingPassword = false;
        this.passwordForm = {
          currentPassword: '',
          newPassword: '',
          confirmPassword: ''
        };
        setTimeout(() => { this.passwordSuccess = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: (err: PasswordChangeError) => {
        this.passwordError = err.error?.message || 'Failed to change password';
        this.savingPassword = false;
        this.cdr.detectChanges();
      }
    });
  }

  get confirmedBookings() {
    return this.bookings.filter(b => b.bookingStatus === 'Confirmed').length;
  }

  get totalSpent() {
    return this.bookings
      .filter(b => b.bookingStatus === 'Confirmed')
      .reduce((sum, b) => sum + b.totalAmount, 0);
  }

  getInitials(): string {
    if (!this.profile?.fullName) return '?';
    return this.profile.fullName
      .split(' ')
      .map((n: string) => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }
}