import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';

/**
 * Capture for USB and Bluetooth barcode scanners, which present themselves as keyboards, plus
 * manual entry for when no scanner or camera is available.
 *
 * Two behaviours make this work on a warehouse floor:
 *
 * 1. **Scanner vs. human.** A wedge scanner emits a whole code in a few milliseconds per
 *    character and usually ends with Enter. Some models omit the Enter, so a burst that was
 *    fast enough to be machine-typed is submitted on its own after a short quiet period.
 *    Slow, human typing is never auto-submitted — the operator presses Enter or the button.
 *
 * 2. **Focus retention.** Staff scan one item after another without touching the screen. The
 *    field pulls focus back when it loses it, so the next scan always lands somewhere useful.
 */
@Component({
  selector: 'app-scan-input',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './scan-input.component.html',
  styleUrl: './scan-input.component.css',
})
export class ScanInputComponent {
  /** Gaps at or below this are machine-fast; no human types a code this quickly. */
  private static readonly ScannerMaxGapMs = 35;

  /** Silence after the last keystroke before an Enter-less scan is accepted. */
  private static readonly QuietPeriodMs = 120;

  /** Shorter bursts are too easily produced by hand to auto-submit. */
  private static readonly MinAutoSubmitLength = 4;

  readonly label = input('Scan or type a barcode');
  readonly placeholder = input('Ready to scan…');
  readonly disabled = input(false);

  /** Keeps pulling focus back so consecutive scans need no tap. Off for forms with other fields. */
  readonly autoFocus = input(true);

  /** Clears the field after each submit, which is what repeated scanning wants. */
  readonly clearOnSubmit = input(true);

  readonly scanned = output<string>();

  private readonly inputRef = viewChild<ElementRef<HTMLInputElement>>('field');
  private readonly destroyRef = inject(DestroyRef);

  readonly value = signal('');
  readonly focused = signal(false);

  /** True when the last burst arrived at machine speed — surfaced so staff can see it worked. */
  readonly lastWasScanner = signal(false);

  readonly canSubmit = computed(() => this.value().trim().length > 0 && !this.disabled());

  private keyTimestamps: number[] = [];
  private quietTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    this.destroyRef.onDestroy(() => this.clearQuietTimer());
  }

  /** Puts the cursor in the field; call after a dialog closes or a step advances. */
  focus(): void {
    // A microtask delay lets the element exist after an @if flips.
    queueMicrotask(() => this.inputRef()?.nativeElement.focus());
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.clearQuietTimer();
      this.submit();
      return;
    }

    // Only printable characters count toward the timing profile.
    if (event.key.length === 1) this.keyTimestamps.push(performance.now());
  }

  onInput(raw: string): void {
    this.value.set(raw);
    this.scheduleQuietCheck();
  }

  onBlur(): void {
    this.focused.set(false);

    if (!this.autoFocus() || this.disabled()) return;

    // Give a genuine click on another control time to land before stealing focus back.
    setTimeout(() => {
      if (!this.focused() && this.autoFocus() && !this.disabled()) {
        this.inputRef()?.nativeElement.focus();
      }
    }, 120);
  }

  submit(): void {
    const value = this.value().trim();
    if (!value || this.disabled()) return;

    this.lastWasScanner.set(this.burstLooksLikeAScanner());
    this.resetTiming();

    if (this.clearOnSubmit()) this.value.set('');

    this.scanned.emit(value);
    if (this.autoFocus()) this.focus();
  }

  clear(): void {
    this.value.set('');
    this.resetTiming();
    this.focus();
  }

  /**
   * Handles scanners that send no terminator: once input goes quiet, accept the burst if it
   * arrived too fast to have been typed.
   */
  private scheduleQuietCheck(): void {
    this.clearQuietTimer();

    this.quietTimer = setTimeout(() => {
      const value = this.value().trim();
      if (value.length >= ScanInputComponent.MinAutoSubmitLength && this.burstLooksLikeAScanner()) {
        this.submit();
      }
    }, ScanInputComponent.QuietPeriodMs);
  }

  /** Median gap rather than mean, so one stray pause doesn't disqualify a real scan. */
  private burstLooksLikeAScanner(): boolean {
    if (this.keyTimestamps.length < ScanInputComponent.MinAutoSubmitLength) return false;

    const gaps: number[] = [];
    for (let i = 1; i < this.keyTimestamps.length; i++) {
      gaps.push(this.keyTimestamps[i] - this.keyTimestamps[i - 1]);
    }
    if (gaps.length === 0) return false;

    gaps.sort((a, b) => a - b);
    const median = gaps[Math.floor(gaps.length / 2)];

    return median <= ScanInputComponent.ScannerMaxGapMs;
  }

  private resetTiming(): void {
    this.keyTimestamps = [];
    this.clearQuietTimer();
  }

  private clearQuietTimer(): void {
    if (this.quietTimer) {
      clearTimeout(this.quietTimer);
      this.quietTimer = undefined;
    }
  }
}
