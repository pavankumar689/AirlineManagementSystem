import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FlightService } from '../../services/flight';
import { SearchHistory, SearchQuery } from '../../services/search-history';

/**
 * The default Home/Landing page for the Passenger Portal.
 * Handles the main flight search widget, including retrieving search history and autocomplete data.
 */
@Component({
  selector: 'app-home',
  imports: [CommonModule, FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements OnInit {
  airports: any[] = [];    // List of available airports for the origin/destination dropdowns
  origin = '';             // Bound to the 'From' input area
  destination = '';        // Bound to the 'To' input area
  travelDate = '';         // Bound to the date picker
  seatClass = 'Economy';   // Bound to the class dropdown (Economy/Business/First)
  error = '';              // Warning validation messages
  searchHistory: SearchQuery[] = []; // User's previously searched queries

  /**
   * @param flightService Service used to get the dynamic list of airports.
   * @param router Angular's router to navigate to the search results page.
   * @param cdr CD object to prevent expression-changed-after-check errors.
   * @param searchHistoryService Service using LocalStorage to cache users recent searches.
   */
  constructor(
    private flightService: FlightService, 
    private router: Router,
    private cdr: ChangeDetectorRef,
    private searchHistoryService: SearchHistory
  ) {}

  /**
   * Angular OnInit lifecycle hook, executes once the component is mounted.
   */
  ngOnInit() {
    // 1. Fetch immediately anything stored in LocalStorage for search history
    this.searchHistory = this.searchHistoryService.getHistory();

    // 2. Fetch the latest live airports from the backend for dropdowns
    this.flightService.getAirports().subscribe({
      next: (data) => {
        this.airports = data;
        this.cdr.detectChanges();
      },
      error: () => {
        this.cdr.detectChanges();
      }
    });

    // 3. Set minimum date for the datepicker to today (users can't book past flights)
    const today = new Date();
    this.travelDate = today.toISOString().split('T')[0];
  }

  /**
   * Validates the inputs and navigates to the flight search results view.
   */
  search() {
    // Basic frontend validation
    if (!this.origin || !this.destination || !this.travelDate) {
      this.error = 'Please fill all fields';
      return;
    }
    // Prevent illogical searches
    if (this.origin === this.destination) {
      this.error = 'Origin and destination cannot be same';
      return;
    }
    this.error = '';

    // Save the search query into LocalStorage so it persists on reload
    this.searchHistoryService.addHistory({
      origin: this.origin,
      destination: this.destination,
      date: this.travelDate,
      class: this.seatClass
    });
    // Refresh history array
    this.searchHistory = this.searchHistoryService.getHistory();

    // Redirect the user to /search, passing their selections as URL Query Params
    this.router.navigate(['/search'], {
      queryParams: {
        origin: this.origin,
        destination: this.destination,
        date: this.travelDate,
        class: this.seatClass
      }
    });
  }

  /**
   * Utility to allow users to quickly rerun a past search with one click.
   * @param historyItem The cached parameters of the previous search
   */
  repeatSearch(historyItem: SearchQuery) {
    // Overwrite current inputs with the history item
    this.origin = historyItem.origin;
    this.destination = historyItem.destination;
    this.travelDate = historyItem.date;
    this.seatClass = historyItem.class;
    
    // Automatically trigger the new search
    this.search();
  }

  /**
   * Clears out all recent searches from the application's local storage and internal array.
   */
  clearHistory() {
    this.searchHistoryService.clearHistory();
    this.searchHistory = [];
  }
}