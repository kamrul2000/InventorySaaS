import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

@Injectable({ providedIn: 'root' })
export class AiChatService {
  private readonly endpoint = `${environment.apiUrl}/api/v1/chat`;

  async streamChat(
    message: string,
    history: ChatMessage[],
    onChunk: (text: string) => void,
    onDone: () => void,
    onError: (error: string) => void
  ): Promise<AbortController> {
    const abortController = new AbortController();
    const token = localStorage.getItem('accessToken');

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (token) headers['Authorization'] = `Bearer ${token}`;

    try {
      const response = await fetch(this.endpoint, {
        method: 'POST',
        headers,
        body: JSON.stringify({ message, history }),
        signal: abortController.signal,
      });

      if (!response.ok) {
        onError(`Error: ${response.status}`);
        return abortController;
      }

      const reader = response.body!.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6);
            if (data === '[DONE]') {
              onDone();
              return abortController;
            }
            onChunk(data.replace(/\\n/g, '\n'));
          }
        }
      }
      onDone();
    } catch (err: any) {
      if (err.name !== 'AbortError') {
        onError(err.message || 'Connection failed');
      }
    }

    return abortController;
  }
}
