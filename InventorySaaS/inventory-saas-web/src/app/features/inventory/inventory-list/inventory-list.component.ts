import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { InventoryService } from '../../../core/services/inventory.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { InventoryBalanceDto, InventoryTransactionDto, WarehouseDto } from '../../../core/models/domain.models';
import { StockInComponent } from '../stock-in/stock-in.component';
import { StockTransferComponent } from '../stock-transfer/stock-transfer.component';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTabsModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './inventory-list.component.html',
  styleUrl: './inventory-list.component.css',
})
export class InventoryListComponent implements OnInit {
  balances: InventoryBalanceDto[] = [];
  transactions: InventoryTransactionDto[] = [];
  warehouses: WarehouseDto[] = [];
  warehouseFilter = '';
  pageSize = 10;
  balancePage = 1;
  txPage = 1;
  balanceTotalCount = 0;
  txTotalCount = 0;
  loadingBalances = false;
  loadingTransactions = false;
  activeTab = 0;

  balanceColumns = ['productName', 'productSku', 'warehouseName', 'locationName', 'quantityOnHand', 'quantityReserved', 'quantityAvailable', 'unitCost'];
  txColumns = ['transactionNumber', 'transactionType', 'productName', 'warehouseName', 'quantity', 'transactionDate'];

  constructor(
    private inventoryService: InventoryService,
    private warehouseService: WarehouseService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.warehouses = result.items,
    });
    this.loadData();
  }

  loadData(): void {
    if (this.activeTab === 0) this.loadBalances();
    else this.loadTransactions();
  }

  loadBalances(): void {
    this.loadingBalances = true;
    this.inventoryService.getBalances({
      pageNumber: this.balancePage,
      pageSize: this.pageSize,
      warehouseId: this.warehouseFilter || undefined,
    }).subscribe({
      next: (result) => {
        this.balances = result.items;
        this.balanceTotalCount = result.totalCount;
        this.loadingBalances = false;
      },
      error: () => { this.loadingBalances = false; },
    });
  }

  loadTransactions(): void {
    this.loadingTransactions = true;
    this.inventoryService.getTransactions({
      pageNumber: this.txPage,
      pageSize: this.pageSize,
      warehouseId: this.warehouseFilter || undefined,
    }).subscribe({
      next: (result) => {
        this.transactions = result.items;
        this.txTotalCount = result.totalCount;
        this.loadingTransactions = false;
      },
      error: () => { this.loadingTransactions = false; },
    });
  }

  onTabChange(index: number): void {
    this.activeTab = index;
    this.loadData();
  }

  onBalancePage(event: PageEvent): void {
    this.balancePage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadBalances();
  }

  onTxPage(event: PageEvent): void {
    this.txPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadTransactions();
  }

  openStockIn(): void {
    const dialogRef = this.dialog.open(StockInComponent, { width: '600px' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) this.loadData();
    });
  }

  openTransfer(): void {
    const dialogRef = this.dialog.open(StockTransferComponent, { width: '600px' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) this.loadData();
    });
  }
}
