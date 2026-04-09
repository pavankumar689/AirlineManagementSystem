import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BookingService } from '../../services/booking';

@Component({
  selector: 'app-booking-detail',
  imports: [CommonModule, RouterLink],
  template: `
    <div class="booking-detail-page py-5">
      <div class="container">
        <a routerLink="/my-bookings" class="btn btn-link text-decoration-none mb-4">
          <i class="bi bi-arrow-left me-2"></i>Back to My Bookings
        </a>

        <div *ngIf="loading" class="text-center py-5">
          <div class="spinner-border text-primary"></div>
          <p class="mt-2 text-muted">Loading ticket details...</p>
        </div>

        <div class="alert alert-danger" *ngIf="error">
          <i class="bi bi-exclamation-triangle me-2"></i>{{ error }}
        </div>

        <div class="card shadow-sm border-0" *ngIf="!loading && booking">
          <div class="card-header bg-primary text-white d-flex justify-content-between align-items-center py-3">
            <h5 class="mb-0"><i class="bi bi-ticket-detailed me-2"></i>E-Ticket / Booking Details</h5>
            <span class="badge bg-light text-primary">Booking #{{ booking.bookingId }}</span>
          </div>
          <div class="card-body p-4">
            <div class="row mb-4 bg-light p-3 rounded">
              <div class="col-md-3">
                <small class="text-muted d-block">Status</small>
                <span class="fw-bold fs-5" [ngClass]="{'text-success': booking.bookingStatus === 'Confirmed', 'text-warning': booking.bookingStatus === 'Pending', 'text-danger': booking.bookingStatus === 'Cancelled'}">
                  {{ booking.bookingStatus }}
                </span>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">Booking Date</small>
                <span class="fw-bold">{{ booking.createdAt | date:'medium' }}</span>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">Payment Method</small>
                <span class="fw-bold">{{ booking.paymentMethod }}</span>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">Total Paid</small>
                <span class="fw-bold fs-5 text-success">₹{{ booking.totalAmount }}</span>
              </div>
            </div>

            <h6 class="text-muted mb-3 border-bottom pb-2">Flight Information</h6>
            <div class="row mb-4">
              <div class="col-md-6 mb-3">
                <div class="d-flex align-items-center">
                  <div class="me-3">
                    <i class="bi bi-airplane-engines fs-1 text-primary"></i>
                  </div>
                  <div>
                    <h5 class="mb-0 fw-bold">{{ booking.flightNumber || 'Flight' }}</h5>
                    <span class="badge bg-secondary">{{ booking.class }} Class</span>
                  </div>
                </div>
              </div>
              <div class="col-md-6 mb-3 text-md-end">
                <div class="p-3 border rounded border-primary bg-primary bg-opacity-10 text-center text-md-end d-inline-block">
                  <small class="text-muted d-block">Assigned Seat</small>
                  <span class="fw-bold fs-3 text-primary"><i class="bi bi-person-fill me-2"></i>{{ booking.seatNumber }}</span>
                </div>
              </div>
            </div>

            <div class="row align-items-center mb-4 px-3">
              <div class="col-4 text-center text-md-start">
                <h3 class="fw-bold text-dark mb-0">{{ booking.origin }}</h3>
                <small class="text-muted">Origin</small>
                <p class="fw-bold mt-2">{{ booking.departureTime | date:'dd MMM yyyy, HH:mm' }}</p>
              </div>
              <div class="col-4 text-center">
                <i class="bi bi-arrow-right fs-2 text-muted"></i>
              </div>
              <div class="col-4 text-center text-md-end">
                <h3 class="fw-bold text-dark mb-0">{{ booking.destination }}</h3>
                <small class="text-muted">Destination</small>
                <p class="fw-bold mt-2">{{ booking.departureTime | date:'dd MMM yyyy, HH:mm' }}</p>
              </div>
            </div>

            <h6 class="text-muted mb-3 border-bottom pb-2">Passenger Information</h6>
            <div class="row mb-4 bg-light p-3 rounded mx-0">
              <div class="col-md-6 mb-3 mb-md-0">
                <small class="text-muted d-block">Passenger Name</small>
                <span class="fw-bold">{{ booking.passengerName }}</span>
              </div>
              <div class="col-md-6">
                <small class="text-muted d-block">Email Address</small>
                <span class="fw-bold text-primary">{{ booking.passengerEmail }}</span>
              </div>
            </div>

            <div class="text-center mt-5">
               <button class="btn btn-outline-secondary" onclick="window.print()">
                 <i class="bi bi-printer me-2"></i>Print Ticket
               </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .booking-detail-page {
      background-color: #f8f9fa;
      min-height: 100vh;
    }
    @media print {
      .btn-link, .btn-outline-secondary { display: none !important; }
      .card { border: 2px solid #000 !important; box-shadow: none !important; }
      .booking-detail-page { background-color: #fff; }
    }
  `]
})
export class BookingDetail implements OnInit {
  bookingId: number = 0;
  booking: any = null;
  loading: boolean = true;
  error: string = '';

  constructor(
    private route: ActivatedRoute,
    private bookingService: BookingService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.bookingId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadBookingDetails();
  }

  loadBookingDetails() {
    this.loading = true;
    this.bookingService.getMyBookings().subscribe({
      next: (bookings: any[]) => {
        // Find the specific booking locally since getBookingStatus just returns { status: "" } in some APIs, 
        // while getMyBookings returns the full detailed DTO list for this user.
        this.booking = bookings.find(b => b.bookingId === this.bookingId);
        
        if (!this.booking) {
          this.error = "Booking not found.";
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = "Failed to load booking details.";
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}