import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomersDataService } from '../../core/data/customers-data.service';
import '../../core/locale/es-locale';

@Component({
  selector: 'app-customers-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './customers-page.html',
  styleUrl: './customers-page.scss',
  standalone: true,
})
export class CustomersPage {
  readonly data = inject(CustomersDataService);
  readonly query = signal('');
  readonly filtered = computed(() => {
    const term = this.query().trim().toLocaleLowerCase('es');
    return this.data.customers().filter((customer) => !term || `${customer.name} ${customer.addressLabel}`.toLocaleLowerCase('es').includes(term));
  });

  constructor() { this.load(); }

  load(): void { this.data.loadCustomers().subscribe({ error: () => undefined }); }
}
