import { Directive, EventEmitter, Input, OnDestroy, OnInit, Output, Renderer2 } from '@angular/core';
import { MatOption, MatSelect } from '@angular/material/select';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';

@Directive({
  selector: 'mat-select[appSearchableSelect]',
  standalone: true,
})
export class SearchableSelectDirective implements OnInit, OnDestroy {
  @Input() searchPlaceholder = 'Search options...';
  @Output() selectSearch = new EventEmitter<string>();

  private readonly searchTerms = new Subject<string>();
  private readonly subscriptions = new Subscription();
  private currentTerm = '';
  private input: HTMLInputElement | null = null;
  private emptyState: HTMLDivElement | null = null;

  constructor(
    private readonly select: MatSelect,
    private readonly renderer: Renderer2,
  ) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.select.openedChange.subscribe((opened) => {
        // Material emits before the overlay panel is attached to the DOM.
        if (opened) setTimeout(() => this.attachSearch());
        else this.resetSearch();
      }),
    );
    this.subscriptions.add(
      this.searchTerms.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
        this.currentTerm = term;
        this.filterRenderedOptions();
        this.selectSearch.emit(term);
      }),
    );
    this.subscriptions.add(
      this.select.options.changes.subscribe(() => this.filterRenderedOptions()),
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private attachSearch(): void {
    const panel = this.select.panel?.nativeElement as HTMLElement | undefined;
    if (!panel || panel.querySelector('.app-select-search')) return;

    const search = this.renderer.createElement('div') as HTMLDivElement;
    const icon = this.renderer.createElement('span') as HTMLSpanElement;
    const input = this.renderer.createElement('input') as HTMLInputElement;
    const emptyState = this.renderer.createElement('div') as HTMLDivElement;

    this.renderer.addClass(search, 'app-select-search');
    this.renderer.addClass(icon, 'app-select-search-icon');
    this.renderer.setAttribute(icon, 'aria-hidden', 'true');
    this.renderer.setProperty(icon, 'textContent', 'search');
    this.renderer.setAttribute(input, 'type', 'search');
    this.renderer.setAttribute(input, 'autocomplete', 'off');
    this.renderer.setAttribute(input, 'placeholder', this.searchPlaceholder);
    this.renderer.setAttribute(input, 'aria-label', this.searchPlaceholder);
    this.renderer.addClass(emptyState, 'app-select-empty');
    this.renderer.setProperty(emptyState, 'textContent', 'No matching options');
    this.renderer.setStyle(emptyState, 'display', 'none');

    this.renderer.listen(search, 'mousedown', (event: MouseEvent) => event.stopPropagation());
    this.renderer.listen(search, 'click', (event: MouseEvent) => event.stopPropagation());
    this.renderer.listen(input, 'keydown', (event: KeyboardEvent) => {
      if (event.key === 'Escape') this.select.close();
      event.stopPropagation();
    });
    this.renderer.listen(input, 'input', () => this.searchTerms.next(input.value.trim()));

    this.renderer.appendChild(search, icon);
    this.renderer.appendChild(search, input);
    this.renderer.insertBefore(panel, search, panel.firstChild);
    this.renderer.insertBefore(panel, emptyState, search.nextSibling);
    this.input = input;
    this.emptyState = emptyState;

    setTimeout(() => input.focus());
  }

  private resetSearch(): void {
    if (!this.currentTerm && !this.input?.value) return;
    this.currentTerm = '';
    if (this.input) this.input.value = '';
    this.filterRenderedOptions();
    this.searchTerms.next('');
    this.input = null;
    this.emptyState = null;
  }

  private filterRenderedOptions(): void {
    const normalized = this.currentTerm.toLocaleLowerCase();
    let visibleCount = 0;
    this.select.options.forEach((option: MatOption) => {
      const host = (option as MatOption & { _getHostElement(): HTMLElement })._getHostElement();
      const visible = !normalized || option.viewValue.toLocaleLowerCase().includes(normalized);
      this.renderer.setStyle(host, 'display', visible ? '' : 'none');
      if (visible) visibleCount += 1;
    });
    if (this.emptyState) {
      this.renderer.setStyle(this.emptyState, 'display', visibleCount === 0 ? 'block' : 'none');
    }
  }
}
