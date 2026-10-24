import { AfterViewInit, Directive, ElementRef, OnDestroy } from '@angular/core';

/**
 * Fades an element in the first time it scrolls into view. Toggles the classes
 * on the element directly (not via host bindings) so it does not wait for a
 * change-detection pass — the dashboard bootstraps with run coalescing.
 */
@Directive({
  selector: '[appReveal]',
  standalone: true
})
export class RevealDirective implements AfterViewInit, OnDestroy {
  private observer?: IntersectionObserver;

  constructor(private el: ElementRef<HTMLElement>) {
    el.nativeElement.classList.add('reveal');
  }

  ngAfterViewInit(): void {
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce || typeof IntersectionObserver === 'undefined') {
      this.show();
      return;
    }
    this.observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) {
        this.show();
        this.observer?.disconnect();
      }
    }, { threshold: 0.16 });
    this.observer.observe(this.el.nativeElement);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private show(): void {
    this.el.nativeElement.classList.add('is-in');
  }
}
