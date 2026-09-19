import {
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import {
  BarcodeDetectorInstance,
  REQUESTED_BARCODE_FORMATS,
  cameraUnsupportedReason,
  getBarcodeDetector,
  isCameraScanningSupported,
} from './barcode-detector.types';

/**
 * `torch` is a real capability on Android Chrome but is absent from the standard DOM typings,
 * so it is declared here and applied through a cast at the call site.
 */
type TorchCapabilities = MediaTrackCapabilities & { torch?: boolean };

@Component({
  selector: 'app-camera-scanner',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './camera-scanner.component.html',
  styleUrl: './camera-scanner.component.css',
})
export class CameraScannerComponent implements OnDestroy {
  /** How often the video frame is examined. ~10/sec reads fast without pinning the CPU. */
  private static readonly DetectIntervalMs = 100;

  /**
   * Ignore the same code re-read within this window. A barcode stays in frame for many
   * hundreds of milliseconds, so without this one physical scan would fire dozens of times.
   */
  private static readonly DuplicateSuppressionMs = 1500;

  /** Emitted once per distinct physical scan. */
  readonly scanned = output<string>();
  readonly cameraError = output<string>();

  /** Set false by the host to release the camera without destroying the component. */
  readonly active = input(true);

  private readonly videoRef = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly destroyRef = inject(DestroyRef);

  readonly running = signal(false);
  readonly starting = signal(false);
  readonly error = signal<string | null>(null);
  readonly torchOn = signal(false);
  readonly torchAvailable = signal(false);
  readonly hasMultipleCameras = signal(false);

  readonly supported = isCameraScanningSupported();
  readonly unsupportedReason = cameraUnsupportedReason();
  readonly canScan = computed(() => this.supported && !this.error());

  private stream?: MediaStream;
  private detector?: BarcodeDetectorInstance;
  private timer?: ReturnType<typeof setInterval>;
  private facingMode: 'environment' | 'user' = 'environment';
  private lastValue = '';
  private lastValueAt = 0;
  private detecting = false;

  constructor() {
    this.destroyRef.onDestroy(() => this.stop());
  }

  ngOnDestroy(): void {
    this.stop();
  }

  async start(): Promise<void> {
    if (this.running() || this.starting()) return;

    if (!this.supported) {
      this.error.set(this.unsupportedReason ?? 'Camera scanning is unavailable.');
      return;
    }

    this.starting.set(true);
    this.error.set(null);

    try {
      const Detector = getBarcodeDetector()!;

      // Ask only for formats this browser actually supports; passing an unsupported one
      // throws and would leave the camera running with nothing reading it.
      const supportedFormats = await Detector.getSupportedFormats();
      const formats = REQUESTED_BARCODE_FORMATS.filter((f) => supportedFormats.includes(f));
      this.detector = new Detector(formats.length > 0 ? { formats } : undefined);

      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: this.facingMode, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });

      const video = this.videoRef()?.nativeElement;
      if (!video) throw new Error('The camera preview is not ready.');

      video.srcObject = this.stream;
      // iOS refuses to play an inline video without both of these set before play().
      video.setAttribute('playsinline', 'true');
      video.muted = true;
      await video.play();

      this.detectTorchSupport();
      void this.detectMultipleCameras();

      this.timer = setInterval(() => void this.detectFrame(), CameraScannerComponent.DetectIntervalMs);
      this.running.set(true);
    } catch (error) {
      const message = this.describeCameraError(error);
      this.error.set(message);
      this.cameraError.emit(message);
      this.releaseStream();
    } finally {
      this.starting.set(false);
    }
  }

  stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = undefined;
    }

    this.releaseStream();
    this.running.set(false);
    this.torchOn.set(false);
  }

  async toggle(): Promise<void> {
    if (this.running()) this.stop();
    else await this.start();
  }

  /** Flips between the rear and front camera, restarting the stream on the new one. */
  async switchCamera(): Promise<void> {
    this.facingMode = this.facingMode === 'environment' ? 'user' : 'environment';
    if (this.running()) {
      this.stop();
      await this.start();
    }
  }

  async toggleTorch(): Promise<void> {
    const track = this.videoTrack();
    if (!track || !this.torchAvailable()) return;

    const next = !this.torchOn();
    try {
      await track.applyConstraints({
        advanced: [{ torch: next }],
      } as unknown as MediaTrackConstraints);
      this.torchOn.set(next);
    } catch {
      this.torchAvailable.set(false);
    }
  }

  private async detectFrame(): Promise<void> {
    // Detection is async and slower than the interval on cheap hardware; overlapping calls
    // would queue up and stall the UI thread.
    if (this.detecting || !this.detector || !this.active()) return;

    const video = this.videoRef()?.nativeElement;
    if (!video || video.readyState < HTMLMediaElement.HAVE_ENOUGH_DATA) return;

    this.detecting = true;
    try {
      const results = await this.detector.detect(video);
      const value = results[0]?.rawValue?.trim();
      if (value) this.emitIfNew(value);
    } catch {
      // A transient decode failure is normal between frames; keep scanning.
    } finally {
      this.detecting = false;
    }
  }

  private emitIfNew(value: string): void {
    const now = Date.now();
    const isRepeat =
      value === this.lastValue && now - this.lastValueAt < CameraScannerComponent.DuplicateSuppressionMs;

    // Refresh the timestamp either way, so a barcode held in frame stays suppressed.
    this.lastValue = value;
    this.lastValueAt = now;

    if (!isRepeat) this.scanned.emit(value);
  }

  private detectTorchSupport(): void {
    const capabilities = this.videoTrack()?.getCapabilities?.() as TorchCapabilities | undefined;
    this.torchAvailable.set(capabilities?.torch === true);
  }

  private async detectMultipleCameras(): Promise<void> {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      this.hasMultipleCameras.set(devices.filter((d) => d.kind === 'videoinput').length > 1);
    } catch {
      this.hasMultipleCameras.set(false);
    }
  }

  private videoTrack(): MediaStreamTrack | undefined {
    return this.stream?.getVideoTracks()[0];
  }

  private releaseStream(): void {
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = undefined;

    const video = this.videoRef()?.nativeElement;
    if (video) video.srcObject = null;
  }

  /** Turns a DOMException into something a warehouse user can act on. */
  private describeCameraError(error: unknown): string {
    const name = (error as { name?: string })?.name;

    switch (name) {
      case 'NotAllowedError':
      case 'SecurityError':
        return 'Camera access was blocked. Allow the camera for this site, or type the code below.';
      case 'NotFoundError':
      case 'OverconstrainedError':
        return 'No suitable camera was found on this device.';
      case 'NotReadableError':
        return 'The camera is already in use by another app.';
      default:
        return (error as Error)?.message || 'The camera could not be started.';
    }
  }
}
