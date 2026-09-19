import { Component, ViewChild, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { CameraScannerComponent } from './camera-scanner.component';
import { ScanInputComponent } from './scan-input.component';
import { ScanFeedbackService } from './scan-feedback.service';
import { isCameraScanningSupported } from './barcode-detector.types';

/**
 * The scanning widget feature pages embed: camera, USB/Bluetooth wedge capture and manual
 * entry behind one `scanned` output, plus the success/error banner and sound toggle.
 *
 * It owns presentation and feedback only. Resolving the code and deciding what to do with it
 * belongs to the page, which reports the outcome back via {@link reportSuccess} /
 * {@link reportFailure} so the banner and the beep reflect what actually happened.
 */
@Component({
  selector: 'app-scan-target',
  standalone: true,
  imports: [CommonModule, MatIconModule, CameraScannerComponent, ScanInputComponent],
  templateUrl: './scan-target.component.html',
  styleUrl: './scan-target.component.css',
})
export class ScanTargetComponent {
  readonly label = input('Scan or type a barcode');
  readonly placeholder = input('Ready to scan…');

  /** Blocks input while the page is posting, so a double scan can't queue a second request. */
  readonly busy = input(false);

  readonly scanned = output<string>();

  @ViewChild(ScanInputComponent) private scanInput?: ScanInputComponent;
  @ViewChild(CameraScannerComponent) private camera?: CameraScannerComponent;

  readonly feedback = inject(ScanFeedbackService);

  readonly cameraSupported = isCameraScanningSupported();
  readonly cameraOpen = signal(false);

  readonly status = signal<{ tone: 'success' | 'error' | 'warning'; text: string } | null>(null);
  readonly lastCode = signal('');

  readonly soundOn = computed(() => this.feedback.soundEnabled());

  onScanned(value: string): void {
    this.lastCode.set(value);
    this.status.set(null);
    this.scanned.emit(value);
  }

  /** Called by the host once the scan resolved and the action succeeded. */
  reportSuccess(text: string): void {
    this.status.set({ tone: 'success', text });
    this.feedback.success();
    this.refocus();
  }

  /** Called by the host when the code was unknown or the operation was rejected. */
  reportFailure(text: string): void {
    this.status.set({ tone: 'error', text });
    this.feedback.failure();
    this.refocus();
  }

  /** Accepted, but the operator needs to look at something before continuing. */
  reportWarning(text: string): void {
    this.status.set({ tone: 'warning', text });
    this.feedback.warning();
    this.refocus();
  }

  clearStatus(): void {
    this.status.set(null);
  }

  toggleCamera(): void {
    const open = !this.cameraOpen();
    this.cameraOpen.set(open);

    // Releasing the camera when the panel closes matters on phones, where a live stream is
    // a visible battery and privacy-light cost.
    if (!open) this.camera?.stop();
    else this.refocus();
  }

  toggleSound(): void {
    this.feedback.toggleSound();
  }

  /** Returns the keyboard focus to the scan field so the next scan lands. */
  refocus(): void {
    this.scanInput?.focus();
  }
}
