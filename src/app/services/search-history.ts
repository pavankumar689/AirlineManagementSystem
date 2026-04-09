import { Injectable } from '@angular/core';

export interface SearchQuery {
  origin: string;
  destination: string;
  date: string;
  class: string;
}

@Injectable({
  providedIn: 'root',
})
export class SearchHistory {
  private readonly STORAGE_KEY = 'veloskyra_search_history';

  getHistory(): SearchQuery[] {
    const history = localStorage.getItem(this.STORAGE_KEY);
    return history ? JSON.parse(history) : [];
  }

  addHistory(query: SearchQuery): void {
    const history = this.getHistory();
    // Prevent duplicate exact searches from flooding the history
    const filtered = history.filter(
      h => !(h.origin === query.origin && h.destination === query.destination && h.date === query.date && h.class === query.class)
    );
    // Add to the front of the list, keep max 5 recent searches
    filtered.unshift(query);
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(filtered.slice(0, 5)));
  }

  clearHistory(): void {
    localStorage.removeItem(this.STORAGE_KEY);
  }
}