import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class FlightService {
  private url = environment.flightUrl;

  constructor(private http: HttpClient) {}

  getAirports() {
    return this.http.get<any[]>(`${this.url}/api/airport`);
  }

  searchFlights(origin: string, destination: string, date: string, seatClass: string) {
    return this.http.get<any[]>(
      `${this.url}/api/schedule/search?OriginCode=${origin}&DestinationCode=${destination}&TravelDate=${date}&Class=${seatClass}`
    );
  }

  getScheduleById(id: number) {
    return this.http.get<any>(`${this.url}/api/schedule/${id}`);
  }
  getAllSchedules() {
    return this.http.get<any[]>(`${this.url}/api/schedule`);
  }

getScheduleByFlight(flightNumber: string) {
  return this.http.get<any[]>(
    `${this.url}/api/schedule/search`
  );
}
}