/**
 * Minimal typings for the browser's native Barcode Detection API.
 *
 * It ships in Chromium browsers (notably Chrome on Android, which is what warehouse tablets
 * and phones run) but is absent from Firefox and, at time of writing, desktop/iOS Safari.
 * `isCameraScanningSupported` is the single place that decides whether the camera tab is
 * offered at all; where it isn't, the keyboard-wedge and manual paths still work everywhere.
 *
 * The API is declared here rather than pulled from a DOM lib because TypeScript's bundled DOM
 * typings do not yet include it.
 */

export interface DetectedBarcode {
  rawValue: string;
  format: string;
  boundingBox: DOMRectReadOnly;
  cornerPoints: ReadonlyArray<{ x: number; y: number }>;
}

export interface BarcodeDetectorInstance {
  detect(source: CanvasImageSource | Blob | ImageData): Promise<DetectedBarcode[]>;
}

export interface BarcodeDetectorConstructor {
  new (options?: { formats?: string[] }): BarcodeDetectorInstance;
  getSupportedFormats(): Promise<string[]>;
}

/**
 * The symbologies an inventory system actually encounters: retail EANs/UPCs, Code 128/39 on
 * internal labels, ITF-14 on cartons, and QR/Data Matrix for structured payloads.
 */
export const REQUESTED_BARCODE_FORMATS = [
  'qr_code',
  'data_matrix',
  'code_128',
  'code_39',
  'code_93',
  'ean_13',
  'ean_8',
  'upc_a',
  'upc_e',
  'itf',
  'codabar',
  'pdf417',
];

export function getBarcodeDetector(): BarcodeDetectorConstructor | undefined {
  return (globalThis as unknown as { BarcodeDetector?: BarcodeDetectorConstructor }).BarcodeDetector;
}

/**
 * Camera scanning needs both the detector and a secure context — `getUserMedia` is refused on
 * plain HTTP outside localhost, which is the usual reason a warehouse tablet sees no camera.
 */
export function isCameraScanningSupported(): boolean {
  return (
    getBarcodeDetector() !== undefined &&
    typeof navigator !== 'undefined' &&
    navigator.mediaDevices?.getUserMedia !== undefined
  );
}

/** Distinguishes "your browser can't" from "this page isn't served over HTTPS". */
export function cameraUnsupportedReason(): string | null {
  if (typeof window !== 'undefined' && !window.isSecureContext) {
    return 'Camera scanning needs a secure (HTTPS) connection. Use a USB or Bluetooth scanner, or type the code below.';
  }

  if (getBarcodeDetector() === undefined) {
    return 'This browser cannot scan with the camera. Chrome on Android is recommended — or use a USB or Bluetooth scanner, or type the code below.';
  }

  if (typeof navigator === 'undefined' || navigator.mediaDevices?.getUserMedia === undefined) {
    return 'No camera is available on this device. Use a USB or Bluetooth scanner, or type the code below.';
  }

  return null;
}
