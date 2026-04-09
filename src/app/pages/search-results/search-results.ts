import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FlightService } from '../../services/flight';

@Component({
  selector: 'app-search-results',
  imports: [CommonModule],
  templateUrl: './search-results.html',
  styleUrl: './search-results.scss'
})
export class SearchResults implements OnInit {
  schedules: any[] = [];
  loading = true;
  error = '';
  origin = '';
  destination = '';
  date = '';
  seatClass = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private flightService: FlightService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.origin = params['origin'];
      this.destination = params['destination'];
      this.date = params['date'];
      this.seatClass = params['class'];
      this.searchFlights();
    });
  }

  searchFlights() {
    this.loading = true;
    this.error = '';
    this.flightService.searchFlights(
      this.origin,
      this.destination,
      this.date,
      this.seatClass
    ).subscribe({
      next: (data: any[]) => {
        this.schedules = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to load flights';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  bookFlight(scheduleId: number) {
    this.router.navigate(['/booking', scheduleId], {
      queryParams: { class: this.seatClass }
    });
  }

  getPrice(schedule: any): number {
    return this.seatClass === 'Business'
      ? schedule.businessPrice
      : schedule.economyPrice;
  }

  getAvailableSeats(schedule: any): number {
    return this.seatClass === 'Business'
      ? schedule.availableBusinessSeats
      : schedule.availableEconomySeats;
  }

  getDuration(departure: string, arrival: string): string {
    const diff = new Date(arrival).getTime() - new Date(departure).getTime();
    const hours = Math.floor(diff / 3600000);
    const minutes = Math.floor((diff % 3600000) / 60000);
    return `${hours}h ${minutes}m`;
  }

  goBack() {
    this.router.navigate(['/']);
  }
}