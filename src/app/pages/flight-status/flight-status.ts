import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FlightService } from '../../services/flight';

type ScheduleData = any[];

@Component({
  selector: 'app-flight-status',
  imports: [CommonModule, FormsModule],
  templateUrl: './flight-status.html',
  styleUrl: './flight-status.scss'
})
export class FlightStatus implements OnInit {
  schedules: any[] = [];
  filtered: any[] = [];
  loading = true;
  searchFlight = '';
  error = '';

  constructor(
    private flightService: FlightService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.flightService.getAllSchedules().subscribe({
      next: (data: ScheduleData) => {
        this.schedules = data;
        this.filtered = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to load flight status';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  search() {
    if (!this.searchFlight.trim()) {
      this.filtered = this.schedules;
      this.cdr.detectChanges();
      return;
    }
    this.filtered = this.schedules.filter(s =>
      s.flight?.flightNumber?.toLowerCase()
        .includes(this.searchFlight.toLowerCase()) ||
      s.flight?.originAirport?.code?.toLowerCase()
        .includes(this.searchFlight.toLowerCase()) ||
      s.flight?.destinationAirport?.code?.toLowerCase()
        .includes(this.searchFlight.toLowerCase())
    );
    this.cdr.detectChanges();
  }

  getStatusClass(status: string): string {
    if (status === 'Scheduled') return 'status-scheduled';
    if (status === 'Delayed') return 'status-delayed';
    if (status === 'Cancelled') return 'status-cancelled';
    return 'status-scheduled';
  }

  getStatusIcon(status: string): string {
    if (status === 'Scheduled') return 'bi-check-circle-fill';
    if (status === 'Delayed') return 'bi-clock-fill';
    if (status === 'Cancelled') return 'bi-x-circle-fill';
    return 'bi-check-circle-fill';
  }

  getDuration(departure: string, arrival: string): string {
    const diff = new Date(arrival).getTime() - new Date(departure).getTime();
    const hours = Math.floor(diff / 3600000);
    const minutes = Math.floor((diff % 3600000) / 60000);
    return `${hours}h ${minutes}m`;
  }
}