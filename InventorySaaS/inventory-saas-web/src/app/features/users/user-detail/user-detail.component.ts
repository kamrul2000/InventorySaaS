import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { UserService } from '../../../core/services/user.service';
import { User } from '../../../core/models/auth.models';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './user-detail.component.html',
  styleUrl: './user-detail.component.css',
})
export class UserDetailComponent implements OnInit {
  user: User | null = null;
  loading = true;

  constructor(
    private userService: UserService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.userService.getById(id).subscribe({
        next: (u) => { this.user = u; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.user) this.router.navigate(['/users', this.user.id, 'edit']); }
  back(): void { this.router.navigate(['/users']); }
}
