import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { InventoryService } from '../../../core/services/inventory.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { InventoryBalanceDto, InventoryTransactionDto, WarehouseDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
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

  constructor(
    private inventoryService: InventoryService,
    private warehouseService: WarehouseService,
    private router: Router
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

  goBalancePage(page: number): void {
    this.balancePage = page;
    this.loadBalances();
  }

  goTxPage(page: number): void {
    this.txPage = page;
    this.loadTransactions();
  }

  openStockIn(): void {
    this.router.navigate(['/inventory/stock-in']);
  }

  openTransfer(): void {
    this.router.navigate(['/inventory/transfer']);
  }
}
