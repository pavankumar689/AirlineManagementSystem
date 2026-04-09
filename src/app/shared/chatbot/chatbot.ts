import {
  Component, OnInit, ViewChild, ElementRef,
  AfterViewChecked, ChangeDetectorRef, HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatbotService, ChatMessage } from '../../services/chatbot';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-chatbot',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chatbot.html',
  styleUrl: './chatbot.scss'
})
export class ChatbotComponent implements OnInit, AfterViewChecked {
  isOpen = false;
  isMinimized = false;
  messages: ChatMessage[] = [];
  userInput = '';
  isLoading = false;
  suggestedQuestions: string[] = [];
  showSuggestions = true;
  hasNewMessage = false;

  @ViewChild('messagesList') messagesList!: ElementRef;
  private shouldScroll = false;

  constructor(
    private chatbot: ChatbotService,
    public auth: AuthService,
    private cdr: ChangeDetectorRef,
    private el: ElementRef
  ) {}

  @HostListener('document:mousedown', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (this.isOpen && !this.el.nativeElement.contains(event.target)) {
      this.minimize();
      this.cdr.detectChanges();
    }
  }

  async ngOnInit() {
    this.suggestedQuestions = await this.chatbot.getSuggestedQuestions(this.auth.isLoggedIn());
    // Greeting on open
    this.messages.push({
      role: 'assistant',
      content: this.getGreeting(),
      timestamp: new Date()
    });
  }

  ngAfterViewChecked() {
    if (this.shouldScroll) {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
  }

  getGreeting(): string {
    const hour = new Date().getHours();
    const timeGreet = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';
    const name = this.auth.isLoggedIn() ? ` ${this.auth.getName().split(' ')[0]}` : '';
    return `✈️ ${timeGreet}${name}! I'm **Aria**, your Veloskyra travel assistant.\n\nI can help you with:\n• Your bookings & PNR details\n• Flight schedules & pricing\n• Baggage, check-in, seat info\n• Cancellations & refunds\n\nWhat can I do for you today?`;
  }

  toggleChat() {
    this.isOpen = !this.isOpen;
    if (this.isOpen) {
      this.hasNewMessage = false;
      this.isMinimized = false;
      this.shouldScroll = true;
    }
  }

  minimize() {
    this.isMinimized = true;
    this.isOpen = false;
  }

  async sendMessage(text?: string) {
    const message = (text || this.userInput).trim();
    if (!message || this.isLoading) return;

    this.userInput = '';
    this.showSuggestions = false;

    // Add user message
    this.messages.push({
      role: 'user',
      content: message,
      timestamp: new Date()
    });

    // Add loading placeholder
    const loadingMsg: ChatMessage = {
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isLoading: true
    };
    this.messages.push(loadingMsg);
    this.isLoading = true;
    this.shouldScroll = true;
    this.cdr.detectChanges();

    try {
      const response = await this.chatbot.sendMessage(message);
      // Replace loading with real response
      const idx = this.messages.indexOf(loadingMsg);
      if (idx > -1) {
        this.messages[idx] = {
          role: 'assistant',
          content: response,
          timestamp: new Date()
        };
      }
      if (!this.isOpen) {
        this.hasNewMessage = true;
      }
    } catch {
      const idx = this.messages.indexOf(loadingMsg);
      if (idx > -1) {
        this.messages[idx] = {
          role: 'assistant',
          content: "⚠️ Something went wrong. Please try again!",
          timestamp: new Date()
        };
      }
    }

    this.isLoading = false;
    this.shouldScroll = true;
    this.cdr.detectChanges();
  }

  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  useSuggestion(q: string) {
    this.sendMessage(q);
  }

  async clearChat() {
    await this.chatbot.clearHistory();
    this.messages = [{
      role: 'assistant',
      content: this.getGreeting(),
      timestamp: new Date()
    }];
    this.showSuggestions = true;
    this.suggestedQuestions = await this.chatbot.getSuggestedQuestions(this.auth.isLoggedIn());
  }

  private scrollToBottom() {
    try {
      const el = this.messagesList?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    } catch {}
  }

  /** Render markdown-lite: bold, line breaks */
  renderContent(text: string): string {
    return text
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/\n/g, '<br>');
  }

  formatTime(date: Date): string {
    return date.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' });
  }
}
