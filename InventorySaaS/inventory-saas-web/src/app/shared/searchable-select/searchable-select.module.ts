import { NgModule } from '@angular/core';
import { MatSelectModule } from '@angular/material/select';
import { SearchableSelectDirective } from './searchable-select.directive';

@NgModule({
  imports: [MatSelectModule, SearchableSelectDirective],
  exports: [MatSelectModule, SearchableSelectDirective],
})
export class SearchableSelectModule {}
