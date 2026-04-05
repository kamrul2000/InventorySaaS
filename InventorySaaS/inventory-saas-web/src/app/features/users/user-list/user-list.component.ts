import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { UserService } from '../../../core/services/user.service';
import { User } from '../../../core/models/auth.models';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.css',
})
export class UserListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'email', label: 'Email' },
    { key: 'firstName', label: 'First Name' },
    { key: 'lastName', label: 'Last Name' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
    { key: 'createdAt', label: 'Created', type: 'date' },
  ];

  users: User[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(
    private userService: UserService, private router: Router, private dialog: MatDialog
  ) {}

  ngOnInit(): void { this.loadUsers(); }

  loadUsers(): void {
    this.loading = true;
    this.userService.getAll({ pageNumber: this.pageNumber, pageSize: this.pageSize, searchTerm: this.searchTerm }).subscribe({
      next: (r) => { this.users = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  addUser(): void { this.router.navigate(['/users/new']); }

  inviteUser(): void {
    // Could open a dialog; for now navigate to form
    this.router.navigate(['/users/new']);
  }

  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadUsers(); }
  onSortChange(_s: Sort): void { this.loadUsers(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadUsers(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const user = event.row as User;
    if (event.action === 'view') {
      this.router.navigate(['/users', user.id]);
    } else if (event.action === 'edit') {
      this.router.navigate(['/users', user.id, 'edit']);
    } else if (event.action === 'toggle:isActive') {
      this.userService.update(user.id, { isActive: !user.isActive }).subscribe({
        next: () => this.loadUsers(),
      });
    }
  }
}
