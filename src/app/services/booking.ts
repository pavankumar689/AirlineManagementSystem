import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private url = environment.bookingUrl;

  constructor(private http: HttpClient) {}

  createBooking(data: any) {
    return this.http.post<any>(`${this.url}/api/booking`, data);
  }

  getOccupiedSeats(scheduleId: number) {
    return this.http.get<string[]>(`${this.url}/api/booking/occupied-seats/${scheduleId}`);
  }

  getMyBookings() {
    return this.http.get<any[]>(`${this.url}/api/booking/my-bookings`);
  }

  cancelBooking(id: number) {
    return this.http.post<any>(`${this.url}/api/booking/${id}/cancel`, {});
  }

  getBookingStatus(id: number) {
    return this.http.get<any>(`${this.url}/api/booking/${id}/status`);
  }
  createRazorpayOrder(data: any) {
  return this.http.post<any>(
    `${environment.paymentUrl}/api/payment/create-order`, data);
}

verifyPayment(data: any) {
  return this.http.post<any>(
    `${environment.paymentUrl}/api/payment/verify`, data);
}
  getBookingById(id: number) {
  return this.http.get<any>(`${this.url}/api/booking/${id}`);
}
private notificationUrl = environment.notificationUrl;

subscribeAlert(data: any) {
  return this.http.post<any>(
    `${this.notificationUrl}/api/alert/subscribe`, data);
}

getMyAlerts() {
  return this.http.get<any[]>(
    `${this.notificationUrl}/api/alert/my-alerts`);
}

unsubscribeAlert(id: number) {
  return this.http.delete(
    `${this.notificationUrl}/api/alert/${id}`);
}
}