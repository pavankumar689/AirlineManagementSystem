import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FlightService } from '../../services/flight';
import { BookingService } from '../../services/booking';
import { AuthService } from '../../services/auth';

declare var Razorpay: any;

@Component({
  selector: 'app-booking',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './booking.html',
  styleUrls: ['./booking.scss']
})
export class BookingComponent implements OnInit {
  schedule: any = null;
  loading = true;
  booking = false;
  error = '';
  success = false;
  bookedData: any = null;

  scheduleId = 0;
  seatClass = 'Economy';
  paymentMethod = 'UPI';

  // Reward Points
  rewardPointsBalance = 0;
  useRewardPoints = false;
  pointsToRedeem = 0;   // Points user wants to use (auto-capped at 60%)
  rewardDiscount = 0;   // ₹ discount from reward points

  // Multi-passenger support
  passengers: { name: string; email: string; seatNumber?: string }[] = [{ name: '', email: '', seatNumber: '' }];

  // Seat selection
  occupiedSeats: string[] = [];
  selectedSeats: string[] = [];
  rows: number[] = [];

  constructor(
    private route: ActivatedRoute,
    public router: Router,
    private flightService: FlightService,
    private bookingService: BookingService,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.scheduleId = Number(this.route.snapshot.paramMap.get('id'));
    this.seatClass = this.route.snapshot.queryParams['class'] || 'Economy';
    this.loadSchedule();
    this.loadOccupiedSeats();
    this.generateSeatMap();
    this.loadRewardPoints();
  }

  loadRewardPoints() {
    if (!this.auth.isLoggedIn()) return;
    this.auth.getRewardPoints().subscribe({
      next: (data) => {
        this.rewardPointsBalance = data.balance || 0;
        this.cdr.detectChanges();
      }
    });
  }

  toggleRewardPoints() {
    this.useRewardPoints = !this.useRewardPoints;
    if (this.useRewardPoints) {
      this.recalcRewardDiscount();
    } else {
      this.rewardDiscount = 0;
      this.pointsToRedeem = 0;
    }
  }

  recalcRewardDiscount() {
    const total = this.getBasePrice() + this.getTax();
    const maxDiscount = total * 0.60;                     // 60% cap
    const maxPointsUsable = Math.min(this.rewardPointsBalance, Math.floor(maxDiscount));
    this.pointsToRedeem = maxPointsUsable;
    this.rewardDiscount = maxPointsUsable;                // 1 pt = ₹1
  }

  loadOccupiedSeats() {
    this.bookingService.getOccupiedSeats(this.scheduleId).subscribe({
      next: (data) => {
        this.occupiedSeats = data;
        this.cdr.detectChanges();
      }
    });
  }

  generateSeatMap() {
    const totalRows = this.seatClass === 'Business' ? 6 : 20;
    this.rows = Array.from({ length: totalRows }, (_, i) => i + 1);
  }

  getSeatPrefix(): string {
    return this.seatClass === 'Business' ? 'B' : 'E';
  }

  isWindowSeat(seatLabel: string): boolean {
    return seatLabel.endsWith('A') || seatLabel.endsWith('F');
  }

  getSeatPrice(seatLabel: string): number {
    const base = this.getUnitPrice();
    return this.isWindowSeat(seatLabel) ? Math.round(base * 1.15) : base;
  }

  getSeatSurcharge(seatLabel: string): number {
    const base = this.getUnitPrice();
    return this.isWindowSeat(seatLabel) ? Math.round(base * 0.15) : 0;
  }

  toggleSeat(seatLabel: string) {
    if (this.occupiedSeats.includes(seatLabel)) return;

    if (this.selectedSeats.includes(seatLabel)) {
        this.selectedSeats = this.selectedSeats.filter(s => s !== seatLabel);
    } else {
        if (this.selectedSeats.length < this.passengers.length) {
            this.selectedSeats.push(seatLabel);
        } else {
            this.error = 'You have already selected seats for all passengers.';
            setTimeout(() => this.error = '', 3000);
        }
    }
  }

  loadSchedule() {
    this.flightService.getScheduleById(this.scheduleId).subscribe({
      next: (data: unknown) => {
        this.schedule = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (e: any) => {
        this.error = 'Failed to load flight details';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  addPassenger() {
    const maxSeats = this.seatClass === 'Business'
      ? this.schedule?.availableBusinessSeats
      : this.schedule?.availableEconomySeats;
    if (this.passengers.length >= maxSeats) {
      this.error = 'Cannot add more passengers than available seats';
      return;
    }
    this.passengers.push({ name: '', email: '', seatNumber: '' });
  }

  removePassenger(index: number) {
    if (this.passengers.length > 1) {
      this.passengers.splice(index, 1);
      // Remove last selected seat if removing passenger decreases seat requirements
      if (this.selectedSeats.length > this.passengers.length) {
        this.selectedSeats.pop();
      }
    }
  }

  getUnitPrice(): number {
    if (!this.schedule) return 0;
    return this.seatClass === 'Business'
      ? this.schedule.businessPrice
      : this.schedule.economyPrice;
  }

  getBasePrice(): number {
    let total = 0;
    if (this.selectedSeats.length > 0) {
        total = this.selectedSeats.reduce((sum, seat) => sum + this.getSeatPrice(seat), 0);
    } else {
        total = this.getUnitPrice() * this.passengers.length;
    }
    return Math.round(total);
  }

  getTax(): number {
    return Math.round(this.getBasePrice() * 0.18);
  }

  getTotal(): number {
    const raw = this.getBasePrice() + this.getTax() - this.rewardDiscount;
    return Math.max(0, Math.round(raw));
  }

  confirmBooking() {
    if (!this.auth.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    const invalid = this.passengers.some(p => !p.name.trim());
    if (invalid) {
      this.error = 'Please enter a name for every passenger';
      return;
    }

    if (this.selectedSeats.length < this.passengers.length) {
      this.error = `Please select ${this.passengers.length} seat(s) from the seat map.`;
      return;
    }

    this.booking = true;
    this.error = '';

    this.bookingService.createBooking({
      scheduleId: this.scheduleId,
      class: this.seatClass,
      paymentMethod: this.paymentMethod,
      passengers: this.passengers.map((p, i) => ({
        name: p.name.trim(),
        email: p.email.trim() || this.auth.getEmail(),
        seatNumber: this.selectedSeats[i]
      }))
    }).subscribe({
      next: (bookingData: any) => {
        this.bookedData = bookingData;
        // If reward points applied, redeem them now before payment
        if (this.useRewardPoints && this.pointsToRedeem > 0) {
          this.auth.redeemPoints(
            this.pointsToRedeem,
            this.getBasePrice() + this.getTax(),
            bookingData.bookingId.toString()
          ).subscribe({
            next: (redeemResult) => {
              this.rewardDiscount = redeemResult.discountAmount;
              this.rewardPointsBalance = redeemResult.remainingBalance;
              this.createRazorpayOrder(bookingData);
            },
            error: () => {
              // Redemption failed — proceed without discount
              this.rewardDiscount = 0;
              this.createRazorpayOrder(bookingData);
            }
          });
        } else {
          this.createRazorpayOrder(bookingData);
        }
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Booking failed';
        this.booking = false;
        this.cdr.detectChanges();
      }
    });
  }

  createRazorpayOrder(bookingData: any) {
    this.bookingService.createRazorpayOrder({
      amount: this.getTotal(),
      bookingId: bookingData.bookingId
    }).subscribe({
      next: (orderData: any) => {
        this.openRazorpay(orderData, bookingData);
      },
      error: (err: any) => {
        this.error = 'Payment gateway error: ' + (err.error?.message || err.message || 'Unknown error');
        this.booking = false;
        this.cdr.detectChanges();
      }
    });
  }

  openRazorpay(orderData: any, bookingData: any) {
    try {
      const rzpKey = orderData.keyId || orderData.KeyId;
      const rzpOrderId = orderData.orderId || orderData.OrderId;
      const rzpAmount = orderData.amount || orderData.Amount;
      const options = {
        key: rzpKey,
        amount: rzpAmount * 100,
        currency: 'INR',
        name: 'Veloskyra Airlines',
        description: `Flight ${bookingData.flightNumber} · ${this.passengers.length} Passenger(s)`,
        order_id: rzpOrderId,
        prefill: {
          name: this.auth.getName(),
          email: bookingData.passengerEmail || 'test@example.com'
        },
        theme: { color: '#1a237e' },
        handler: (response: any) => {
          this.verifyPayment(response, bookingData);
        },
        modal: {
          ondismiss: () => {
            this.booking = false;
            this.error = 'Payment cancelled';
            this.cdr.detectChanges();
          }
        }
      };

      const rzp = new Razorpay(options);
      rzp.on('payment.failed', function (response: any) {
        alert('Payment Failed: ' + response.error.description);
      });
      rzp.open();
    } catch (e: any) {
      this.error = 'Error initializing payment gateway: ' + (e.message || 'Unknown error');
      this.cdr.detectChanges();
    } finally {
      this.booking = false;
      this.cdr.detectChanges();
    }
  }

  verifyPayment(response: any, bookingData: any) {
    this.booking = true;
    this.bookingService.verifyPayment({
      orderId: response.razorpay_order_id,
      paymentId: response.razorpay_payment_id,
      signature: response.razorpay_signature,
      bookingId: bookingData.bookingId
    }).subscribe({
      next: () => {
        this.success = true;
        this.booking = false;
        if (this.bookedData) {
          this.bookedData.bookingStatus = 'Confirmed';
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Payment verification failed';
        this.booking = false;
        this.cdr.detectChanges();
      }
    });
  }

  goToMyBookings() {
    this.router.navigate(['/my-bookings']);
  }

  goBack() {
    window.history.back();
  }
}