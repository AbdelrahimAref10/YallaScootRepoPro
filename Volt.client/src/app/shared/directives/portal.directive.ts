import { Directive, ElementRef, OnDestroy, OnInit, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';

/**
 * Renders the element at the end of <body> instead of where it sits in the template.
 *
 * Put it on modal overlays and toasts: a `position: fixed` element is only fixed to the
 * viewport when no ancestor has a transform, filter or animation. Page containers often do
 * (entrance animations), which pins the "fixed" overlay to the top of the page instead of
 * the screen. Bindings and styles keep working; the element just lives under <body>.
 *
 *   @if (open) { <div class="modal__overlay" appPortal>…</div> }
 */
@Directive({
  selector: '[appPortal]',
  standalone: true
})
export class PortalDirective implements OnInit, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);

  ngOnInit(): void {
    this.document.body.appendChild(this.host.nativeElement);
  }

  ngOnDestroy(): void {
    this.host.nativeElement.remove();
  }
}
