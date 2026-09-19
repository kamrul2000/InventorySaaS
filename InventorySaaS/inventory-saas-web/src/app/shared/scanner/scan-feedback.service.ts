import { Injectable, signal } from '@angular/core';

const SOUND_PREFERENCE_KEY = 'inapp.scanner.sound';

/**
 * Audible and haptic confirmation for a scan.
 *
 * Warehouse staff scan without looking at the screen, so a distinguishable success/failure tone
 * matters more than the on-screen banner. Tones are synthesised with WebAudio rather than
 * shipped as audio files — no asset to load, and no delay on the first beep.
 */
@Injectable({ providedIn: 'root' })
export class ScanFeedbackService {
  /** Sound is on by default and remembered per device. */
  readonly soundEnabled = signal(this.readSoundPreference());

  private audioContext?: AudioContext;

  toggleSound(): void {
    const enabled = !this.soundEnabled();
    this.soundEnabled.set(enabled);

    try {
      localStorage.setItem(SOUND_PREFERENCE_KEY, String(enabled));
    } catch {
      // Private browsing or blocked storage: the preference just won't persist.
    }

    // Play the new state back so the toggle is self-evident.
    if (enabled) this.success();
  }

  /** A short high tone: the scan resolved. */
  success(): void {
    this.beep(880, 0.08);
    this.vibrate(40);
  }

  /** Two low tones: the scan did not resolve, or the operation was rejected. */
  failure(): void {
    this.beep(220, 0.14);
    setTimeout(() => this.beep(180, 0.18), 150);
    this.vibrate([60, 60, 60]);
  }

  /** A soft mid tone for a scan that was accepted but needs attention (e.g. a warning). */
  warning(): void {
    this.beep(520, 0.12);
    this.vibrate(30);
  }

  private beep(frequency: number, durationSeconds: number): void {
    if (!this.soundEnabled()) return;

    try {
      const context = this.getAudioContext();
      if (!context) return;

      // Browsers suspend the context until a user gesture; a scan follows one, so resume.
      if (context.state === 'suspended') void context.resume();

      const oscillator = context.createOscillator();
      const gain = context.createGain();

      oscillator.type = 'square';
      oscillator.frequency.value = frequency;

      // Ramp down instead of stopping abruptly, which would click audibly.
      gain.gain.setValueAtTime(0.12, context.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + durationSeconds);

      oscillator.connect(gain).connect(context.destination);
      oscillator.start();
      oscillator.stop(context.currentTime + durationSeconds);
    } catch {
      // Audio is a nicety; never let it break a scan.
    }
  }

  private vibrate(pattern: number | number[]): void {
    try {
      navigator.vibrate?.(pattern);
    } catch {
      // Unsupported on desktop and iOS Safari.
    }
  }

  private getAudioContext(): AudioContext | undefined {
    if (this.audioContext) return this.audioContext;

    const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) return undefined;

    this.audioContext = new Ctor();
    return this.audioContext;
  }

  private readSoundPreference(): boolean {
    try {
      return localStorage.getItem(SOUND_PREFERENCE_KEY) !== 'false';
    } catch {
      return true;
    }
  }
}
