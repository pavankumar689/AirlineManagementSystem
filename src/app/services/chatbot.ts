import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth';

/**
 * Interface defining the structure of a chat message.
 * - role: indicates who sent the message (the user or the assistant/bot).
 * - content: the text of the message.
 * - timestamp: when the message was created.
 * - isLoading: optional flag to show a loading state while waiting for the bot's reply.
 */
export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  isLoading?: boolean;
}

/**
 * Injectable ChatbotService provided at the root level so there's only one instance (Singleton).
 * Handles all communication between the Passenger Portal and the Python Chatbot Microservice.
 */
@Injectable({ providedIn: 'root' })
export class ChatbotService {
  /** 
   * Base URL for the chatbot API. 
   * It is fetched from the environment variable (environment.apiUrl) and routed via Ocelot API Gateway.
   */
  private readonly CHATBOT_URL = `${environment.apiUrl}/chatbot/api/chatbot`;

  /** 
   * Holds the current session ID to maintain chat context across multiple turns.
   * If null, the server will start a new conversation context and return a new session ID.
   */
  private sessionId: string | null = null;

  /**
   * Constructor injects:
   * - HttpClient: to make HTTP requests to the backend.
   * - AuthService: to retrieve the logged-in user's token and name for personalization.
   */
  constructor(private http: HttpClient, private auth: AuthService) {}

  // ─── Public API ────────────────────────────────────────────────────────────

  /**
   * Sends a message to the chatbot.
   * @param userMessage The message typed by the user.
   * @returns A promise resolving to the chatbot's string reply.
   */
  async sendMessage(userMessage: string): Promise<string> {
    // 1. Prepare HTTP headers including auth token if the user is logged in
    const headers = this._buildHeaders();

    // 2. Prepare request payload. We pass the user's message and the current session ID.
    const body = {
      message: userMessage,
      session_id: this.sessionId,   // null on first call → server creates one
    };

    try {
      // 3. Make a POST request to the /message endpoint and wait for the response
      // firstValueFrom converts the RxJS Observable to a Promise
      const res = await firstValueFrom(
        this.http.post<{ reply: string; session_id: string }>(
          `${this.CHATBOT_URL}/message`,
          body,
          { headers }
        )
      );

      // 4. Update the local sessionId with the one returned from the server
      // This persists the session context for the next message
      this.sessionId = res.session_id;
      return res.reply;

    } catch (err: any) {
      // 5. Advanced error handling and logging
      console.error('[Aria] ChatbotService error:', err);
      // Try to extract a meaningful error message from the response properties
      const msg: string = err?.error?.detail || err?.message || '';
      
      // Handle standard network errors
      if (err?.status === 0)  return '🌐 Network error — check your connection.';
      // Handle rate limiting to avoid server overload
      if (err?.status === 429) return '⏳ Rate limit reached — please wait a moment.';
      
      // Fallback generic error message
      return `⚠️ ${msg || 'Something went wrong. Please try again.'}`;
    }
  }

  /**
   * Gets a list of suggested questions based on the user's login status.
   * @param isLoggedIn true if the user is currently authenticated.
   * @returns A promise resolving to an array of question strings.
   */
  async getSuggestedQuestions(isLoggedIn: boolean): Promise<string[]> {
    try {
      // Fetch dynamic suggestions from the Python microservice
      const res = await firstValueFrom(
        this.http.get<{ questions: string[] }>(
          `${this.CHATBOT_URL}/questions`,
          // Pass the logged-in boolean as a URL query parameter
          { params: { logged_in: String(isLoggedIn) } }
        )
      );
      return res.questions;
    } catch {
      // If the backend call fails, fallback to hardcoded default questions
      return this._defaultQuestions(isLoggedIn);
    }
  }

  /**
   * Clears the current conversation history on the server and resets the local sessionId.
   */
  async clearHistory(): Promise<void> {
    if (this.sessionId) {
      try {
        // HTTP DELETE call to the server to destroy session data
        await firstValueFrom(
          this.http.delete(`${this.CHATBOT_URL}/session/${this.sessionId}`)
        );
      } catch { 
        /* Silently ignore errors during deletion */ 
      }
    }
    // Set the local session back to null so the next message starts fresh
    this.sessionId = null;
  }

  // ─── Helpers ───────────────────────────────────────────────────────────────

  /**
   * Builds the HTTP headers for requests to the chatbot microservice.
   * Includes JWT Token for authorization and the passenger's name for personalized AI responses.
   */
  private _buildHeaders(): HttpHeaders {
    let headers = new HttpHeaders({ 'Content-Type': 'application/json' });

    // If the user has a valid authorization context, attach it to headers
    if (this.auth.isLoggedIn()) {
      const token = this.auth.getToken();
      // Attach Bearer token needed by the .NET API Gateway / Python Service backend authentication middleware
      if (token) headers = headers.set('Authorization', `Bearer ${token}`);
      
      const name = this.auth.getName();
      // Attach a custom header used by the bot to address the passenger by their actual name
      if (name)  headers = headers.set('X-Passenger-Name', name);
    }

    return headers;
  }

  /**
   * Provides hardcoded fallback questions when the server suggestion endpoint fails.
   */
  private _defaultQuestions(isLoggedIn: boolean): string[] {
    // If logged in, provide context-aware suggestions like "my bookings"
    if (isLoggedIn) {
      return [
        'What are my upcoming bookings?',
        'Can I cancel my booking?',
        'What is the baggage allowance?',
        'How do I use my reward points?',
        "What's the check-in time?",
        'Show me flights from Delhi',
      ];
    }
    // If not logged in, provide generic pre-booking questions
    return [
      'What flights are available today?',
      'What is the baggage allowance?',
      'How does seat selection work?',
      'What payment methods do you accept?',
      'Can I get a refund if I cancel?',
      'Tell me about Business class',
    ];
  }
}
