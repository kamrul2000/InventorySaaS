import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchableSelectModule } from '../../../shared/searchable-select/searchable-select.module';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { InventoryService } from '../../../core/services/inventory.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { AuthService } from '../../../core/services/auth.service';
import { InventoryBalanceDto, InventoryTransactionDto, WarehouseDto } from '../../../core/models/domain.models';

/** Roles the API's ManagerUp policy lets post a stock adjustment. */
const ADJUSTMENT_ROLES = ['TenantAdmin', 'Manager', 'SuperAdmin'];

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [
    SearchableSelectModule,
    CommonModule,
    FormsModule,
    MatIconModule,
    MatTableModule,
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
    private authService: AuthService,
    private router: Router
  ) {}

  get canAdjust(): boolean {
    const roles = this.authService.getUserRoles();
    return ADJUSTMENT_ROLES.some((role) => roles.includes(role));
  }

  ngOnInit(): void {
    this.searchWarehouses('');
    this.loadData();
  }

  searchWarehouses(search: string): void {
    this.warehouseService.getAll({ pageSize: 100, search }).subscribe({
      next: (result) => this.warehouses = result.items,
    });
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
    this.router.navigate(['/inventory/stock-in']);
  }

  openStockOut(): void {
    this.router.navigate(['/inventory/stock-out']);
  }

  openTransfer(): void {
    this.router.navigate(['/inventory/transfer']);
  }

  openAdjustment(): void {
    this.router.navigate(['/inventory/adjustment']);
  }
}
