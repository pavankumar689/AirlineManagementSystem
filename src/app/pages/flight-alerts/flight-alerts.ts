import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookingService } from '../../services/booking';
import { FlightService } from '../../services/flight';

@Component({
  selector: 'app-flight-alerts',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './flight-alerts.html',
  styleUrl: './flight-alerts.scss'
})
export class FlightAlertsComponent implements OnInit {
  alerts: any[] = [];
  schedules: any[] = [];
  loading = true;
  success = '';
  error = '';

  constructor(
    private bookingService: BookingService,
    private flightService: FlightService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadAlerts();
    this.loadSchedules();
  }

  loadAlerts() {
    this.bookingService.getMyAlerts().subscribe({
      next: (data: any[]) => {
        this.alerts = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => { 
        this.loading = false; 
        this.cdr.detectChanges();
      }
    });
  }

  loadSchedules() {
    this.flightService.getAllSchedules().subscribe({
      next: (data: any[]) => { 
        this.schedules = data; 
        this.cdr.detectChanges();
      },
      error: () => {}
    });
  }

  subscribe(schedule: any) {
    this.bookingService.subscribeAlert({
      scheduleId: schedule.id,
      flightNumber: schedule.flight?.flightNumber,
      origin: schedule.flight?.originAirport?.code,
      destination: schedule.flight?.destinationAirport?.code,
      departureTime: schedule.departureTime
    }).subscribe({
      next: () => {
        this.success = `Subscribed to alerts for flight ${schedule.flight?.flightNumber}`;
        this.loadAlerts();
        setTimeout(() => { this.success = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message || 'Failed to subscribe';
        setTimeout(() => { this.error = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      }
    });
  }

  unsubscribe(id: number) {
    if (!confirm('Unsubscribe from this alert?')) return;
    this.bookingService.unsubscribeAlert(id).subscribe({
      next: () => {
        this.success = 'Unsubscribed successfully';
        this.loadAlerts();
        setTimeout(() => { this.success = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to unsubscribe';
        setTimeout(() => { this.error = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      }
    });
  }

  isSubscribed(scheduleId: number): boolean {
    return this.alerts.some(a => a.scheduleId === scheduleId);
  }
}