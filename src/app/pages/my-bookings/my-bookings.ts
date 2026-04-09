import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BookingService } from '../../services/booking';

type BookingCancelError = {
  error?: {
    message?: string;
  };
};

@Component({
  selector: 'app-my-bookings',
  imports: [CommonModule, RouterLink],
  templateUrl: './my-bookings.html',
  styleUrl: './my-bookings.scss'
})
export class MyBookings implements OnInit {
  bookings: any[] = [];
  loading = true;
  error = '';
  success = '';
  cancelling = false;

  constructor(
    private bookingService: BookingService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadBookings();
  }

  loadBookings() {
    this.loading = true;
    this.bookingService.getMyBookings().subscribe({
      next: (data: any[]) => {
        this.bookings = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to load bookings';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancelBooking(id: number, amount: number) {
    const refund = Math.round(amount * 0.9);
    if (!confirm(`Cancel this booking? You will receive ₹${refund} refund (90%).`))
      return;

    this.cancelling = true;
    this.bookingService.cancelBooking(id).subscribe({
      next: () => {
        this.success = `Booking cancelled. Refund of ₹${refund} will be processed.`;
        this.cancelling = false;
        this.loadBookings();
        setTimeout(() => { this.success = ''; this.cdr.detectChanges(); }, 5000);
        this.cdr.detectChanges();
      },
      error: (err: BookingCancelError) => {
        this.error = err.error?.message || 'Failed to cancel booking';
        this.cancelling = false;
        setTimeout(() => { this.error = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      }
    });
  }

  getStatusClass(status: string): string {
    if (status === 'Confirmed') return 'bg-success';
    if (status === 'Cancelled') return 'bg-danger';
    if (status === 'Pending') return 'bg-warning text-dark';
    return 'bg-secondary';
  }

  get confirmedCount() {
    return this.bookings.filter(b => b.bookingStatus === 'Confirmed').length;
  }

  get cancelledCount() {
    return this.bookings.filter(b => b.bookingStatus === 'Cancelled').length;
  }

  get pendingCount() {
    return this.bookings.filter(b => b.bookingStatus === 'Pending').length;
  }
}