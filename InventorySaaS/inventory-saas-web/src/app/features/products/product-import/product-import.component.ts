import { Component, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ProductImportService } from '../../../core/services/product-import.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ProductImportResult, ProductImportRow } from '../../../core/models/domain.models';

const MAX_CSV_BYTES = 5 * 1024 * 1024;

@Component({
  selector: 'app-product-import',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './product-import.component.html',
  styleUrl: './product-import.component.css',
})
export class ProductImportComponent {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  file: File | null = null;
  createMissingMasters = false;
  previewing = false;
  importing = false;
  downloading = false;

  /** Result of the last preview or import; drives the whole lower half of the page. */
  result: ProductImportResult | null = null;

  /** Set once the file has been imported, so it can't be submitted twice. */
  committed = false;

  constructor(
    private importService: ProductImportService,
    private notification: NotificationService,
    private router: Router
  ) {}

  get busy(): boolean {
    return this.previewing || this.importing;
  }

  get canImport(): boolean {
    if (!this.result || this.committed || this.busy) return false;
    return this.result.isPreview && this.result.validRows > 0;
  }

  get createsMasters(): boolean {
    if (!this.result) return false;
    return this.result.newCategories.length > 0
      || this.result.newBrands.length > 0
      || this.result.newUnits.length > 0;
  }

  openFilePicker(): void {
    this.fileInput?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const chosen = input.files?.[0];
    input.value = '';
    if (!chosen) return;

    if (!chosen.name.toLowerCase().endsWith('.csv')) {
      this.notification.error('Please choose a .csv file.');
      return;
    }
    if (chosen.size > MAX_CSV_BYTES) {
      this.notification.error('File must be 5 MB or smaller.');
      return;
    }

    this.file = chosen;
    this.result = null;
    this.committed = false;
    this.preview();
  }

  /** Re-runs the dry run — also used when the "create missing" choice changes. */
  preview(): void {
    if (!this.file || this.busy) return;

    this.previewing = true;
    this.committed = false;
    this.importService.preview(this.file, this.createMissingMasters).subscribe({
      next: (result) => {
        this.result = result;
        this.previewing = false;
      },
      error: () => {
        this.result = null;
        this.previewing = false;
      },
    });
  }

  runImport(): void {
    if (!this.file || this.busy) return;

    this.importing = true;
    this.importService.import(this.file, this.createMissingMasters).subscribe({
      next: (result) => {
        this.result = result;
        this.committed = true;
        this.importing = false;
        this.notification.success(
          `Imported ${result.importedRows} product(s).` +
          (result.invalidRows > 0 ? ` ${result.invalidRows} row(s) skipped.` : '')
        );
      },
      error: () => { this.importing = false; },
    });
  }

  downloadTemplate(): void {
    this.downloading = true;
    this.importService.downloadTemplate().subscribe({
      next: (blob) => {
        this.save(blob, 'product-import-template.csv');
        this.downloading = false;
      },
      error: () => { this.downloading = false; },
    });
  }

  clear(): void {
    this.file = null;
    this.result = null;
    this.committed = false;
  }

  goToProducts(): void {
    this.router.navigate(['/products']);
  }

  /** Failed rows first — those are the ones needing attention. */
  get sortedRows(): ProductImportRow[] {
    if (!this.result) return [];
    return [...this.result.rows].sort((a, b) => {
      const aBad = a.status === 'Invalid' ? 0 : 1;
      const bBad = b.status === 'Invalid' ? 0 : 1;
      return aBad - bBad || a.lineNumber - b.lineNumber;
    });
  }

  private save(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
