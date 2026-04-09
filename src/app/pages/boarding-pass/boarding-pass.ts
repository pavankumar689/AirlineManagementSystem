import { Component, OnInit, ElementRef, ViewChild, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BookingService } from '../../services/booking';
import QRCode from 'qrcode';

@Component({
  selector: 'app-boarding-pass',
  imports: [CommonModule, RouterLink],
  templateUrl: './boarding-pass.html',
  styleUrl: './boarding-pass.scss'
})
export class BoardingPass implements OnInit {
  booking: any = null;
  loading = true;
  error = '';
  qrDataUrl = '';

  @ViewChild('qrCanvas') qrCanvas!: ElementRef;

  constructor(
    private route: ActivatedRoute,
    private bookingService: BookingService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.bookingService.getBookingById(id).subscribe({
      next: (data: any) => {
        this.booking = data;
        this.loading = false;
        if (data.bookingStatus === 'Confirmed') {
          this.generateQR(data).then(() => {
            this.cdr.detectChanges();
          });
        } else {
          this.cdr.detectChanges();
        }
      },
      error: () => {
        this.error = 'Failed to load booking';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  async generateQR(booking: any) {
    const qrData = JSON.stringify({
      bookingId: booking.bookingId,
      flight: booking.flightNumber,
      passenger: booking.passengerName,
      seat: booking.seatNumber,
      from: booking.origin,
      to: booking.destination,
      departure: booking.departureTime
    });

    this.qrDataUrl = await QRCode.toDataURL(qrData, {
      width: 180,
      margin: 1,
      color: {
        dark: '#1a237e',
        light: '#ffffff'
      }
    });
  }

  getBoardingTime(): string {
    if (!this.booking?.departureTime) return '';
    const boarding = new Date(this.booking.departureTime);
    boarding.setMinutes(boarding.getMinutes() - 30);
    return boarding.toLocaleTimeString('en-IN', {
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  printPass() {
    window.print();
  }
}