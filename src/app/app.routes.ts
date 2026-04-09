import { Routes } from '@angular/router';
import { LandingComponent } from './pages/landing/landing';
import { Home } from './pages/home/home';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { ForgotPassword } from './pages/forgot-password/forgot-password';
import { ResetPassword } from './pages/reset-password/reset-password';
import { SearchResults } from './pages/search-results/search-results';
import { BookingComponent } from './pages/booking/booking';
import { MyBookings } from './pages/my-bookings/my-bookings';
import { BookingDetail } from './pages/booking-detail/booking-detail';
import { authGuard } from './guards/auth.guard';
import { FlightStatus } from './pages/flight-status/flight-status';
import { BoardingPass } from './pages/boarding-pass/boarding-pass';
import { Profile } from './pages/profile/profile';
import { FlightAlertsComponent } from './pages/flight-alerts/flight-alerts';
import { BaggageInfoComponent } from './pages/baggage-info/baggage-info';

export const routes: Routes = [
  { path: '', component: LandingComponent },
  { path: 'home', component: Home },
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: 'forgot-password', component: ForgotPassword },
  { path: 'reset-password', component: ResetPassword },
  { path: 'search', component: SearchResults },
  { path: 'booking/:id', component: BookingComponent, canActivate: [authGuard] },
  { path: 'my-bookings', component: MyBookings, canActivate: [authGuard] },
  { path: 'booking-detail/:id', component: BookingDetail, canActivate: [authGuard] },
  { path: 'profile', component: Profile, canActivate: [authGuard] },
  { path: 'baggage-info', component: BaggageInfoComponent },
  { path: 'boarding-pass/:id', component: BoardingPass, canActivate: [authGuard] },
  { path: 'flight-alerts', component: FlightAlertsComponent, canActivate: [authGuard] },
  { path: 'flight-status', component: FlightStatus },
  { path: '**', redirectTo: '' }
];