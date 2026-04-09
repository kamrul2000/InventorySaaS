import { Component, ElementRef, NgZone, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { AiChatService, ChatMessage } from '../../core/services/ai-chat.service';

@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './chat-panel.component.html',
  styleUrl: './chat-panel.component.css',
})
export class ChatPanelComponent {
  messages: ChatMessage[] = [];
  inputText = '';
  isLoading = false;
  private abortController: AbortController | null = null;
  private readonly MAX_HISTORY = 10;

  @ViewChild('messagesContainer') messagesContainer!: ElementRef;

  constructor(private chatService: AiChatService, private ngZone: NgZone) {}

  async sendMessage(): Promise<void> {
    const text = this.inputText.trim();
    if (!text || this.isLoading) return;

    this.messages.push({ role: 'user', content: text });
    this.inputText = '';
    this.isLoading = true;
    this.scrollToBottom();

    this.messages.push({ role: 'assistant', content: '' });
    const assistantIndex = this.messages.length - 1;

    const history = this.messages.slice(0, -1).slice(-this.MAX_HISTORY);

    this.abortController = await this.chatService.streamChat(
      text,
      history,
      (chunk) => {
        this.ngZone.run(() => {
          this.messages[assistantIndex].content += chunk;
          this.scrollToBottom();
        });
      },
      () => {
        this.ngZone.run(() => {
          this.isLoading = false;
        });
      },
      (error) => {
        this.ngZone.run(() => {
          this.messages[assistantIndex].content = `Error: ${error}`;
          this.isLoading = false;
        });
      }
    );
  }

  sendSuggestion(text: string): void {
    this.inputText = text;
    this.sendMessage();
  }

  cancelStream(): void {
    this.abortController?.abort();
    this.isLoading = false;
  }

  clearChat(): void {
    this.messages = [];
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    }, 0);
  }
}
