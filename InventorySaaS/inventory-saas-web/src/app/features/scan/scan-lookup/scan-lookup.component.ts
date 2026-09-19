import { Component, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { ScanService } from '../../../core/services/scan.service';
import { ScanTargetComponent } from '../../../shared/scanner/scan-target.component';
import { ScanResultDto, ScannedProductDto, ScannedStockDto } from '../../../core/models/scan.models';

/** A compact record of what was scanned, so staff can retrace the last few reads. */
interface RecentScan {
  code: string;
  label: string;
  matched: boolean;
  at: Date;
}

@Component({
  selector: 'app-scan-lookup',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, ScanTargetComponent],
  templateUrl: './scan-lookup.component.html',
  styleUrl: './scan-lookup.component.css',
})
export class ScanLookupComponent {
  private static readonly MaxRecentScans = 8;

  @ViewChild(ScanTargetComponent) private scanTarget?: ScanTargetComponent;

  private readonly scanService = inject(ScanService);

  readonly loading = signal(false);
  readonly result = signal<ScanResultDto | null>(null);
  readonly recent = signal<RecentScan[]>([]);

  onScanned(code: string): void {
    this.loading.set(true);

    this.scanService.findProduct(code).subscribe({
      next: (result) => {
        this.loading.set(false);
        this.result.set(result);

        if (result.matched && result.product) {
          this.remember(code, result.product.name, true);
          this.scanTarget?.reportSuccess(result.product.name);
        } else {
          this.remember(code, 'Not found', false);
          this.scanTarget?.reportFailure(
            result.message ?? 'That barcode is not assigned to a product.'
          );
        }
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.result.set(null);
        this.remember(code, 'Lookup failed', false);

        // The API's problem-details body carries the useful message; fall back if absent.
        const message =
          error.error?.detail ?? error.error?.error ?? 'The lookup failed. Please try again.';
        this.scanTarget?.reportFailure(message);
      },
    });
  }

  get product(): ScannedProductDto | null {
    return this.result()?.product ?? null;
  }

  /** True when the scan matched but resolved to nothing usable. */
  get notFound(): boolean {
    const current = this.result();
    return current !== null && !current.matched;
  }

  clear(): void {
    this.result.set(null);
    this.scanTarget?.clearStatus();
    this.scanTarget?.refocus();
  }

  /** Bins holding stock come first; empty rows are still shown but sink to the bottom. */
  sortedStock(product: ScannedProductDto): ScannedStockDto[] {
    return [...product.stock].sort((a, b) => b.quantityAvailable - a.quantityAvailable);
  }

  isExpiringSoon(expiry?: string): boolean {
    if (!expiry) return false;
    const days = (new Date(expiry).getTime() - Date.now()) / 86_400_000;
    return days <= 90;
  }

  isExpired(expiry?: string): boolean {
    return expiry ? new Date(expiry).getTime() < Date.now() : false;
  }

  private remember(code: string, label: string, matched: boolean): void {
    this.recent.update((entries) =>
      [{ code, label, matched, at: new Date() }, ...entries].slice(0, ScanLookupComponent.MaxRecentScans)
    );
  }
}
